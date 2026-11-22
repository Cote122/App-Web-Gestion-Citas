import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard, patientGuard, professionalGuard } from './core/auth.guard';
import { LoginComponent } from './pages/login/login.component';
import { RegisterComponent } from './pages/register/register.component';
import { ShellComponent } from './layout/shell/shell.component';
import { HomeComponent } from './pages/home/home.component';
import { BookingComponent } from './pages/booking/booking.component';
import { AppointmentsComponent } from './pages/appointments/appointments.component';
import { ForgotPasswordComponent } from './pages/password/forgot-password.component';
import { ResetPasswordComponent } from './pages/password/reset-password.component';
import { AdminSpecialtiesComponent } from './pages/admin/specialties/admin-specialties.component';
import { AdminProfessionalsComponent } from './pages/admin/professionals/admin-professionals.component';
import { AdminSchedulesComponent } from './pages/admin/schedules/admin-schedules.component';
import { AdminUsersComponent } from './pages/admin/users/admin-users.component';
import { AgendaComponent } from './pages/agenda/agenda.component';
import { ProfessionalAbsencesComponent } from './pages/professional/absences/professional-absences.component';
import { ResultsComponent } from './pages/results/results.component';
import { PrescriptionsComponent } from './pages/prescriptions/prescriptions.component';
import { AdminPrescriptionsComponent } from './pages/admin/prescriptions/admin-prescriptions.component';
import { ProfileComponent } from './pages/profile/profile.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
  { path: 'register', component: RegisterComponent, canActivate: [guestGuard] },
  {
    path: 'app',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: 'home', component: HomeComponent },
      { path: 'profile', component: ProfileComponent },
      { path: 'book', component: BookingComponent, canActivate: [patientGuard] },
      { path: 'appointments', component: AppointmentsComponent, canActivate: [patientGuard] },
      { path: 'results', component: ResultsComponent, canActivate: [patientGuard] },
      { path: 'prescriptions', component: PrescriptionsComponent, canActivate: [patientGuard] },
      { path: 'prescriptions/appointments/:appointmentId', component: PrescriptionsComponent, canActivate: [professionalGuard] },
      { path: 'results/appointments/:appointmentId', component: ResultsComponent, canActivate: [professionalGuard] },
      { path: 'admin/users', component: AdminUsersComponent, canActivate: [adminGuard] },
      { path: 'admin/prescriptions', component: AdminPrescriptionsComponent, canActivate: [adminGuard] },
      { path: 'admin/specialties', component: AdminSpecialtiesComponent, canActivate: [adminGuard] },
      { path: 'admin/professionals', component: AdminProfessionalsComponent, canActivate: [adminGuard] },
      { path: 'admin/schedules', component: AdminSchedulesComponent, canActivate: [adminGuard] },
      { path: 'agenda', component: AgendaComponent, canActivate: [professionalGuard] },
      { path: 'professional/absences', component: ProfessionalAbsencesComponent, canActivate: [professionalGuard] },
      { path: '', pathMatch: 'full', redirectTo: 'home' }
    ]
  },
  // HU-04: recuperación de contraseña por correo.
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset-password', component: ResetPasswordComponent },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];

