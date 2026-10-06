import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth.guard';
import { LoginComponent } from './pages/login/login.component';
import { RegisterComponent } from './pages/register/register.component';
import { ShellComponent } from './layout/shell/shell.component';
import { HomeComponent } from './pages/home/home.component';
import { BookingComponent } from './pages/booking/booking.component';
import { AppointmentsComponent } from './pages/appointments/appointments.component';
import { AdminSpecialtiesComponent } from './pages/admin/specialties/admin-specialties.component';
import { AdminProfessionalsComponent } from './pages/admin/professionals/admin-professionals.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  {
    path: 'app',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: 'home', component: HomeComponent },
      { path: 'book', component: BookingComponent },
      { path: 'appointments', component: AppointmentsComponent },
      { path: 'admin/specialties', component: AdminSpecialtiesComponent, canActivate: [adminGuard] },
      { path: 'admin/professionals', component: AdminProfessionalsComponent, canActivate: [adminGuard] },
      { path: '', pathMatch: 'full', redirectTo: 'home' }
    ]
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];
