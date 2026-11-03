import { Component, OnDestroy, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { IdleService } from '../../core/idle.service';
import { SESSION_IDLE_CONFIG } from '../../core/session.config';
import { IdleWarningModalComponent } from '../../shared/idle-warning-modal/idle-warning-modal.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, IdleWarningModalComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css'
})
export class ShellComponent implements OnInit, OnDestroy {
  readonly user = this.auth.user;
  readonly isPatient = computed(() => this.user()?.role === 'Paciente');
  readonly isAdmin = computed(() => this.user()?.role === 'Administrador');
  readonly isProfessional = computed(() => this.user()?.role === 'Profesional');
  readonly warningSeconds = SESSION_IDLE_CONFIG.warningSeconds;

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    readonly idle: IdleService
  ) {}

  // HU-34: el control de inactividad funciona mientras se está dentro de la app con sesión.
  ngOnInit(): void {
    this.idle.start();
  }

  ngOnDestroy(): void {
    this.idle.stop();
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
