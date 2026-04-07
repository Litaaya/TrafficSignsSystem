import { Component, OnInit, inject, ChangeDetectorRef, ViewChild, ElementRef } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { MatMenuTrigger } from '@angular/material/menu';
import { Subject, debounceTime, switchMap, distinctUntilChanged } from 'rxjs';

@Component({
  selector: 'app-account-list',
  standalone: false,
  templateUrl: './account-list.html'
})
export class AccountListComponent implements OnInit {
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private availableSearchSubject = new Subject<string>();
  private accountSearchSubject = new Subject<string>();

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
  errorMessage = '';
  isConfirming = false;
  confirmPassword = '';
  pendingAction: 'CREATE' | 'UPDATE' | 'DELETE' | 'REACTIVATE' | 'ASSIGN_USER' | 'UPDATE_USER_ROLE' | 'REMOVE_USER' | null = null;
  selectedAccount: any = null;
  confirmErrorMessage = '';
  selectedRole: string = 'Viewer';
  isRoleDropdownOpen = false;
  activeTab: 'info' | 'users' = 'info';
  accountUsers: any[] = [];
  availableUsers: any[] = [];
  loadingAvailable = false;
  searchAvailableTerm = '';
  availablePage = 1;
  hasMoreAvailable = true;
  pendingUserTarget: any = null;
  userListSearchTerm = '';
  userListFilter: 'all' | 'owner' = 'all';
  newAccount = { name: '', desc: '', email: '', phone: '', system: false };
  isStatusDropdownOpen = false;
  selectAccountStatus(status: 'all' | 'active' | 'inactive') {
    this.accountStatusFilter = status;
    this.isStatusDropdownOpen = false;
    this.pageNumber = 1;
    this.fetchData();
  }

  @ViewChild('assignTrigger', { read: MatMenuTrigger }) assignTrigger!: MatMenuTrigger;
  @ViewChild('jumpTrigger', { read: MatMenuTrigger }) jumpTrigger!: MatMenuTrigger;
  @ViewChild('tableContainer') tableContainer!: ElementRef;

  ngOnInit(): void {
    this.fetchData();

    this.accountSearchSubject.pipe(debounceTime(400), distinctUntilChanged()).subscribe(term => {
      this.accountSearchTerm = term;
      this.pageNumber = 1;
      this.fetchData();
    });

    this.availableSearchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe(term => {
      this.searchAvailableTerm = term;
      this.fetchAvailableUsers(true);
    });
  }

  onAccountSearch(term: string) {
    this.pageNumber = 1;
    this.accountSearchSubject.next(term);
  }

  highlight(text: string, term: string): string {
    if (!term || !text) return text;
    const sanitizedTerm = term.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&');
    const re = new RegExp(`(${sanitizedTerm})`, 'gi');
    return text.replace(re, '<b class="text-blue-600 font-black">$1</b>');
  }

