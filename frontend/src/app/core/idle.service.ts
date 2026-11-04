import { Injectable, NgZone, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { SESSION_IDLE_CONFIG } from './session.config';

const ACTIVITY_EVENTS = ['mousemove', 'mousedown', 'keydown', 'wheel', 'scroll', 'touchstart'];

/**
 * HU-34: cierra la sesión tras un periodo sin actividad del usuario.
 * Compara la hora de la última actividad con la actual (no cuenta segundos),
 * así funciona aunque el navegador pause los temporizadores con la pestaña en segundo plano.
 */
@Injectable({ providedIn: 'root' })
export class IdleService {
  readonly warning = signal(false);
  readonly secondsLeft = signal(0);

  private lastActivity = Date.now();
  private timerId?: ReturnType<typeof setInterval>;
  // Con el aviso visible, la actividad no cuenta: el usuario debe elegir "Seguir conectado".
  private readonly onActivity = () => {
    if (!this.warning()) this.lastActivity = Date.now();
  };

  constructor(private readonly auth: AuthService, private readonly router: Router, private readonly zone: NgZone) {}

  start(): void {
    this.stop();
    this.lastActivity = Date.now();
    // Fuera de la zona de Angular para no redibujar la pantalla con cada movimiento del mouse.
    this.zone.runOutsideAngular(() => {
      ACTIVITY_EVENTS.forEach(name => window.addEventListener(name, this.onActivity, { passive: true, capture: true }));
      this.timerId = setInterval(() => this.check(), 1000);
    });
  }

  stop(): void {
    ACTIVITY_EVENTS.forEach(name => window.removeEventListener(name, this.onActivity, { capture: true }));
    if (this.timerId) clearInterval(this.timerId);
    this.timerId = undefined;
    this.warning.set(false);
  }

  /** "Seguir conectado": reinicia el conteo de inactividad. */
  keepAlive(): void {
    this.lastActivity = Date.now();
    this.warning.set(false);
  }

  private check(): void {
    const idleMs = SESSION_IDLE_CONFIG.idleMinutes * 60_000;
    const warningMs = SESSION_IDLE_CONFIG.warningSeconds * 1000;
    const elapsed = Date.now() - this.lastActivity;

    if (elapsed >= idleMs) {
      this.zone.run(() => this.expire());
    } else if (elapsed >= idleMs - warningMs) {
      this.zone.run(() => {
        this.secondsLeft.set(Math.ceil((idleMs - elapsed) / 1000));
        this.warning.set(true);
      });
    }
  }

  private expire(): void {
    this.stop();
    this.auth.logout();
    void this.router.navigate(['/login'], { queryParams: { session: 'idle' } });
  }
}
