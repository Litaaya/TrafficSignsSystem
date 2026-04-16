import { NgModule, APP_INITIALIZER } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { RouterModule } from '@angular/router';
import { OAuthModule } from 'angular-oauth2-oidc';

// Material
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';

import { AppComponent } from './app';
import { AppRoutingModule } from './app-routing-module';
import { AuthInterceptor } from './core/interceptors/auth-interceptor';
import { ErrorInterceptor } from './core/interceptors/error-interceptor';

// Components
import { MainLayoutComponent } from './layout/main-layout';
import { AccountListComponent } from './features/admin/accounts/account-list';
import { UserListComponent } from './features/admin/users/user-list';
import { MapPageComponent } from './features/map/map-page';
import { AuthService } from './core/services/auth-service';
export function initializeOAuth(authService: AuthService) {
  return () => authService.initAuth();
}

@NgModule({
  declarations: [
    AppComponent,
    MainLayoutComponent,
    AccountListComponent,
    UserListComponent
  ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    CommonModule,
    HttpClientModule,
    FormsModule,
    AppRoutingModule,
    MatMenuModule,
    MatButtonModule,
    MapPageComponent,
    RouterModule.forRoot([]),
    OAuthModule.forRoot()
  ],
  providers: [
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    {
      provide: APP_INITIALIZER,
      useFactory: initializeOAuth,
      deps: [AuthService],
      multi: true
    },
    {
      provide: HTTP_INTERCEPTORS,
      useClass: ErrorInterceptor,
      multi: true
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
