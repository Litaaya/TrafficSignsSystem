import { Injectable, inject } from '@angular/core';
import { AuthConfig, OAuthService } from 'angular-oauth2-oidc';

export const authConfig: AuthConfig = {
  issuer: 'http://localhost:8181/realms/trafficsigns-realm',
  redirectUri: window.location.origin + '/map',
  clientId: 'trafficsigns-ui',
  responseType: 'code',
  scope: 'openid profile email',
  showDebugInformation: true,
  requireHttps: false
};

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private oauthService = inject(OAuthService);

  constructor() {
    this.oauthService.configure(authConfig);
    this.oauthService.setupAutomaticSilentRefresh();
  }

  public async initAuth(): Promise<boolean> {
    return this.oauthService.loadDiscoveryDocumentAndTryLogin();
  }

  public login(): void {
    this.oauthService.initLoginFlow(); 
  }

  public logout(): void {
    this.oauthService.logOut();
  }

  public getToken(): string | null {
    return this.oauthService.getAccessToken() || null;
  }

  public isAdmin(): boolean {
    const token = this.getToken();
    if (!token) return false;
    try {
      const payloadBase64 = token.split('.')[1];
      const decodedJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
      const payload = JSON.parse(decodedJson);

      const roles: string[] = payload.realm_access?.roles || [];
      return roles.some(role => role.toLowerCase() === 'admin');
    } catch (e) {
      console.error('Error while parsing token:', e);
      return false;
    }
  }

  public getUserId(): string | null {
    const claims = this.oauthService.getIdentityClaims() as any;
    return claims ? claims['sub'] : null;
  }

  public getUsername(): string | null {
    const claims = this.oauthService.getIdentityClaims() as any;
    return claims ? (claims['preferred_username'] || claims['name'] || claims['sub']) : null;
  }

  public getFirstName(): string | null {
    const claims = this.oauthService.getIdentityClaims() as any;
    return claims ? claims['given_name'] : null;
  }

  public getLastName(): string | null {
    const claims = this.oauthService.getIdentityClaims() as any;
    return claims ? claims['family_name'] : null;
  }
}
