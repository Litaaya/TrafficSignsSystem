import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth-service';

export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const token = authService.getToken();

  if (!token) {
    authService.login();
    return false;
  }

  const isAdminPath = state.url.includes('/admin');
  if (isAdminPath && !authService.isAdmin()) {
    router.navigate(['/map']);
    return false;
  }

  return true;
};