  canSubmit(): boolean {
    if (this.isSubmitting) return false;
    const hasRequired = !!this.newAccount.name.trim();
    const hasChange = this.selectedAccount ? this.isDataChanged() : true;
    return hasRequired && hasChange;
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

  onConfirmUpdate() {
    if (!this.isDataChanged()) {
      this.errorMessage = "Nothing difference, don't need update process.";
      return;
    }
    this.errorMessage = '';
    this.openConfirmModal('UPDATE', this.selectedAccount);
  }

  openConfirmModal(action: any, data?: any, targetUser?: any) {
    this.pendingAction = action;
    if (data) this.selectedAccount = data;
    if (targetUser) this.pendingUserTarget = targetUser;
    if (action === 'ASSIGN_USER' && this.assignTrigger) this.assignTrigger.closeMenu();

    if (action === 'ASSIGN_USER') {
      this.selectedRole = 'Viewer';
    } else if (action === 'UPDATE_USER_ROLE') {
      this.selectedRole = targetUser?.role || (targetUser?.isOwner ? 'Owner' : 'Viewer');
    }

    this.confirmPassword = '';
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

  get filteredAccountUsers() {
    return this.accountUsers.filter(u => {
      const term = this.userListSearchTerm.toLowerCase();
      const matchesSearch = u.username.toLowerCase().includes(term) || (u.email && u.email.toLowerCase().includes(term));
      const matchesFilter = this.userListFilter === 'all' || (this.userListFilter === 'owner' && u.isOwner);
      return matchesSearch && matchesFilter;
    });
  }

  private executePendingAction() {
    switch (this.pendingAction) {
      case 'CREATE': this.runCreateAccount(); break;
      case 'UPDATE': this.runUpdateAccount(); break;
      case 'DELETE': this.runDeleteAccount(); break;
      case 'REACTIVATE': this.runReactivateAccount(); break;
      case 'ASSIGN_USER': this.runAssignUserToAccount(); break;
      case 'UPDATE_USER_ROLE': this.runUpdateUserRole(); break;
      case 'REMOVE_USER': this.runRemoveUserFromAccount(); break;
    }
  }

  private runAssignUserToAccount() {
    const body = { accountId: this.selectedAccount.id, userId: this.pendingUserTarget.id, role: this.selectedRole };
    this.http.post('https://localhost:7272/api/accounts/assign-user', body).subscribe({
      next: () => this.onRelationSuccess(),
      error: (err) => this.onRelationError(err)
    });
  }

  private runUpdateUserRole() {
    const body = { role: this.selectedRole };
    const targetId = this.pendingUserTarget.userId || this.pendingUserTarget.id;

    this.http.put(`https://localhost:7272/api/accounts/${this.selectedAccount.id}/users/${targetId}/role`, body).subscribe({
      next: () => {
        this.pendingUserTarget.role = this.selectedRole;
        this.onRelationSuccess();
      },
      error: (err) => this.onRelationError(err)
    });
  }

  private runRemoveUserFromAccount() {
    const targetId = this.pendingUserTarget.userId || this.pendingUserTarget.id;
    this.http.delete(`https://localhost:7272/api/accounts/${this.selectedAccount.id}/users/${targetId}`).subscribe({
      next: () => this.onRelationSuccess(),
      error: (err) => this.onRelationError(err)
    });
  }

  private onRelationSuccess() {
    this.fetchAccountUsers(this.selectedAccount.id);
    this.isConfirming = false;
    this.isSubmitting = false;
    this.pendingUserTarget = null;
    this.confirmPassword = '';
    this.cdr.markForCheck();
    this.cdr.detectChanges();
  }

  private onRelationError(err: any) {
    this.isSubmitting = false;
    this.confirmErrorMessage = err.error?.message || "Action fail";
    this.cdr.detectChanges();
  }

  private runCreateAccount() {
    this.http.post('https://localhost:7272/api/accounts', this.newAccount).subscribe({
      next: () => this.onSuccessCleanup(),
      error: (err) => this.onErrorCleanup(err.error?.message)
    });
  }

  private runUpdateAccount() {
    const payload = { ...this.newAccount, id: this.selectedAccount.id };
    this.http.put(`https://localhost:7272/api/accounts/${this.selectedAccount.id}`, payload).subscribe({
      next: () => this.onSuccessCleanup(),
      error: (err) => this.onErrorCleanup(err.error?.message)
    });
  }

  private runDeleteAccount() {
    this.http.delete(`https://localhost:7272/api/accounts/${this.selectedAccount.id}`).subscribe({
      next: () => this.onSuccessCleanup(),
      error: (err) => this.onErrorCleanup(err.error?.message)
    });
  }

  private runReactivateAccount() {
    this.http.patch(`https://localhost:7272/api/accounts/${this.selectedAccount.id}/reactivate`, {}).subscribe({
      next: () => this.onSuccessCleanup(),
      error: (err) => this.onErrorCleanup(err.error?.message)
    });
  }

  private onSuccessCleanup() {
    this.fetchData();
    this.closeModal();
    this.isConfirming = false;
    this.isSubmitting = false;
    this.selectedAccount = null;
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
    this.http.get<any[]>(`https://localhost:7272/api/accounts/${accountId}/users`).subscribe({
      next: (res) => {
        this.accountUsers = res || [];
        this.cdr.detectChanges();
      }
    });
  }

  onAvailableSearch(term: string) {
    this.availableSearchSubject.next(term);
  }

  openAssignSection() {
    this.searchAvailableTerm = '';
    this.fetchAvailableUsers(true);
  }

  fetchAvailableUsers(reset: boolean = false) {
    if (reset) {
      this.availablePage = 1;
      this.availableUsers = [];
      this.hasMoreAvailable = true;
    }
    if (!this.hasMoreAvailable) return;

    this.loadingAvailable = true;
    const params = new HttpParams()
      .set('pageNumber', this.availablePage.toString())
      .set('pageSize', '20')
      .set('searchTerm', this.searchAvailableTerm);

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
    if (tab === 'users' && this.selectedAccount) {
      this.fetchAccountUsers(this.selectedAccount.id);
    }
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
    this.errorMessage = '';
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
    this.showModal = true;
    this.errorMessage = '';
  }

  closeModal(): void {
    this.showModal = false;
    this.resetForm();
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
