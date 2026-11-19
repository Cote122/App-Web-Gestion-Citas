import { Injectable, NgZone, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { SESSION_IDLE_CONFIG } from './session.config';

const ACTIVITY_EVENTS = ['mousemove', 'mousedown', 'keydown', 'wheel', 'scroll', 'touchstart'];
// HU-45: la sesión es compartida entre pestañas, así que la última actividad también.
const SHARED_ACTIVITY_KEY = 'gestion_citas_last_activity';
const SHARE_EVERY_MS = 5000;

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
  private lastShared = 0;
  private timerId?: ReturnType<typeof setInterval>;
  // Con el aviso visible, la actividad no cuenta: el usuario debe elegir "Seguir conectado".
  private readonly onActivity = () => {
    if (!this.warning()) this.markActivity();
  };

  constructor(private readonly auth: AuthService, private readonly router: Router, private readonly zone: NgZone) {}

  start(): void {
    this.stop();
    this.markActivity(true);
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
    this.markActivity(true);
    this.warning.set(false);
  }

  private check(): void {
    const idleMs = SESSION_IDLE_CONFIG.idleMinutes * 60_000;
    const warningMs = SESSION_IDLE_CONFIG.warningSeconds * 1000;
    const elapsed = Date.now() - Math.max(this.lastActivity, this.readSharedActivity());

    if (elapsed >= idleMs) {
      this.zone.run(() => this.expire());
    } else if (elapsed >= idleMs - warningMs) {
      this.zone.run(() => {
        this.secondsLeft.set(Math.ceil((idleMs - elapsed) / 1000));
        this.warning.set(true);
      });
    } else if (this.warning()) {
      // Hubo actividad en otra pestaña (o allí eligió "Seguir conectado").
      this.zone.run(() => this.warning.set(false));
    }
  }

  private markActivity(force = false): void {
    this.lastActivity = Date.now();
    if (force || this.lastActivity - this.lastShared >= SHARE_EVERY_MS) {
      this.lastShared = this.lastActivity;
      try {
        localStorage.setItem(SHARED_ACTIVITY_KEY, String(this.lastActivity));
      } catch {
        // Sin almacenamiento disponible: solo cuenta la actividad de esta pestaña.
      }
    }
  }

  private readSharedActivity(): number {
    try {
      return Number(localStorage.getItem(SHARED_ACTIVITY_KEY)) || 0;
    } catch {
      return 0;
    }
  }

  private expire(): void {
    this.stop();
    this.auth.logout();
    void this.router.navigate(['/login'], { queryParams: { session: 'idle' } });
  }
}
