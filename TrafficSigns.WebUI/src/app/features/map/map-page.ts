import { Component, OnInit, AfterViewInit, NgZone, ChangeDetectorRef, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpParams, HttpHeaders } from '@angular/common/http';
import * as L from 'leaflet';
import { OsmRoadService } from './osm-road.service';
import { SIGN_TEMPLATES, SPEED_OPTIONS, VEHICLE_OPTIONS, SignTemplate } from './traffic-sign-rules';
import { AuthService } from '../../core/services/auth-service';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subject, debounceTime, switchMap, map, distinctUntilChanged, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

@Component({
  selector: 'app-map-page',
  standalone: true,
  imports: [CommonModule, FormsModule, MatTooltipModule],
  templateUrl: './map-page.html',
  styleUrls: ['./map-page.scss']
})
export class MapPageComponent implements OnInit, AfterViewInit {
  private http = inject(HttpClient);
  private osmService = inject(OsmRoadService);
  private zone = inject(NgZone);
  private cdr = inject(ChangeDetectorRef);
  private authService = inject(AuthService);

  private map!: L.Map;
  private signLayer: L.LayerGroup = new L.LayerGroup();
  private selectionMarker: L.CircleMarker | null = null;
  private readonly API_URL = 'https://localhost:7272/api';
  private readonly KEYCLOAK_URL = 'http://localhost:8181';
  private readonly REALM_NAME = 'trafficsigns-realm';

  private profileCheckSubject = new Subject<{ field: string, value: string }>();

  isAccountDropdownOpen = false;
  isUserDropdownOpen = false;
  accountSearchTerm = '';
  hasNoAccounts: boolean = false;
  accounts: any[] = [];
  selectedAccountId: string = '';
  sidebarState: 'HIDDEN' | 'SIGN_INFO' | 'WIZARD' = 'HIDDEN';
  wizardStep: 'MAIN_CATEGORY' | 'SUB_CATEGORY' | 'TYPE' | 'FORM' | 'REVIEW' = 'MAIN_CATEGORY';

  showDeletedMode: boolean = false;
  showConfirmModal: boolean = false;
  confirmActionType: 'DELETE' | 'REACTIVATE' | null = null;

  isSaving: boolean = false;
  selectedMainCategory: string = '';
  selectedSubCategory: string = '';
  selectedSignDetails: any = null;
  selectedTemplate: SignTemplate | null = null;
  laneCount: number = 1;
  lanes: any[] = [];
  activeLaneIndex: number = 0;
  clickedLatLng: { lat: number; lng: number } | null = null;
  selectedSignId: string | null = null;
  selectedSignData: any = null;

  startTime: string = '22:00';
  endTime: string = '05:00';
  dp127HasVehicles: boolean = false;
  mouseLatLng: string = '0.000000, 0.000000';

  isAccountUserModalOpen = false;
  accountUsers: any[] = [];
  loadingUsers = false;
  
  inviteEmail: string = '';
  inviteRole: string = 'Member';
  isInviteRoleDropdownOpen: boolean = false;
  isSubmittingInvite: boolean = false;

  memberListSearchTerm: string = '';
  memberListFilter: 'all' | 'Owner' | 'Member' | 'Viewer' = 'all';
  isListRoleFilterOpen: boolean = false;

  isRoleModalOpen = false;
  isRoleDropdownOpen = false;
  roleTargetUser: any = null;
  selectedRole: string = 'Viewer';

  isWithdrawModalOpen = false;
  withdrawTargetUser: any = null;
  isSubmitting = false;

  displayUsername: string = " ";
  userInitials: string = " ";

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

  readonly speedOptions = SPEED_OPTIONS;
  readonly vehicleOptions = VEHICLE_OPTIONS;
  readonly signTemplates = SIGN_TEMPLATES;

  constructor() { }

  goToSecuritySettings() {
    const securityUrl = `${this.KEYCLOAK_URL}/realms/${this.REALM_NAME}/account/`;
    window.open(securityUrl, '_blank');
  }

