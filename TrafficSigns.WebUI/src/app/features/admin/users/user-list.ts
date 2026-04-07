import { Component, OnInit, inject, ChangeDetectorRef, ViewChild, ElementRef } from '@angular/core';
import { HttpClient, HttpParams, HttpHeaders } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';
import { MatMenuTrigger } from '@angular/material/menu';
import { Subject, debounceTime, switchMap, map, distinctUntilChanged } from 'rxjs';

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
  errorMessage = '';
  isConfirming = false;
  confirmPassword = '';
  newReactivatePassword = '';
  pendingAction: 'CREATE' | 'UPDATE' | 'DELETE' | 'REACTIVATE' | 'ASSIGN_ACC' | 'UPDATE_ACC_ROLE' | 'REMOVE_ACC' | null = null;
  selectedUser: any = null;
  confirmErrorMessage = '';
  selectedRole: string = 'Viewer';
  isRoleDropdownOpen = false;
  activeTab: 'info' | 'accounts' = 'info';
  userAccounts: any[] = [];
  availableAccounts: any[] = [];
  loadingAccounts = false;
  loadingAvailable = false;
  searchAccountTerm = '';
  pendingAccTarget: any = null;
  accountListSearchTerm = '';
  accountListFilter: 'all' | 'owner' = 'all';
  newUser = { username: '', password: '', email: '', phone: '', firstName: '', lastName: '' };
  fieldStatus: any = {
    username: { checking: false, valid: false, error: '' },
    email: { checking: false, valid: false, error: '' },
    phone: { checking: false, valid: false, error: '' }
  };
  isStatusDropdownOpen = false;

  selectUserStatus(status: 'all' | 'active' | 'inactive') {
    this.userStatusFilter = status;
    this.isStatusDropdownOpen = false;
    this.pageNumber = 1;
    this.fetchData();
  }
  
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

  onUserSearch(term: string) {
    this.pageNumber = 1;
    this.userSearchSubject.next(term);
  }

  initRealtimeValidation() {
    this.checkSubject.pipe(
      debounceTime(600),
      switchMap(data => {
        this.fieldStatus[data.field].checking = true;
        this.cdr.detectChanges();
        let queryParams: any = { field: data.field, value: data.value };
        if (this.selectedUser?.id) queryParams.excludeId = this.selectedUser.id;
        return this.http.get<any>(`https://localhost:7272/api/users/check-duplicate`, { params: queryParams })
          .pipe(map(res => ({ ...res, field: data.field })));
      })
    ).subscribe({
      next: (res) => {
        this.fieldStatus[res.field].checking = false;
        this.fieldStatus[res.field].valid = !res.isDuplicate;
        this.fieldStatus[res.field].error = res.isDuplicate ? res.message : '';
        this.cdr.detectChanges();
      },
      error: () => {
        this.fieldStatus.username.checking = false;
        this.fieldStatus.email.checking = false;
        this.fieldStatus.phone.checking = false;
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
    this.onInputChange();
    if (!value || value.trim().length < 2) {
      this.fieldStatus[field] = { checking: false, valid: false, error: '' };
      return;
    }
    this.fieldStatus[field].checking = true;
    this.fieldStatus[field].valid = false;
    this.fieldStatus[field].error = '';
    this.checkSubject.next({ field, value });
  }

  onInputChange() {
    if (this.errorMessage.includes('nothing difference')) this.errorMessage = '';
  }

  canSubmit(): boolean {
    if (this.isSubmitting) return false;
    const fields = this.selectedUser ? ['email', 'phone'] : ['username', 'email', 'phone'];
    const isAllValid = fields.every(f => this.fieldStatus[f].valid);
    const isChecking = fields.some(f => this.fieldStatus[f].checking);
    const hasRequired = this.selectedUser ? (!!this.newUser.email && !!this.newUser.phone) : (!!this.newUser.username && !!this.newUser.password && !!this.newUser.email && !!this.newUser.phone);
    const hasChange = this.selectedUser ? this.isDataChanged() : true;
    return isAllValid && !isChecking && hasRequired && hasChange;
  }

  isDataChanged(): boolean {
    if (!this.selectedUser) return true;
    return (this.newUser.email.trim() !== (this.selectedUser.email || '') ||
      this.newUser.phone.trim() !== (this.selectedUser.phone || '') ||
      (this.newUser.firstName?.trim() || '') !== (this.selectedUser.firstName || '') ||
      (this.newUser.lastName?.trim() || '') !== (this.selectedUser.lastName || ''));
  }

  onConfirmUpdate() {
    if (!this.isDataChanged()) {
      this.errorMessage = "nothing difference, don't need update process.";
      return;
    }
    this.errorMessage = '';
    this.openConfirmModal('UPDATE', this.selectedUser);
  }

  openConfirmModal(action: any, data?: any, targetAcc?: any) {
    this.pendingAction = action;
    if (data) this.selectedUser = data;
    if (targetAcc) this.pendingAccTarget = targetAcc;
    if (action === 'ASSIGN_ACC' && this.assignTrigger) this.assignTrigger.closeMenu();

    if (action === 'ASSIGN_ACC') {
      this.selectedRole = 'Viewer';
    } else if (action === 'UPDATE_ACC_ROLE') {
      this.selectedRole = targetAcc?.role || (targetAcc?.isOwner ? 'Owner' : 'Viewer');
    }

    this.confirmPassword = '';
    this.newReactivatePassword = '';
    this.confirmErrorMessage = '';
    this.isConfirming = true;
    this.cdr.detectChanges();
  }

  handleActionConfirm() {
    if (!this.confirmPassword) {
      this.confirmErrorMessage = "Input Admin Password!";
      this.cdr.detectChanges();
      return;
    }
    this.isSubmitting = true;
    this.cdr.detectChanges();
    this.http.post('https://localhost:7272/api/auth/verify-password', { password: this.confirmPassword }).subscribe({
      next: () => this.executePendingAction(),
      error: (err) => {
        this.isSubmitting = false;
        this.confirmErrorMessage = err.error?.message || "Wrong Password!";
        this.cdr.detectChanges();
      }
    });
  }

  toggleRoleDropdown() {
    this.isRoleDropdownOpen = !this.isRoleDropdownOpen;
  }

  selectRole(role: string) {
    this.selectedRole = role;
    this.isRoleDropdownOpen = false;
    this.cdr.detectChanges();
  }

  get filteredUserAccounts() {
    return this.userAccounts.filter(acc => {
      const matchesSearch = acc.accountName.toLowerCase().includes(this.accountListSearchTerm.toLowerCase());
      const matchesFilter = this.accountListFilter === 'all' || (this.accountListFilter === 'owner' && acc.isOwner);
      return matchesSearch && matchesFilter;
    });
  }

  private executePendingAction() {
    switch (this.pendingAction) {
      case 'CREATE': this.runCreateUser(); break;
      case 'UPDATE': this.runUpdateUser(); break;
      case 'DELETE': this.runDeleteUser(); break;
      case 'REACTIVATE': this.runReactivateUser(); break;
      case 'ASSIGN_ACC': this.runAssignToAccount(); break;
      case 'UPDATE_ACC_ROLE': this.runUpdateUserRole(); break;
      case 'REMOVE_ACC': this.runRemoveUserFromAccount(); break;
    }
  }

  private runAssignToAccount() {
    const body = { accountId: this.pendingAccTarget.id, userId: this.selectedUser.id, role: this.selectedRole };
    this.http.post('https://localhost:7272/api/accounts/assign-user', body).subscribe({
      next: () => this.onAccSuccess(),
      error: (err) => this.onAccError(err)
    });
  }

  private runUpdateUserRole() {
    const body = { role: this.selectedRole };
    const accId = this.pendingAccTarget.accountId || this.pendingAccTarget.id;
    this.http.put(`https://localhost:7272/api/accounts/${accId}/users/${this.selectedUser.id}/role`, body).subscribe({
      next: () => {
        this.pendingAccTarget.role = this.selectedRole;
        this.onAccSuccess();
      },
      error: (err) => this.onAccError(err)
    });
  }

  private runRemoveUserFromAccount() {
    const accId = this.pendingAccTarget.accountId || this.pendingAccTarget.id;
    this.http.delete(`https://localhost:7272/api/accounts/${accId}/users/${this.selectedUser.id}`).subscribe({
      next: () => this.onAccSuccess(),
      error: (err) => this.onAccError(err)
    });
  }

  private onAccSuccess() {
    this.fetchUserAccounts(this.selectedUser.id);
    this.isConfirming = false;
    this.isSubmitting = false;
    this.pendingAccTarget = null;
    this.confirmPassword = '';
    if (this.assignTrigger) {
      this.assignTrigger.closeMenu();
    }
    this.cdr.markForCheck();
    this.cdr.detectChanges();
  }

  private onAccError(err: any) {
    this.isSubmitting = false;
    this.confirmErrorMessage = err.error?.message || "Action fail";
    this.cdr.detectChanges();
  }

  private runCreateUser() {
    const headers = new HttpHeaders({ 'X-Actor-Id': this.authService.getUserId() || '' });
    this.http.post('https://localhost:7272/api/users', this.newUser, { headers }).subscribe({
      next: () => this.onSuccessCleanup(),
      error: (err) => this.onErrorCleanup(err.error?.message)
    });
  }

  private runUpdateUser() {
    const payload = { ...this.newUser, id: this.selectedUser.id };
    this.http.put(`https://localhost:7272/api/users/${this.selectedUser.id}`, payload).subscribe({
      next: () => this.onSuccessCleanup(),
      error: (err) => this.onErrorCleanup(err.error?.message)
    });
  }

  private runDeleteUser() {
    this.http.delete(`https://localhost:7272/api/users/${this.selectedUser.id}`).subscribe({
      next: () => this.onSuccessCleanup(),
      error: (err) => this.onErrorCleanup(err.error?.message)
    });
  }

  private runReactivateUser() {
    this.http.patch(`https://localhost:7272/api/users/${this.selectedUser.id}/reactivate`, { newPassword: this.newReactivatePassword }).subscribe({
      next: () => this.onSuccessCleanup(),
      error: (err) => this.onErrorCleanup(err.error?.message)
    });
  }

  private onSuccessCleanup() {
    this.fetchData();
    this.closeModal();
    this.isConfirming = false;
    this.isSubmitting = false;
    this.selectedUser = null;
    this.cdr.markForCheck();
    this.cdr.detectChanges();
  }

  private onErrorCleanup(msg: string) {
    this.isSubmitting = false;
    this.isConfirming = false;
    if (this.showModal) this.errorMessage = msg;
    else this.confirmErrorMessage = msg;
    this.cdr.detectChanges();
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
    this.errorMessage = '';
    this.fieldStatus = { username: { valid: false }, email: { valid: false }, phone: { valid: false } };
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
    this.errorMessage = '';
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
