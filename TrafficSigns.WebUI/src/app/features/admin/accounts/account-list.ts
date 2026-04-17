import { Component, OnInit, inject, ChangeDetectorRef, ViewChild, ElementRef } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { MatMenuTrigger } from '@angular/material/menu';
import { Subject, debounceTime, distinctUntilChanged, switchMap, map, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

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
  private checkSubject = new Subject<{ field: string, value: string }>();

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

  availableUsers: any[] = [];
  loadingAvailable = false;
  searchAvailableTerm = '';
  availablePage = 1;
  hasMoreAvailable = true;

  userListSearchTerm = '';
  userListFilter: 'all' | 'owner' = 'all';
  newAccount = { name: '', desc: '', email: '', phone: '', system: false };
  isStatusDropdownOpen = false;

  fieldStatus: any = {
    name: { checking: false, valid: false, error: '' },
    email: { checking: false, valid: true, error: '' },
    phone: { checking: false, valid: true, error: '' }
  };

  isRoleModalOpen = false;
  roleActionType: 'ASSIGN' | 'UPDATE' = 'ASSIGN';
  roleTargetUser: any = null;

  @ViewChild('assignTrigger', { read: MatMenuTrigger }) assignTrigger!: MatMenuTrigger;
  @ViewChild('jumpTrigger', { read: MatMenuTrigger }) jumpTrigger!: MatMenuTrigger;
  @ViewChild('tableContainer') tableContainer!: ElementRef;

  ngOnInit(): void {
    this.fetchData();
    this.initRealtimeValidation();

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

  initRealtimeValidation() {
    this.checkSubject.pipe(
      debounceTime(600),
      switchMap((data: any) => {
        this.fieldStatus[data.field].checking = true;
        this.cdr.detectChanges();
        let queryParams: any = { field: data.field, value: data.value };
        if (this.selectedAccount?.id) queryParams.excludeId = this.selectedAccount.id;
        return this.http.get<any>(`https://localhost:7272/api/accounts/validate-field`, { params: queryParams })
          .pipe(
            map((res: any) => ({ ...res, field: data.field })),
            catchError(() => of({ isValid: false, message: 'Validation error', field: data.field, hasError: true }))
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
        this.cdr.detectChanges();
      }
    });
  }

  onFieldChange(field: string, value: string) {
    if (!value || value.trim().length < 2) {
      this.fieldStatus[field] = { checking: false, valid: field !== 'name', error: '' };
      return;
    }
    this.fieldStatus[field].checking = true;
    this.fieldStatus[field].valid = false;
    this.fieldStatus[field].error = '';
    this.checkSubject.next({ field, value });
  }

  canSubmit(): boolean {
    if (this.isSubmitting) return false;
    const isAllValid = this.fieldStatus.name.valid && this.fieldStatus.email.valid && this.fieldStatus.phone.valid;
    const hasRequired = !!this.newAccount.name.trim();
    const hasChange = this.selectedAccount ? this.isDataChanged() : true;
    return isAllValid && hasRequired && hasChange;
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

  openModal(): void {
    this.selectedAccount = null;
    this.resetForm();
    this.fieldStatus = {
      name: { checking: false, valid: false, error: '' },
      email: { checking: false, valid: true, error: '' },
      phone: { checking: false, valid: true, error: '' }
    };
    this.showModal = true;
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
  }

  selectAccountStatus(status: 'all' | 'active' | 'inactive') {
    this.accountStatusFilter = status;
    this.isStatusDropdownOpen = false;
    this.pageNumber = 1;
    this.fetchData();
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

  deleteAccount(acc: any) {
    if (confirm(`Are you sure you want to delete workspace: ${acc.name}?`)) {
      this.isSubmitting = true;
      this.http.delete(`https://localhost:7272/api/accounts/${acc.id}`).subscribe({
        next: () => this.onSuccessCleanup(),
        error: () => this.isSubmitting = false
      });
    }
  }

  reactivateAccount(acc: any) {
    this.isSubmitting = true;
    this.http.patch(`https://localhost:7272/api/accounts/${acc.id}/reactivate`, {}).subscribe({
      next: () => this.onSuccessCleanup(),
      error: () => this.isSubmitting = false
    });
  }

  removeUser(acc: any, user: any) {
    if (confirm(`Remove user ${user.username} from this workspace?`)) {
      this.isSubmitting = true;
      const targetId = user.userId || user.id;
      this.http.delete(`https://localhost:7272/api/accounts/${acc.id}/users/${targetId}`).subscribe({
        next: () => {
          this.fetchAccountUsers(acc.id);
          this.isSubmitting = false;
        },
        error: () => this.isSubmitting = false
      });
    }
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
    this.isRoleModalOpen = false;
    this.roleTargetUser = null;
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

  runAssignUserToAccount() {
    this.isSubmitting = true;
    const body = { accountId: this.selectedAccount.id, userId: this.roleTargetUser.id, role: this.selectedRole };
    this.http.post('https://localhost:7272/api/accounts/assign-user', body).subscribe({
      next: () => {
        this.fetchAccountUsers(this.selectedAccount.id);
        this.closeRoleModal();
        this.isSubmitting = false;
      },
      error: () => this.isSubmitting = false
    });
  }

  runUpdateUserRole() {
    this.isSubmitting = true;
    const body = { role: this.selectedRole };
    const targetId = this.roleTargetUser.userId || this.roleTargetUser.id;
    this.http.put(`https://localhost:7272/api/accounts/${this.selectedAccount.id}/users/${targetId}/role`, body).subscribe({
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
      error: () => this.isSubmitting = false
    });
  }

  runUpdateAccount() {
    this.isSubmitting = true;
    const payload = { ...this.newAccount, id: this.selectedAccount.id };
    this.http.put(`https://localhost:7272/api/accounts/${this.selectedAccount.id}`, payload).subscribe({
      next: () => this.onSuccessCleanup(),
      error: () => this.isSubmitting = false
    });
  }

  private onSuccessCleanup() {
    this.fetchData();
    this.closeModal();
    this.isSubmitting = false;
    this.selectedAccount = null;
    this.cdr.markForCheck();
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

  get filteredAccountUsers() {
    return this.accountUsers.filter(u => {
      const term = this.userListSearchTerm.toLowerCase();
      const matchesSearch = u.username.toLowerCase().includes(term) || (u.email && u.email.toLowerCase().includes(term));
      const userRole = u.role || u.Role || 'Viewer';
      const matchesFilter = this.userListFilter === 'all' || (this.userListFilter === 'owner' && userRole === 'Owner');
      return matchesSearch && matchesFilter;
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
    if (this.loadingAvailable || !this.hasMoreAvailable) return;
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
    if (tab === 'users' && this.selectedAccount) this.fetchAccountUsers(this.selectedAccount.id);
  }

  openDetailsModal(acc: any) {
    this.selectedAccount = acc;
    this.activeTab = 'info';
    this.showDetailsModal = true;
    this.fetchAccountUsers(acc.id);
    this.cdr.detectChanges();
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
