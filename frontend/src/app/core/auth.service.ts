import { Injectable, NgZone, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { LoginResponse, RegisterResponse, User } from '../models/models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // HU-45: la sesión se guarda en localStorage para que todas las pestañas del
  // navegador compartan la misma (abrir, cambiar de cuenta y cerrar sesión).
  private readonly storageKey = 'gestion_citas_session';
  private readonly session = signal<LoginResponse | null>(this.readSession());

  readonly user = computed<User | null>(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isAuthenticated = computed(() => !!this.session()?.token);

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
    private readonly zone: NgZone
  ) {
    // El evento "storage" solo llega a las OTRAS pestañas cuando una cambia la sesión.
    window.addEventListener('storage', event => {
      if (event.key === this.storageKey || event.key === null) {
        this.zone.run(() => this.syncFromStorage());
      }
    });

    // Si el navegador restaura la página desde su caché (botón "Atrás"),
    // se vuelve a comprobar que la sesión siga existiendo.
    window.addEventListener('pageshow', event => {
      if (event.persisted) this.zone.run(() => this.syncFromStorage());
    });
  }

  login(identifier: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { identifier, password })
      .pipe(tap(response => this.saveSession(response)));
  }

  register(payload: { dni: string; email: string; fullName: string; phone: string; password: string; confirmPassword: string }): Observable<RegisterResponse> {
    return this.http.post<RegisterResponse>('/api/auth/register', payload);
  }

  // HU-37: valida el DNI con el servicio externo (a través de la API) y devuelve el nombre.
  lookupDni(dni: string): Observable<{ valid: boolean; fullName: string }> {
    return this.http.get<{ valid: boolean; fullName: string }>(`/api/auth/dni/${encodeURIComponent(dni)}`);
  }

  // HU-45: invalida el token en el servidor y cierra la sesión en todas las pestañas.
  logout(): void {
    const token = this.token();
    if (token) {
      this.http.post('/api/auth/logout', {}, { headers: { Authorization: `Bearer ${token}` } })
        .subscribe({ error: () => undefined });
    }
    this.endSession();
  }

  // Cierra la sesión sin llamar al servidor. Las demás pestañas se enteran por el evento "storage".
  endSession(): void {
    localStorage.removeItem(this.storageKey);
    this.session.set(null);
  }

  // Otra pestaña cerró sesión o entró con otra cuenta.
  private syncFromStorage(): void {
    const previous = this.session();
    const current = this.readSession();
    if (previous?.token === current?.token) return;

    this.session.set(current);
    if (!current) {
      if (previous) void this.router.navigate(['/login'], { queryParams: { session: 'closed' }, replaceUrl: true });
    } else if (previous?.user.id !== current.user.id) {
      // Cambió la cuenta: se vuelve al inicio porque el menú depende del rol.
      void this.router.navigate(['/app/home'], { replaceUrl: true });
    }
  }

  private saveSession(response: LoginResponse): void {
    localStorage.setItem(this.storageKey, JSON.stringify(response));
    this.session.set(response);
  }

  private readSession(): LoginResponse | null {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) return null;
      const value = JSON.parse(raw) as LoginResponse;
      if (new Date(value.expiresAt).getTime() <= Date.now()) {
        localStorage.removeItem(this.storageKey);
        return null;
      }
      return value;
    } catch {
      return null;
    }
  }
}
