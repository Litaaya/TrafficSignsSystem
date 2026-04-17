import { Component, OnInit, inject, ChangeDetectorRef, ViewChild, ElementRef } from '@angular/core';
import { HttpClient, HttpParams, HttpHeaders } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth-service';
import { MatMenuTrigger } from '@angular/material/menu';
import { Subject, debounceTime, switchMap, map, distinctUntilChanged, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

@Component({
  selector: 'app-user-list',
  standalone: false,
  templateUrl: './user-list.html'
})
export class UserListComponent implements OnInit {
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private authService = inject(AuthService);
  private userSearchSubject = new Subject<string>();
  private checkSubject = new Subject<{ field: string, value: string }>();
  private accountSearchSubject = new Subject<string>();

  users: any[] = [];
  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  pageSizeOptions = [5, 10, 20, 50];
  jumpPageInput: number | null = null;
  userSearchTerm = '';
  userStatusFilter: 'all' | 'active' | 'inactive' = 'all';
  showModal = false;
  showDetailsModal = false;
  isSubmitting = false;
  selectedUser: any = null;
  selectedRole: string = 'Viewer';
  isRoleDropdownOpen = false;
  activeTab: 'info' | 'accounts' = 'info';
  userAccounts: any[] = [];
  availableAccounts: any[] = [];
  loadingAccounts = false;
  loadingAvailable = false;
  searchAccountTerm = '';
  accountListSearchTerm = '';
  accountListFilter: 'all' | 'owner' = 'all';
  newUser = { username: '', password: '', email: '', phone: '', firstName: '', lastName: '' };
  formIsValid = false;

  fieldStatus: any = {
    username: { checking: false, valid: false, error: '' },
    email: { checking: false, valid: false, error: '' },
    phone: { checking: false, valid: false, error: '' }
  };

  isStatusDropdownOpen = false;
  isRoleModalOpen = false;
  roleActionType: 'ASSIGN' | 'UPDATE' = 'ASSIGN';
  roleTargetAccount: any = null;
  isReactivateModalOpen = false;
  reactivateTargetUser: any = null;
  newReactivatePassword = '';

  @ViewChild('assignTrigger', { read: MatMenuTrigger }) assignTrigger!: MatMenuTrigger;
  @ViewChild('jumpTrigger', { read: MatMenuTrigger }) jumpTrigger!: MatMenuTrigger;
  @ViewChild('tableContainer') tableContainer!: ElementRef;

  ngOnInit(): void {
    this.fetchData();
    this.initRealtimeValidation();
    this.initAccountSearch();

    this.userSearchSubject.pipe(debounceTime(400), distinctUntilChanged()).subscribe(term => {
      this.userSearchTerm = term;
      this.pageNumber = 1;
      this.fetchData();
    });
  }

  selectUserStatus(status: 'all' | 'active' | 'inactive') {
    this.userStatusFilter = status;
    this.isStatusDropdownOpen = false;
    this.pageNumber = 1;
    this.fetchData();
  }

  onUserSearch(term: string) {
    this.pageNumber = 1;
    this.userSearchSubject.next(term);
  }

  initRealtimeValidation() {
    this.checkSubject.pipe(
      debounceTime(600),
      switchMap(data => {
        this.fieldStatus[data.field].checking = true;
        this.updateFormValidity();
        this.cdr.detectChanges();

        let queryParams: any = { field: data.field, value: data.value };
        if (this.selectedUser?.id) queryParams.excludeId = this.selectedUser.id;

        return this.http.get<any>(`https://localhost:7272/api/users/validate-field`, { params: queryParams })
          .pipe(
            map((res: any) => ({ ...res, field: data.field })),
            catchError(() => {
              return of({ isValid: false, message: 'Validation service error', field: data.field, hasError: true });
            })
          );
      })
    ).subscribe({
      next: (res: any) => {
        this.fieldStatus[res.field].checking = false;
        if (res.hasError) {
          this.fieldStatus[res.field].valid = false;
          this.fieldStatus[res.field].error = res.message;
        } else {
          this.fieldStatus[res.field].valid = res.isValid;
          this.fieldStatus[res.field].error = res.isValid ? '' : res.message;
        }
        this.updateFormValidity();
        this.cdr.detectChanges();
      }
    });
  }

  initAccountSearch() {
    this.accountSearchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => {
        this.loadingAvailable = true;
        this.cdr.detectChanges();
        const params = new HttpParams().set('Page', '1').set('PageSize', '20').set('SearchTerm', term);
        return this.http.get<any>('https://localhost:7272/api/accounts', { params });
      })
    ).subscribe({
      next: (res) => {
        this.availableAccounts = res.items || [];
        this.loadingAvailable = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loadingAvailable = false;
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

  onFieldChange(field: string, value: string) {
    if (!value || value.trim().length < 2) {
      this.fieldStatus[field] = { checking: false, valid: false, error: '' };
      this.updateFormValidity();
      return;
    }
    this.fieldStatus[field].checking = true;
    this.fieldStatus[field].valid = false;
    this.fieldStatus[field].error = '';
    this.updateFormValidity();
    this.checkSubject.next({ field, value });
  }

  updateFormValidity() {
    const fields = this.selectedUser ? ['email', 'phone'] : ['username', 'email', 'phone'];
    const isAllValid = fields.every(f => this.fieldStatus[f].valid);
    const isChecking = fields.some(f => this.fieldStatus[f].checking);
    const hasRequired = this.selectedUser
      ? (!!this.newUser.email && !!this.newUser.phone)
      : (!!this.newUser.username && !!this.newUser.password && !!this.newUser.email && !!this.newUser.phone);
    const hasChange = this.selectedUser ? this.isDataChanged() : true;

    this.formIsValid = isAllValid && !isChecking && hasRequired && hasChange && !this.isSubmitting;
  }

  isDataChanged(): boolean {
    if (!this.selectedUser) return true;
    return (this.newUser.email.trim() !== (this.selectedUser.email || '') ||
      this.newUser.phone.trim() !== (this.selectedUser.phone || '') ||
      (this.newUser.firstName?.trim() || '') !== (this.selectedUser.firstName || '') ||
      (this.newUser.lastName?.trim() || '') !== (this.selectedUser.lastName || ''));
  }

  deleteUser(user: any) {
    if (confirm(`Are you sure you want to delete user: ${user.username}?`)) {
      this.isSubmitting = true;
      this.http.delete(`https://localhost:7272/api/users/${user.id}`).subscribe({
        next: () => this.onSuccessCleanup(),
        error: () => { this.isSubmitting = false; this.updateFormValidity(); }
      });
    }
  }

  removeAccount(user: any, acc: any) {
    if (confirm(`Remove user ${user.username} from workspace ${acc.accountName || acc.name}?`)) {
      this.isSubmitting = true;
      const accId = acc.accountId || acc.id;
      this.http.delete(`https://localhost:7272/api/accounts/${accId}/users/${user.id}`).subscribe({
        next: () => {
          this.fetchUserAccounts(user.id);
          this.isSubmitting = false;
          this.updateFormValidity();
        },
        error: () => { this.isSubmitting = false; this.updateFormValidity(); }
      });
    }
  }

  openRoleModal(type: 'ASSIGN' | 'UPDATE', user: any, acc: any) {
    this.roleActionType = type;
    this.selectedUser = user;
    this.roleTargetAccount = acc;
    this.selectedRole = type === 'UPDATE' ? (acc.role || acc.Role || 'Viewer') : 'Viewer';
    this.isRoleModalOpen = true;
    if (type === 'ASSIGN' && this.assignTrigger) this.assignTrigger.closeMenu();
    this.cdr.detectChanges();
  }

  closeRoleModal() {
    this.isRoleModalOpen = false;
    this.roleTargetAccount = null;
    this.isRoleDropdownOpen = false;
  }

  toggleRoleDropdown() {
    this.isRoleDropdownOpen = !this.isRoleDropdownOpen;
  }

  selectRole(role: string) {
    this.selectedRole = role;
    this.isRoleDropdownOpen = false;
    this.cdr.detectChanges();
  }

  runAssignToAccount() {
    this.isSubmitting = true;
    const body = { accountId: this.roleTargetAccount.id, userId: this.selectedUser.id, role: this.selectedRole };
    this.http.post('https://localhost:7272/api/accounts/assign-user', body).subscribe({
      next: () => {
        this.fetchUserAccounts(this.selectedUser.id);
        this.closeRoleModal();
        this.isSubmitting = false;
      },
      error: () => this.isSubmitting = false
    });
  }

  runUpdateUserRole() {
    this.isSubmitting = true;
    const body = { role: this.selectedRole };
    const accId = this.roleTargetAccount.accountId || this.roleTargetAccount.id;
    this.http.put(`https://localhost:7272/api/accounts/${accId}/users/${this.selectedUser.id}/role`, body).subscribe({
      next: () => {
        this.fetchUserAccounts(this.selectedUser.id);
        this.closeRoleModal();
        this.isSubmitting = false;
      },
      error: () => this.isSubmitting = false
    });
  }

  openReactivateModal(user: any) {
    this.reactivateTargetUser = user;
    this.newReactivatePassword = '';
    this.isReactivateModalOpen = true;
  }

  closeReactivateModal() {
    this.isReactivateModalOpen = false;
    this.reactivateTargetUser = null;
    this.newReactivatePassword = '';
  }

  runReactivateUser() {
    this.isSubmitting = true;
    this.http.patch(`https://localhost:7272/api/users/${this.reactivateTargetUser.id}/reactivate`, { newPassword: this.newReactivatePassword }).subscribe({
      next: () => {
        this.closeReactivateModal();
        this.onSuccessCleanup();
      },
      error: () => this.isSubmitting = false
    });
  }

  runCreateUser() {
    this.isSubmitting = true;
    this.updateFormValidity();
    const headers = new HttpHeaders({ 'X-Actor-Id': this.authService.getUserId() || '' });
    this.http.post('https://localhost:7272/api/users', this.newUser, { headers }).subscribe({
      next: () => this.onSuccessCleanup(),
      error: () => { this.isSubmitting = false; this.updateFormValidity(); this.cdr.detectChanges(); }
    });
  }

  runUpdateUser() {
    this.isSubmitting = true;
    this.updateFormValidity();
    const payload = { ...this.newUser, id: this.selectedUser.id };
    this.http.put(`https://localhost:7272/api/users/${this.selectedUser.id}`, payload).subscribe({
      next: () => this.onSuccessCleanup(),
      error: () => { this.isSubmitting = false; this.updateFormValidity(); this.cdr.detectChanges(); }
    });
  }

  private onSuccessCleanup() {
    this.fetchData();
    this.closeModal();
    this.isSubmitting = false;
    this.selectedUser = null;
    this.updateFormValidity();
    this.cdr.markForCheck();
    this.cdr.detectChanges();
  }

  get filteredUserAccounts() {
    return this.userAccounts.filter(acc => {
      const matchesSearch = acc.accountName.toLowerCase().includes(this.accountListSearchTerm.toLowerCase());
      const currentRole = acc.role || acc.Role || 'Viewer';
      const matchesFilter = this.accountListFilter === 'all' || (this.accountListFilter === 'owner' && currentRole === 'Owner');
      return matchesSearch && matchesFilter;
    });
  }

  fetchData(): void {
    const params = new HttpParams()
      .set('pageNumber', this.pageNumber.toString())
      .set('pageSize', this.pageSize.toString())
      .set('searchTerm', this.userSearchTerm.trim())
      .set('statusFilter', this.userStatusFilter);

    this.http.get<any>('https://localhost:7272/api/users', { params }).subscribe({
      next: (res) => {
        this.users = res.items || [];
        this.totalCount = res.totalCount || 0;
        this.cdr.detectChanges();
      }
    });
  }

  fetchUserAccounts(userId: string) {
    this.loadingAccounts = true;
    this.http.get<any[]>(`https://localhost:7272/api/users/${userId}/accounts`).subscribe({
      next: (res) => {
        this.userAccounts = res || [];
        this.loadingAccounts = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loadingAccounts = false;
        this.cdr.detectChanges();
      }
    });
  }

  fetchAvailableAccounts(term: string = '') {
    this.loadingAvailable = true;
    const params = new HttpParams().set('Page', '1').set('PageSize', '20').set('SearchTerm', term);
    this.http.get<any>('https://localhost:7272/api/accounts', { params }).subscribe({
      next: (res) => {
        this.availableAccounts = res.items || [];
        this.loadingAvailable = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loadingAvailable = false;
        this.cdr.detectChanges();
      }
    });
  }

  onPageChange(p: number | string): void {
    if (typeof p === 'number') {
      this.pageNumber = p;
      this.fetchData();
      this.scrollToTop();
    }
  }

  onPageSizeChange(): void {
    this.pageNumber = 1;
    this.fetchData();
    this.scrollToTop();
  }

  private scrollToTop() {
    if (this.tableContainer) {
      this.tableContainer.nativeElement.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  setTab(tab: 'info' | 'accounts') {
    this.activeTab = tab;
    if (tab === 'accounts' && this.selectedUser) {
      this.fetchUserAccounts(this.selectedUser.id);
    }
  }

  openDetailsModal(user: any) {
    this.selectedUser = user;
    this.activeTab = 'info';
    this.showDetailsModal = true;
    this.fetchUserAccounts(user.id);
    this.cdr.detectChanges();
  }

  openModal(): void {
    this.selectedUser = null;
    this.resetForm();
    this.showModal = true;
    this.fieldStatus = {
      username: { checking: false, valid: false, error: '' },
      email: { checking: false, valid: false, error: '' },
      phone: { checking: false, valid: false, error: '' }
    };
    this.updateFormValidity();
    this.cdr.detectChanges();
  }

  openEditModal(user: any) {
    this.selectedUser = user;
    this.newUser = {
      username: user.username,
      password: '',
      email: user.email,
      phone: user.phone,
      firstName: user.firstName,
      lastName: user.lastName
    };
    this.fieldStatus = { username: { valid: true }, email: { valid: true }, phone: { valid: true } };
    this.showModal = true;
    this.updateFormValidity();
    this.cdr.detectChanges();
  }

  onAccountSearch(term: string) {
    this.accountSearchSubject.next(term);
  }

  openAssignSection() {
    this.searchAccountTerm = '';
    this.fetchAvailableAccounts();
  }

  closeModal(): void {
    this.showModal = false;
    this.resetForm();
  }

  resetForm(): void {
    this.newUser = { username: '', password: '', email: '', phone: '', firstName: '', lastName: '' };
    this.updateFormValidity();
  }

  jumpToPage(): void {
    if (this.jumpPageInput !== null && this.jumpPageInput >= 1 && this.jumpPageInput <= this.totalPages) {
      this.pageNumber = this.jumpPageInput;
      this.fetchData();
      this.jumpPageInput = null;
      if (this.jumpTrigger) this.jumpTrigger.closeMenu();
      this.scrollToTop();
    }
  }

  get totalPages(): number {
    return Math.ceil(this.totalCount / this.pageSize);
  }

  get visiblePages(): (number | string)[] {
    const total = this.totalPages;
    const current = this.pageNumber;
    const pages: (number | string)[] = [];
    if (total <= 3) {
      for (let i = 1; i <= total; i++) pages.push(i);
      return pages;
    }
    pages.push(1);
    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);
    if (start > 2) pages.push('...');
    for (let i = start; i <= end; i++) pages.push(i);
    if (end < total - 1) pages.push('...');
    if (total > 1) pages.push(total);
    return pages;
  }
}
