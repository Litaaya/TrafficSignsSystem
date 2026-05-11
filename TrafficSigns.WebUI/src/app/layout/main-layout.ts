import { Component, OnInit, ChangeDetectorRef, HostListener, inject } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Subject, debounceTime, switchMap, map, catchError, of } from 'rxjs';
import { AuthService } from '../core/services/auth-service';
import { OsmRoadService } from '../features/map/osm-road.service';

@Component({
  selector: 'app-main-layout',
  standalone: false,
  templateUrl: './main-layout.html'
})
export class MainLayoutComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private osmService = inject(OsmRoadService);

  isAdmin = false;
  isUserDropdownOpen = false;
  displayUsername: string = " ";
  userInitials: string = " ";
  
  accounts: any[] = [];
  selectedAccountId: string = '';

  isMyInfoModalOpen = false;
  infoActiveTab: 'profile' | 'workspaces' = 'profile';
  currentUser: any = null;
  originalCurrentUser: any = null;
  isSubmittingProfile = false;
  infoMessage: { text: string, type: 'success' | 'error' } | null = null;

  isProfileFormValid = false;
  profileFieldStatus: any = {
    email: { checking: false, valid: true, error: '' },
    phone: { checking: false, valid: true, error: '' }
  };

  isDarkMode: boolean = false;

  private profileCheckSubject = new Subject<{ field: string, value: string }>();
  private readonly API_URL = 'https://localhost:7272/api';
  private readonly KEYCLOAK_URL = 'http://localhost:8181';
  private readonly REALM_NAME = 'trafficsigns-realm';

  get currentAccountRole(): string {
    const currentAcc = this.accounts.find(a => a.accountId === this.selectedAccountId);
    return currentAcc?.role || 'Viewer';
  }

  ngOnInit(): void {
    this.isAdmin = this.authService.isAdmin();
    this.initUserData();
    this.initProfileRealtimeValidation();
    this.isDarkMode = localStorage.getItem('theme') === 'dark';
    this.applyTheme();
    this.loadAccounts();
  }

  loadAccounts() {
    const userId = this.authService.getUserId();
    if (!userId) return;

    this.osmService.getAccountsOfUser(userId).subscribe({
      next: (res: any) => {
        this.accounts = res || [];
        const savedAccountId = localStorage.getItem('lastSelectedAccountId');
        const found = this.accounts.find(a => a.accountId === savedAccountId);
        this.selectedAccountId = (savedAccountId && found) ? savedAccountId : (this.accounts[0]?.accountId || '');
        this.cdr.detectChanges();
      }
    });
  }

  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event) {
    const target = event.target as HTMLElement;
    if (!target.closest('.user-dropdown-container')) {
      this.isUserDropdownOpen = false;
    }
  }

  goToSecuritySettings() {
    const securityUrl = `${this.KEYCLOAK_URL}/realms/${this.REALM_NAME}/account/`;
    window.open(securityUrl, '_blank');
  }

  initProfileRealtimeValidation() {
    this.profileCheckSubject.pipe(
      debounceTime(300),
      switchMap(data => {
        this.profileFieldStatus[data.field].checking = true;
        this.updateProfileFormValidity();
        this.cdr.detectChanges();
        let queryParams: any = { field: data.field, value: data.value };
        if (this.currentUser?.id) queryParams.excludeId = this.currentUser.id;
        
        return this.http.get<any>(`${this.API_URL}/users/validate-field`, { params: queryParams }).pipe(
          map((res: any) => ({ ...res, field: data.field })),
          catchError(() => of({ isValid: false, message: 'Validation service error', field: data.field, hasError: true }))
        );
      })
    ).subscribe({
      next: (res: any) => {
        this.profileFieldStatus[res.field].checking = false;
        if (res.hasError) {
          this.profileFieldStatus[res.field].valid = false;
          this.profileFieldStatus[res.field].error = res.message;
        } else {
          this.profileFieldStatus[res.field].valid = res.isValid;
          this.profileFieldStatus[res.field].error = res.isValid ? '' : res.message;
        }
        this.updateProfileFormValidity();
        this.cdr.detectChanges();
      }
    });
  }

  onProfileFieldChange(field: string, value: string) {
    if (!value || value.trim().length < 2) {
      this.profileFieldStatus[field] = { checking: false, valid: false, error: '' };
      this.updateProfileFormValidity();
      this.cdr.detectChanges();
      return;
    }
    this.profileFieldStatus[field].checking = true;
    this.profileFieldStatus[field].valid = false;
    this.profileFieldStatus[field].error = '';
    this.updateProfileFormValidity();
    this.cdr.detectChanges();
    this.profileCheckSubject.next({ field, value });
  }

  onProfileTextChange() {
    this.updateProfileFormValidity();
  }

  updateProfileFormValidity() {
    if (!this.currentUser || !this.originalCurrentUser) {
      this.isProfileFormValid = false;
      return;
    }
    const isEmailValid = this.profileFieldStatus['email'].valid;
    const isPhoneValid = this.profileFieldStatus['phone'].valid;
    const isChecking = this.profileFieldStatus['email'].checking || this.profileFieldStatus['phone'].checking;
    const hasRequired = !!this.currentUser.email && !!this.currentUser.phone && !!this.currentUser.firstName && !!this.currentUser.lastName;
    
    const emailChanged = this.currentUser.email !== this.originalCurrentUser.email;
    const phoneChanged = this.currentUser.phone !== this.originalCurrentUser.phone;
    const firstChanged = this.currentUser.firstName !== this.originalCurrentUser.firstName;
    const lastChanged = this.currentUser.lastName !== this.originalCurrentUser.lastName;
    const hasChange = emailChanged || phoneChanged || firstChanged || lastChanged;

    this.isProfileFormValid = isEmailValid && isPhoneValid && !isChecking && hasRequired && hasChange && !this.isSubmittingProfile;
  }

  saveProfile() {
    if (!this.currentUser || !this.isProfileFormValid) return;

    this.isSubmittingProfile = true;
    this.infoMessage = null;

    const command = {
      email: this.currentUser.email,
      phone: this.currentUser.phone,
      firstName: this.currentUser.firstName,
      lastName: this.currentUser.lastName
    };

    this.http.put(`${this.API_URL}/users/profile`, command).subscribe({
      next: () => {
        this.isSubmittingProfile = false;
        this.infoMessage = { text: 'Profile updated successfully!', type: 'success' };
        this.originalCurrentUser = { ...this.currentUser };
        this.displayUsername = `${this.currentUser.firstName} ${this.currentUser.lastName}`.trim() || this.currentUser.username;      
        this.updateUserInitials(); 
        this.updateProfileFormValidity();
        
        setTimeout(() => { 
          this.infoMessage = null; 
          this.cdr.detectChanges(); 
        }, 3000);
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.isSubmittingProfile = false;            
        this.infoMessage = { 
          text: err.error?.detail || 'Failed to update profile.', 
          type: 'error' 
        };
        this.cdr.detectChanges();
      }
    });
  }

  private updateUserInitials() {
    const first = this.currentUser.firstName?.[0] || '';
    const last = this.currentUser.lastName?.[0] || '';
    this.userInitials = (first + last).toUpperCase() || '??';
  }

  private initUserData() {
    const username = this.authService.getUsername() || 'User';
    const firstName = this.authService.getFirstName() || '';
    const lastName = this.authService.getLastName() || '';

    this.displayUsername = username;

    if (firstName && lastName) {
      this.userInitials = (firstName.charAt(0) + lastName.charAt(0)).toUpperCase();
    } else if (username) {
      this.userInitials = username.substring(0, 2).toUpperCase();
    } else {
      this.userInitials = 'U';
    }

    this.cdr.detectChanges();
  }

  toggleUserDropdown() {
    this.isUserDropdownOpen = !this.isUserDropdownOpen;
  }

  toggleDarkMode() {
    this.isDarkMode = !this.isDarkMode;
    this.applyTheme();
  }

  private applyTheme() {
    if (this.isDarkMode) {
      document.documentElement.classList.add('dark');
      localStorage.setItem('theme', 'dark');
    } else {
      document.documentElement.classList.remove('dark');
      localStorage.setItem('theme', 'light');
    }
  }

  openMyInfo() {
    this.isUserDropdownOpen = false;
    this.isMyInfoModalOpen = true;
    this.infoMessage = null;
    
    const userId = this.authService.getUserId();
    if (userId) {
      this.http.get(`${this.API_URL}/users/${userId}`).subscribe({
        next: (res) => {
          this.currentUser = { ...res };
          this.originalCurrentUser = { ...res };
          this.profileFieldStatus = {
            email: { checking: false, valid: true, error: '' },
            phone: { checking: false, valid: true, error: '' }
          };
          this.updateProfileFormValidity();
          this.cdr.detectChanges();
        }
      });
    }
  }

  closeMyInfoModal() {
    this.isMyInfoModalOpen = false;
    this.infoMessage = null;
    this.isSubmittingProfile = false;
  }

  logout() {
    this.isUserDropdownOpen = false;
    this.authService.logout();
    this.router.navigate(['/login']);
  }
} 
