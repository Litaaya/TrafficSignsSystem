import { Component, OnInit, inject, ChangeDetectorRef, ViewChild, ElementRef } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth-service';
import { MatMenuTrigger } from '@angular/material/menu';
import { Subject, debounceTime, switchMap, map, distinctUntilChanged, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

@Component({
  selector: 'app-account-list',
  standalone: false,
  templateUrl: './account-list.html'
})
export class AccountListComponent implements OnInit {
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private authService = inject(AuthService);
  private accountSearchSubject = new Subject<string>();
  private checkSubject = new Subject<{ field: string, value: string }>();
  private userSearchSubject = new Subject<string>();

  accounts: any[] = [];
  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  pageSizeOptions = [5, 10, 20, 50];
  jumpPageInput: number | null = null;
  accountSearchTerm = '';
  accountStatusFilter: 'all' | 'active' | 'inactive' = 'all';

  showModal = false;
  showDetailsModal = false;
  isSubmitting = false;
  selectedAccount: any = null;
  selectedRole: string = 'Viewer';
  isRoleDropdownOpen = false;
  activeTab: 'info' | 'users' = 'info';
  accountUsers: any[] = [];
  loadingUsers = false;

  availableUsers: any[] = [];
  loadingAvailable = false;
  searchUserTerm = '';
  availablePage = 1;
  hasMoreAvailable = true;

  memberListSearchTerm = '';
  memberListFilter: 'all' | 'Owner' | 'Member' | 'Viewer' = 'all';
  newAccount = { name: '', desc: '', email: '', phone: '', system: false };
  formIsValid = false;

  fieldStatus: any = {
    name: { checking: false, valid: false, error: '' },
    email: { checking: false, valid: true, error: '' },
    phone: { checking: false, valid: true, error: '' }
  };

  isStatusDropdownOpen = false;
  isRoleModalOpen = false;
  roleActionType: 'ASSIGN' | 'UPDATE' = 'ASSIGN';
  roleTargetUser: any = null;
  
  isPageSizeDropdownOpen = false;
  isUserRoleFilterOpen = false;

  isClosingModal = false;
  isClosingDetails = false;
  isClosingRole = false;
  isDeleteModalOpen = false;
  isClosingDelete = false;
  deleteTargetAccount: any = null;

  isRemoveMemberModalOpen = false;
  isClosingRemoveMember = false;
  removeTargetMember: any = null;

  @ViewChild('assignTrigger', { read: MatMenuTrigger }) assignTrigger!: MatMenuTrigger;
  @ViewChild('jumpTrigger', { read: MatMenuTrigger }) jumpTrigger!: MatMenuTrigger;
  @ViewChild('tableContainer') tableContainer!: ElementRef;

  ngOnInit(): void {
    this.fetchData();
    this.initRealtimeValidation();
    this.initUserSearch();

    this.accountSearchSubject.pipe(debounceTime(400), distinctUntilChanged()).subscribe(term => {
      this.accountSearchTerm = term;
      this.pageNumber = 1;
      this.fetchData();
    });
  }

  selectAccountStatus(status: 'all' | 'active' | 'inactive') {
    this.accountStatusFilter = status;
    this.isStatusDropdownOpen = false;
    this.pageNumber = 1;
    this.fetchData();
  }

  selectPageSize(size: number) {
    this.pageSize = size;
    this.isPageSizeDropdownOpen = false;
    this.onPageSizeChange();
  }

  onAccountSearch(term: string) {
    this.pageNumber = 1;
    this.accountSearchSubject.next(term);
  }

  initRealtimeValidation() {
    this.checkSubject.pipe(
      debounceTime(300),
      switchMap(data => {
        this.fieldStatus[data.field].checking = true;
        this.updateFormValidity();
        this.cdr.detectChanges();
        let queryParams: any = { field: data.field, value: data.value };
        if (this.selectedAccount?.id) queryParams.excludeId = this.selectedAccount.id;
        return this.http.get<any>(`https://localhost:7272/api/accounts/validate-field`, { params: queryParams })
          .pipe(
            map((res: any) => ({ ...res, field: data.field })),
            catchError(() => of({ isValid: false, message: 'Service error', field: data.field, hasError: true }))
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

  initUserSearch() {
    this.userSearchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe(term => {
      this.searchUserTerm = term;
      this.fetchAvailableUsers(true);
    });
  }

  fetchAvailableUsers(reset: boolean = false) {
    if (reset) {
      this.availablePage = 1;
      this.availableUsers = [];
      this.hasMoreAvailable = true;
    }
    if (this.loadingAvailable || !this.hasMoreAvailable) return;
    this.loadingAvailable = true;
    this.cdr.detectChanges();

    const params = new HttpParams()
      .set('pageNumber', this.availablePage.toString())
      .set('pageSize', '20')
      .set('searchTerm', this.searchUserTerm);

    this.http.get<any>('https://localhost:7272/api/users', { params }).subscribe({
      next: (res) => {
        const newItems = res.items || [];
        this.availableUsers = reset ? newItems : [...this.availableUsers, ...newItems];
        this.hasMoreAvailable = newItems.length === 20;
        this.loadingAvailable = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loadingAvailable = false;
        this.cdr.detectChanges();
      }
    });
  }

  onScrollAvailableUsers(event: any) {
    const element = event.target;
    if (element.scrollHeight - element.scrollTop <= element.clientHeight + 20) {
      if (!this.loadingAvailable && this.hasMoreAvailable) {
        this.availablePage++;
        this.fetchAvailableUsers(false);
      }
    }
  }

  highlight(text: string, term: string): string {
    if (!term || !text) return text;
    const sanitizedTerm = term.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&');
    const re = new RegExp(`(${sanitizedTerm})`, 'gi');
    return text.replace(re, '<b class="text-blue-600 font-black">$1</b>');
  }

  onFieldChange(field: string, value: string) {
    if (!value || value.trim().length < 2) {
      this.fieldStatus[field] = { checking: false, valid: field !== 'name', error: '' };
      this.updateFormValidity();
      this.cdr.detectChanges();
      return;
    }
    this.fieldStatus[field].checking = true;
    this.fieldStatus[field].valid = false;
    this.fieldStatus[field].error = '';
    this.updateFormValidity();
    this.cdr.detectChanges();
    this.checkSubject.next({ field, value });
  }

  updateFormValidity() {
    const isNameValid = this.fieldStatus.name.valid;
    const isEmailValid = this.fieldStatus.email.valid;
    const isPhoneValid = this.fieldStatus.phone.valid;
    const isChecking = this.fieldStatus.name.checking || this.fieldStatus.email.checking || this.fieldStatus.phone.checking;
    const hasRequired = !!this.newAccount.name.trim();
    const hasChange = this.selectedAccount ? this.isDataChanged() : true;
    this.formIsValid = isNameValid && isEmailValid && isPhoneValid && !isChecking && hasRequired && hasChange && !this.isSubmitting;
  }

  isDataChanged(): boolean {
    if (!this.selectedAccount) return true;
    return (
      this.newAccount.name.trim() !== (this.selectedAccount.name || '') ||
      (this.newAccount.desc?.trim() || '') !== (this.selectedAccount.desc || '') ||
      (this.newAccount.email?.trim() || '') !== (this.selectedAccount.email || '') ||
      (this.newAccount.phone?.trim() || '') !== (this.selectedAccount.phone || '') ||
      this.newAccount.system !== (this.selectedAccount.system || false)
    );
  }

  openRoleModal(type: 'ASSIGN' | 'UPDATE', acc: any, user: any) {
    this.roleActionType = type;
    this.selectedAccount = acc;
    this.roleTargetUser = user;
    this.selectedRole = type === 'UPDATE' ? (user.role || user.Role || 'Viewer') : 'Viewer';
    this.isRoleModalOpen = true;
    if (type === 'ASSIGN' && this.assignTrigger) this.assignTrigger.closeMenu();
    this.cdr.detectChanges();
  }

  closeRoleModal() {
    this.isClosingRole = true;
    this.cdr.detectChanges();
    setTimeout(() => {
      this.isRoleModalOpen = false;
      this.isClosingRole = false;
      this.roleTargetUser = null;
      this.isRoleDropdownOpen = false;
      this.cdr.detectChanges();
    }, 400);
  }

  openRemoveMemberModal(user: any) {
    this.removeTargetMember = user;
    this.isRemoveMemberModalOpen = true;
    this.cdr.detectChanges();
  }

  closeRemoveMemberModal() {
    this.isClosingRemoveMember = true;
    this.cdr.detectChanges();
    setTimeout(() => {
      this.isRemoveMemberModalOpen = false;
      this.isClosingRemoveMember = false;
      this.removeTargetMember = null;
      this.cdr.detectChanges();
    }, 400);
  }

  toggleRoleDropdown() {
    this.isRoleDropdownOpen = !this.isRoleDropdownOpen;
  }

  selectRole(role: string) {
    this.selectedRole = role;
    this.isRoleDropdownOpen = false;
    this.cdr.detectChanges();
  }

  openDeleteModal(acc: any) {
    this.deleteTargetAccount = acc;
    this.isDeleteModalOpen = true;
    this.cdr.detectChanges();
  }

  closeDeleteModal() {
    this.isClosingDelete = true;
    this.cdr.detectChanges();
    setTimeout(() => {
      this.isDeleteModalOpen = false;
      this.isClosingDelete = false;
      this.deleteTargetAccount = null;
      this.cdr.detectChanges();
    }, 400);
  }

  runAssignUserToAccount() {
    this.isSubmitting = true;
    const body = { accountId: this.selectedAccount.id, userId: this.roleTargetUser.id, role: this.selectedRole };
    this.http.post('https://localhost:7272/api/accounts/assign-user', body).subscribe({
      next: () => {
        this.fetchAccountUsers(this.selectedAccount.id);
        this.closeRoleModal();
        this.isSubmitting = false;
        this.cdr.detectChanges();
      },
      error: () => { this.isSubmitting = false; this.cdr.detectChanges(); }
    });
  }

  runDeleteAccount() {
    this.isSubmitting = true;
    this.http.delete(`https://localhost:7272/api/accounts/${this.deleteTargetAccount.id}`).subscribe({
      next: () => {
        this.closeDeleteModal();
        this.onSuccessCleanup();
      },
      error: () => { this.isSubmitting = false; this.cdr.detectChanges(); }
    });
  }

  runUpdateUserRole() {
    this.isSubmitting = true;
    const body = { role: this.selectedRole };
    const userId = this.roleTargetUser.userId || this.roleTargetUser.id;
    this.http.put(`https://localhost:7272/api/accounts/${this.selectedAccount.id}/users/${userId}/role`, body).subscribe({
      next: () => {
        this.fetchAccountUsers(this.selectedAccount.id);
        this.closeRoleModal();
        this.isSubmitting = false;
      },
      error: () => this.isSubmitting = false
    });
  }

  runCreateAccount() {
    this.isSubmitting = true;
    this.http.post('https://localhost:7272/api/accounts', this.newAccount).subscribe({
      next: () => this.onSuccessCleanup(),
      error: () => { this.isSubmitting = false; this.cdr.detectChanges(); }
    });
  }

  runUpdateAccount() {
    this.isSubmitting = true;
    const payload = { ...this.newAccount, id: this.selectedAccount.id };
    this.http.put(`https://localhost:7272/api/accounts/${this.selectedAccount.id}`, payload).subscribe({
      next: () => this.onSuccessCleanup(),
      error: () => { this.isSubmitting = false; this.cdr.detectChanges(); }
    });
  }

  runRemoveMember() {
    this.isSubmitting = true;
    const userId = this.removeTargetMember.userId || this.removeTargetMember.id;
    this.http.delete(`https://localhost:7272/api/accounts/${this.selectedAccount.id}/users/${userId}`).subscribe({
      next: () => {
        this.fetchAccountUsers(this.selectedAccount.id);
        this.closeRemoveMemberModal();
        this.isSubmitting = false;
        this.cdr.detectChanges();
      },
      error: () => { this.isSubmitting = false; this.cdr.detectChanges(); }
    });
  }

  private onSuccessCleanup() {
    this.fetchData();
    this.closeModal();
    this.isSubmitting = false;
    this.selectedAccount = null;
    this.cdr.detectChanges();
  }

  fetchData(): void {
    const params = new HttpParams()
      .set('page', this.pageNumber.toString())
      .set('pageSize', this.pageSize.toString())
      .set('searchTerm', this.accountSearchTerm.trim())
      .set('statusFilter', this.accountStatusFilter);
    this.http.get<any>('https://localhost:7272/api/accounts', { params }).subscribe({
      next: (res) => {
        this.accounts = res.items || [];
        this.totalCount = res.totalCount || 0;
        this.cdr.detectChanges();
      }
    });
  }

  fetchAccountUsers(accountId: string) {
    this.loadingUsers = true;
    this.http.get<any[]>(`https://localhost:7272/api/accounts/${accountId}/users`).subscribe({
      next: (res) => {
        this.accountUsers = res || [];
        this.loadingUsers = false;
        this.cdr.detectChanges();
      },
      error: () => { this.loadingUsers = false; this.cdr.detectChanges(); }
    });
  }

  get filteredAccountUsers() {
    return this.accountUsers.filter(u => {
      const term = this.memberListSearchTerm.toLowerCase();
      const matchesSearch = u.username.toLowerCase().includes(term) || (u.email && u.email.toLowerCase().includes(term));
      const role = (u.role || u.Role || 'Viewer').toLowerCase();
      const filter = this.memberListFilter.toLowerCase();
      const matchesFilter = filter === 'all' || role === filter;
      return matchesSearch && matchesFilter;
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
    if (this.tableContainer) this.tableContainer.nativeElement.scrollTo({ top: 0, behavior: 'smooth' });
  }

  setTab(tab: 'info' | 'users') {
    this.activeTab = tab;
    if (tab === 'users' && this.selectedAccount) this.fetchAccountUsers(this.selectedAccount.id);
  }

  openDetailsModal(acc: any) {
    this.selectedAccount = acc;
    this.activeTab = 'info';
    this.showDetailsModal = true;
    this.fetchAccountUsers(acc.id);
    this.cdr.detectChanges();
  }

  openModal(): void {
    this.selectedAccount = null;
    this.resetForm();
    this.showModal = true;
    this.fieldStatus = {
      name: { checking: false, valid: false, error: '' },
      email: { checking: false, valid: true, error: '' },
      phone: { checking: false, valid: true, error: '' }
    };
    this.updateFormValidity();
    this.cdr.detectChanges();
  }

  openEditModal(acc: any) {
    this.selectedAccount = acc;
    this.newAccount = {
      name: acc.name,
      desc: acc.desc || '',
      email: acc.email || '',
      phone: acc.phone || '',
      system: acc.system || false
    };
    this.fieldStatus = {
      name: { checking: false, valid: true, error: '' },
      email: { checking: false, valid: true, error: '' },
      phone: { checking: false, valid: true, error: '' }
    };
    this.showModal = true;
    this.updateFormValidity();
    this.cdr.detectChanges();
  }

  onUserSearch(term: string) {
    this.userSearchSubject.next(term);
  }

  openAssignSection() {
    this.searchUserTerm = '';
    this.fetchAvailableUsers(true);
  }

  closeModal(): void {
    this.isClosingModal = true;
    this.cdr.detectChanges();
    setTimeout(() => {
      this.showModal = false;
      this.isClosingModal = false;
      this.resetForm();
      this.cdr.detectChanges();
    }, 400);
  }

  closeDetails(): void {
    this.isClosingDetails = true;
    this.cdr.detectChanges();
    setTimeout(() => {
      this.showDetailsModal = false;
      this.isClosingDetails = false;
      this.cdr.detectChanges();
    }, 400);
  }

  resetForm(): void {
    this.newAccount = { name: '', desc: '', email: '', phone: '', system: false };
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

  get totalPages(): number { return Math.ceil(this.totalCount / this.pageSize); }

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