  onSearchTermChange() {
    this.cdr.detectChanges();
  }

  get currentAccountRole(): string {
    const currentAcc = this.accounts.find(a => a.accountId === this.selectedAccountId);
    return currentAcc?.role || 'Viewer';
  }

  get isOwner(): boolean {
    return this.currentAccountRole === 'Owner';
  }

  get canEdit(): boolean {
    return this.currentAccountRole === 'Owner' || this.currentAccountRole === 'Member';
  }

  ngOnInit(): void {
    this.loadAccounts();
    this.initUserData();
    this.initProfileRealtimeValidation();
    this.isDarkMode = localStorage.getItem('theme') === 'dark';
    this.applyTheme();
  }

  ngAfterViewInit(): void { }

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

  loadAccounts() {
    const userId = this.authService.getUserId();
    if (!userId) return;

    this.osmService.getAccountsOfUser(userId).subscribe({
      next: (res: any) => {
        this.accounts = res;
        if (!this.accounts || this.accounts.length === 0) {
          this.hasNoAccounts = true;
          this.cdr.detectChanges();
          return;
        }
        this.hasNoAccounts = false;
        const savedAccountId = localStorage.getItem('lastSelectedAccountId');
        const found = this.accounts.find(a => a.accountId === savedAccountId);
        this.selectedAccountId = (savedAccountId && found) ? savedAccountId : (this.accounts[0]?.accountId || '');

        if (!this.map) {
          setTimeout(() => this.initMap(), 0);
        } else {
          this.loadTrafficSigns();
        }
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.hasNoAccounts = true;
        this.cdr.detectChanges();
      }
    });
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

  get filteredAccounts() {
    if (!this.accountSearchTerm) return this.accounts;
    return this.accounts.filter(acc =>
      acc.accountName.toLowerCase().includes(this.accountSearchTerm.toLowerCase())
    );
  }

  selectAccount(acc: any) {
    this.selectedAccountId = acc.accountId;
    this.accountSearchTerm = '';
    this.isAccountDropdownOpen = false;
    this.onAccountChange();
  }

  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event) {
    const target = event.target as HTMLElement;
    if (!target.closest('.account-dropdown-container')) {
      this.isAccountDropdownOpen = false;
    }
    if (!target.closest('.user-dropdown-container')) {
      this.isUserDropdownOpen = false;
    }
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

  onAccountChange() {
    localStorage.setItem('lastSelectedAccountId', this.selectedAccountId);
    if (!this.canEdit) this.showDeletedMode = false;
    this.closeSidebar();
    this.loadTrafficSigns();
  }

  private initMap(): void {
    const savedLat = localStorage.getItem('map_lat') || '10.762622';
    const savedLng = localStorage.getItem('map_lng') || '106.660172';
    const savedZoom = localStorage.getItem('map_zoom') || '15';
    const vietnamBounds = L.latLngBounds(L.latLng(8.0, 102.0), L.latLng(23.5, 110.0));

    const mapElement = document.getElementById('map');
    if (!mapElement) return;

    this.map = L.map('map', {
      doubleClickZoom: false, zoomControl: true,
      maxBounds: vietnamBounds, maxBoundsViscosity: 1.0, minZoom: 5
    }).setView([parseFloat(savedLat), parseFloat(savedLng)], parseInt(savedZoom));

    this.map.on('mousemove', (e: L.LeafletMouseEvent) => {
      this.zone.run(() => {
        this.mouseLatLng = `${e.latlng.lat.toFixed(6)}, ${e.latlng.lng.toFixed(6)}`;
      });
    });

    this.map.createPane('markerPane');
    this.map.getPane('markerPane')!.style.zIndex = '610';

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(this.map);
    this.signLayer.addTo(this.map);

    this.loadTrafficSigns();

    this.map.on('moveend', () => {
      const center = this.map.getCenter();
      localStorage.setItem('map_lat', center.lat.toString());
      localStorage.setItem('map_lng', center.lng.toString());
      localStorage.setItem('map_zoom', this.map.getZoom().toString());
    });

    this.map.on('click', () => {
      this.zone.run(() => this.closeSidebar());
    });

    this.map.on('dblclick', (e: L.LeafletMouseEvent) => {
      this.zone.run(() => {
        if (!this.canEdit) return;
        this.onMapClicked(e.latlng);
      });
    });
  }

  onMapClicked(latlng: L.LatLng) {
    this.selectedSignId = null;
    this.selectedSignData = null;
    this.selectedSignDetails = null;
    this.clickedLatLng = { lat: latlng.lat, lng: latlng.lng };
    this.startWizard();
    this.updateVisuals();
    this.cdr.detectChanges();
  }

  loadTrafficSigns() {
    if (!this.selectedAccountId || this.selectedAccountId === 'undefined' || !this.map) return;
    this.osmService.getTrafficSigns(this.selectedAccountId).subscribe((signs: any[]) => {
      this.signLayer.clearLayers();
      const filteredSigns = signs.filter(s => (s.isDeleted || s.IsDeleted || false) === this.showDeletedMode);

      filteredSigns.forEach(s => {
        const m = L.circleMarker([s.location.coordinates[1], s.location.coordinates[0]], {
          radius: 8,
          fillColor: this.showDeletedMode ? '#94a3b8' : '#ef4444',
          color: '#fff',
          weight: 2,
          fillOpacity: 1,
          pane: 'markerPane'
        });
        m.on('click', (e) => {
          L.DomEvent.stopPropagation(e);
          this.zone.run(() => this.showExistingSignInfo(s));
        });
        this.signLayer.addLayer(m);
      });
    });
  }

  toggleViewMode() {
    this.showDeletedMode = !this.showDeletedMode;
    this.closeSidebar();
    this.loadTrafficSigns();
  }

  showExistingSignInfo(sign: any) {
    this.selectedSignData = sign;
    this.selectedSignId = sign.id;
    this.clickedLatLng = { lat: sign.location.coordinates[1], lng: sign.location.coordinates[0] };

    const displayInfo: any = { 'Sign Code': sign.code, 'Name': sign.name };
    if (sign.metadata) {
      if (sign.metadata.startTime && sign.metadata.endTime) displayInfo['Active Time'] = `${sign.metadata.startTime} - ${sign.metadata.endTime}`;
      if (sign.metadata.totalLanes && sign.metadata.lanes) {
        displayInfo['Total Lanes'] = sign.metadata.totalLanes;
        sign.metadata.lanes.forEach((lane: any) => {
          let detail = `${lane.speed ? lane.speed + ' km/h' : 'N/A'}`;
          if (lane.vehicleTypes && lane.vehicleTypes.length > 0) {
            const vehicles = lane.vehicleTypes.map((v: string) => v.toUpperCase()).join(', ');
            detail += ` [${vehicles}]`;
          }
          displayInfo[`Lane ${lane.laneNumber}`] = detail;
        });
      } else if (sign.metadata.value) {
        displayInfo['Speed Limit'] = `${sign.metadata.value} km/h`;
      }
    }

    this.selectedSignDetails = displayInfo;
    this.sidebarState = 'SIGN_INFO';
    this.updateVisuals();
    this.cdr.detectChanges();
  }

  editSign() {
    if (!this.selectedSignData) return;
    const s = this.selectedSignData;
    const targetCode = (s.code || '').trim().toUpperCase();
    this.selectedTemplate = this.signTemplates.find(t => t.code.toUpperCase() === targetCode) || null;
    if (!this.selectedTemplate) {
      return;
    }

    this.sidebarState = 'WIZARD';
    this.wizardStep = 'FORM';
    this.lanes = [];

    if (s.metadata) {
      this.startTime = s.metadata.startTime || '22:00';
      this.endTime = s.metadata.endTime || '05:00';
      if (this.selectedTemplate.hasLanes && s.metadata.lanes) {
        this.laneCount = s.metadata.totalLanes || s.metadata.lanes.length;
        this.lanes = s.metadata.lanes.map((l: any) => ({
          n: l.laneNumber, v: l.speed, t: l.vehicleTypes ? [...l.vehicleTypes] : []
        }));
      } else {
        this.laneCount = 1;
        this.lanes = [{ n: 1, v: s.metadata.value || null, t: [] }];
      }
      this.dp127HasVehicles = this.selectedTemplate.hasVehicles === 'optional' && s.metadata.lanes?.some((l: any) => l.vehicleTypes?.length > 0);
    } else {
      this.initForm();
    }
    this.activeLaneIndex = 0;
    this.cdr.detectChanges();
  }

  private updateVisuals() {
    if (!this.map) return;
    if (this.selectionMarker) this.map.removeLayer(this.selectionMarker);
    if (this.clickedLatLng) {
      this.selectionMarker = L.circleMarker([this.clickedLatLng.lat, this.clickedLatLng.lng], {
        radius: 9, fillColor: this.showDeletedMode ? '#94a3b8' : '#ef4444', color: '#fff', weight: 3, fillOpacity: 1, pane: 'markerPane'
      }).addTo(this.map);
    }
  }

  toggleSpeed(lane: any, speed: number) {
    lane.v = (lane.v === speed) ? null : speed;
    this.cdr.detectChanges();
  }

  toggleVehicle(lane: any, vehicleId: string) {
    if (!lane.t) lane.t = [];
    const index = lane.t.indexOf(vehicleId);
    if (index > -1) {
      lane.t.splice(index, 1);
    } else {
      lane.t.push(vehicleId);
    }
    this.cdr.detectChanges();
  }

  clearAllVisuals() {
    if (!this.map) return;
    if (this.selectionMarker) this.map.removeLayer(this.selectionMarker);
  }

  closeSidebar() {
    this.zone.run(() => {
      this.sidebarState = 'HIDDEN';
      this.selectedSignId = this.selectedSignData = this.selectedSignDetails = null;
      this.clearAllVisuals();
      this.resetWizard();
      this.cdr.detectChanges();
    });
  }

  startWizard() {
    this.sidebarState = 'WIZARD';
    this.wizardStep = 'MAIN_CATEGORY';
  }

  exitWizard() {
    if (this.selectedSignId) {
      this.sidebarState = 'SIGN_INFO';
    } else {
      this.closeSidebar();
    }
    this.cdr.detectChanges();
  }

  selectMainCategory(c: string) {
    this.selectedMainCategory = c;
    this.wizardStep = 'SUB_CATEGORY';
  }

  selectSubCategory(c: string) {
    this.selectedSubCategory = c;
    this.wizardStep = 'TYPE';
  }

  get filteredTemplates() {
    return this.signTemplates.filter(t => t.subcategory === this.selectedSubCategory);
  }

  selectTemplate(t: SignTemplate) {
    this.selectedTemplate = t;
    this.initForm();
    this.wizardStep = 'FORM';
  }

  initForm() {
    this.laneCount = 1;
    this.activeLaneIndex = 0;
    this.startTime = '22:00';
    this.endTime = '05:00';
    this.dp127HasVehicles = false;
    this.lanes = [];
    this.updateLanesArray(1);
  }

  updateLanesArray(count: number) {
    this.laneCount = count;
    if (count > this.lanes.length) {
      const startCount = this.lanes.length;
      for (let i = 0; i < count - startCount; i++) {
        this.lanes.push({ n: this.lanes.length + 1, v: null, t: [] });
      }
    } else if (count < this.lanes.length) {
      this.lanes.splice(count);
    }
    if (this.activeLaneIndex >= count) {
      this.activeLaneIndex = count - 1;
    }
    this.cdr.detectChanges();
  }

  isVehicleRequired(): boolean {
    if (this.selectedTemplate?.hasVehicles === true) return true;
    if (this.selectedTemplate?.hasVehicles === 'optional' && this.dp127HasVehicles) return true;
    return false;
  }

  get isFormValid(): boolean {
    if (!this.selectedTemplate) return false;
    if (!this.selectedTemplate.hasLanes) {
      return this.lanes.length > 0 && this.lanes[0].v !== null;
    }
    return this.lanes.every(lane => {
      const hasSpeed = lane.v !== null;
      const hasVehicle = this.isVehicleRequired() ? (lane.t && lane.t.length > 0) : true;
      return hasSpeed && hasVehicle;
    });
  }

  goToReview() {
    if (this.isFormValid) this.wizardStep = 'REVIEW';
  }

  saveSign() {
    if (this.isSaving || !this.selectedTemplate || !this.clickedLatLng || !this.selectedAccountId) return;
    this.isSaving = true;
    this.cdr.detectChanges();

    const metadata: any = {};
    if (this.selectedTemplate.hasTime) {
      metadata.startTime = this.startTime;
      metadata.endTime = this.endTime;
    }

    if (this.selectedTemplate.hasLanes) {
      metadata.totalLanes = this.laneCount;
      metadata.lanes = this.lanes.map(l => {
        const laneData: any = { laneNumber: l.n, speed: l.v };
        if (this.isVehicleRequired()) laneData.vehicleTypes = l.t;
        return laneData;
      });
    } else {
      metadata.value = this.lanes[0]?.v;
    }

    const payload = {
      id: this.selectedSignId, code: this.selectedTemplate.code, name: this.selectedTemplate.name,
      latitude: this.clickedLatLng.lat, longitude: this.clickedLatLng.lng,
      accountId: this.selectedAccountId, metadata: metadata
    };

    const obs = this.selectedSignId ? this.osmService.updateTrafficSign(this.selectedSignId, payload) : this.osmService.createTrafficSign(payload);
    obs.subscribe({
      next: () => {
        this.zone.run(() => {
          this.isSaving = false;
          this.loadTrafficSigns();
          this.closeSidebar();
          this.cdr.detectChanges();
        });
      },
      error: (e: any) => {
        this.isSaving = false;
        this.cdr.detectChanges();
      }
    });
  }

  deleteSign() { this.confirmActionType = 'DELETE'; this.showConfirmModal = true; }
  reactivateSign() { this.confirmActionType = 'REACTIVATE'; this.showConfirmModal = true; }
  closeConfirmModal() { this.showConfirmModal = false; this.confirmActionType = null; }

  executeConfirmAction() {
    if (!this.selectedSignId || !this.confirmActionType) return;
    const obs = this.confirmActionType === 'DELETE' ? this.osmService.deleteTrafficSign(this.selectedSignId) : this.osmService.reactivateTrafficSign(this.selectedSignId);
    obs.subscribe({
      next: () => {
        this.zone.run(() => {
          this.loadTrafficSigns();
          this.closeSidebar();
          this.closeConfirmModal();
          this.cdr.detectChanges();
        });
      },
      error: (err: any) => { this.closeConfirmModal(); }
    });
  }

  openAccountUserManager() {
    this.isAccountUserModalOpen = true;
    this.loadAccountUsers();
  }

  closeAccountUserModal() {
    this.isAccountUserModalOpen = false;
    this.inviteEmail = '';
    this.inviteRole = 'Member';
    this.memberListSearchTerm = '';
    this.memberListFilter = 'all';
  }

  loadAccountUsers() {
    if (!this.selectedAccountId) return;
    this.loadingUsers = true;
    this.http.get<any[]>(`${this.API_URL}/accounts/${this.selectedAccountId}/users`).subscribe({
      next: (res: any[]) => {
        this.accountUsers = res || [];
        this.loadingUsers = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.loadingUsers = false;
        this.cdr.detectChanges();
      }
    });
  }

  selectInviteRole(role: string) {
    this.inviteRole = role;
    this.isInviteRoleDropdownOpen = false;
    this.cdr.detectChanges();
  }

  sendInvitation() {
    if (!this.inviteEmail || this.isSubmittingInvite) return;
    this.isSubmittingInvite = true;
    const body = { email: this.inviteEmail, role: this.inviteRole, accountId: this.selectedAccountId };
    this.http.post(`${this.API_URL}/accounts/invite-user`, body).subscribe({
      next: () => {
        this.isSubmittingInvite = false;
        this.inviteEmail = '';
        this.loadAccountUsers();
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.isSubmittingInvite = false;
        this.cdr.detectChanges();
      }
    });
  }

  selectListFilter(role: 'all' | 'Owner' | 'Member' | 'Viewer') {
    this.memberListFilter = role;
    this.isListRoleFilterOpen = false;
    this.cdr.detectChanges();
  }

  get filteredAccountUsers() {
    if (!this.accountUsers) return [];
    return this.accountUsers.filter(u => {
      const term = this.memberListSearchTerm.toLowerCase();
      const username = (u.username || '').toLowerCase();
      const email = (u.email || '').toLowerCase();
      const matchesSearch = username.includes(term) || email.includes(term);

      const userRole = (u.role || 'Viewer').toLowerCase();
      const filter = this.memberListFilter.toLowerCase();
      const matchesFilter = (filter === 'all' || userRole === filter);

      return matchesSearch && matchesFilter;
    });
  }

  openRoleModal(type: string, account: any, user: any) {
    this.roleTargetUser = user;
    this.selectedRole = user.role || 'Viewer';
    this.isRoleModalOpen = true;
    this.isRoleDropdownOpen = false;
    this.cdr.detectChanges();
  }

  closeRoleModal() {
    this.isRoleModalOpen = false;
    this.roleTargetUser = null;
    this.isRoleDropdownOpen = false;
    this.cdr.detectChanges();
  }

  toggleRoleDropdown() {
    this.isRoleDropdownOpen = !this.isRoleDropdownOpen;
  }

  selectRole(role: string) {
    this.selectedRole = role;
    this.isRoleDropdownOpen = false;
    this.cdr.detectChanges();
  }

  runUpdateUserRole() {
    if (!this.roleTargetUser || this.isSubmitting) return;
    this.isSubmitting = true;
    const userId = this.roleTargetUser.userId || this.roleTargetUser.id;
    const body = { role: this.selectedRole };
    
    this.http.put(`${this.API_URL}/accounts/${this.selectedAccountId}/users/${userId}/role`, body).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.closeRoleModal();
        this.loadAccountUsers();
      },
      error: (err: any) => {
        this.isSubmitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  openWithdrawModal(user: any) {
    this.withdrawTargetUser = user;
    this.isWithdrawModalOpen = true;
    this.cdr.detectChanges();
  }

  closeWithdrawModal() {
    this.isWithdrawModalOpen = false;
    this.withdrawTargetUser = null;
    this.cdr.detectChanges();
  }

  executeWithdraw() {
    if (!this.withdrawTargetUser || this.isSubmitting) return;
    this.isSubmitting = true;
    const userId = this.withdrawTargetUser.userId || this.withdrawTargetUser.id;
    
    this.http.delete(`${this.API_URL}/accounts/${this.selectedAccountId}/users/${userId}`).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.closeWithdrawModal();
        this.loadAccountUsers();
      },
      error: (err: any) => {
        this.isSubmitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  highlight(text: string, term: string): string {
    if (!term || !text) return text;
    const sanitizedTerm = term.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&');
    const re = new RegExp(`(${sanitizedTerm})`, 'gi');
    return text.replace(re, '<b class="text-blue-600 font-black">$1</b>');
  }

  resetWizard() {
    this.wizardStep = 'MAIN_CATEGORY';
    this.selectedTemplate = null;
    this.lanes = [];
    this.laneCount = 1;
    this.activeLaneIndex = 0;
  }

  logout() {
    this.isUserDropdownOpen = false;
    this.authService.logout();
  }
} 
