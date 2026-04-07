import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MainLayoutComponent } from './layout/main-layout';
import { AccountListComponent } from './features/admin/accounts/account-list';
import { UserListComponent } from './features/admin/users/user-list';
import { MapPageComponent } from './features/map/map-page';
import { authGuard } from './core/guards/auth-guard';

const routes: Routes = [
  { path: '', redirectTo: 'map', pathMatch: 'full' },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: 'admin/accounts', component: AccountListComponent },
      { path: 'admin/users', component: UserListComponent },
      { path: 'map', component: MapPageComponent }
    ]
  },
  { path: '**', redirectTo: 'map' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
