import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private http = inject(HttpClient);
  private readonly API_URL = 'https://localhost:7272/api/accounts';

  getAccounts(): Observable<any[]> {
    return this.http.get<any[]>(`${this.API_URL}/my`);
  }
}
