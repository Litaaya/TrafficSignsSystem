import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AccountOfUserDto {
  accountId: string;
  accountName: string;
  isOwner: boolean;
  isSystem: boolean;
  joinedDt: string;
}

@Injectable({
  providedIn: 'root'
})
export class OsmRoadService {
  private apiUrl = 'https://localhost:7272';

  constructor(private http: HttpClient) { }

  getAccountsOfUser(userId: string): Observable<AccountOfUserDto[]> {
    return this.http.get<AccountOfUserDto[]>(`${this.apiUrl}/api/users/${userId}/accounts`);
  }

  getRoadsInView(minLat: number, minLng: number, maxLat: number, maxLng: number, zoom: number): Observable<any[]> {
    const params = new HttpParams()
      .set('minLat', minLat)
      .set('minLng', minLng)
      .set('maxLat', maxLat)
      .set('maxLng', maxLng)
      .set('zoom', zoom);
    return this.http.get<any[]>(`${this.apiUrl}/api/map/roads`, { params });
  }

  getTrafficSigns(accountId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/api/traffic-signs?accountId=${accountId}`);
  }

  getTrafficSignById(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/api/traffic-signs/${id}`);
  }

  createTrafficSign(payload: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/api/traffic-signs`, payload);
  }

  deleteTrafficSign(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/api/traffic-signs/${id}`);
  }

  reactivateTrafficSign(id: string): Observable<any> {
    return this.http.patch(`${this.apiUrl}/api/traffic-signs/${id}/reactivate`, {});
  }

  updateTrafficSign(id: string, payload: any): Observable<any> {
    return this.http.put(`${this.apiUrl}/api/traffic-signs/${id}`, payload);
  }

  getUsersInAccount(accountId: string): Observable<any[]> {
    return this.http.get<any[]>(`/api/accounts/${accountId}/users`);
  }

  inviteUserToAccount(payload: { email: string, role: string, accountId: string }): Observable<any> {
    return this.http.post(`/api/accounts/invite`, payload);
  }

  removeUserFromAccount(accountId: string, userId: string): Observable<any> {
    return this.http.delete(`/api/accounts/${accountId}/users/${userId}`);
  }
}
