import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LoginResponse, User } from '../models/models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly storageKey = 'gestion_citas_session';
  private readonly session = signal<LoginResponse | null>(this.readSession());

  readonly user = computed<User | null>(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isAuthenticated = computed(() => !!this.session()?.token);

  constructor(private readonly http: HttpClient) {}

  login(identifier: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { identifier, password })
      .pipe(tap(response => this.saveSession(response)));
  }

  register(payload: { dni: string; email: string; fullName: string; password: string }): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/register', payload)
      .pipe(tap(response => this.saveSession(response)));
  }

  logout(): void {
    sessionStorage.removeItem(this.storageKey);
    this.session.set(null);
  }

  private saveSession(response: LoginResponse): void {
    sessionStorage.setItem(this.storageKey, JSON.stringify(response));
    this.session.set(response);
  }

  private readSession(): LoginResponse | null {
    try {
      const raw = sessionStorage.getItem(this.storageKey);
      if (!raw) return null;
      const value = JSON.parse(raw) as LoginResponse;
      if (new Date(value.expiresAt).getTime() <= Date.now()) {
        sessionStorage.removeItem(this.storageKey);
        return null;
      }
      return value;
    } catch {
      return null;
    }
  }
}
