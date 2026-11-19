import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';

// Mismas reglas de contraseña del registro (HU-38).
const PASSWORD_PATTERN = /^(?=.*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])(?=.*\d).+$/;

type Field = 'password' | 'confirmPassword';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

/** HU-04: con el enlace del correo, el usuario crea una contraseña nueva. */
@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
  styleUrl: './password.css'
})
export class ResetPasswordComponent {
  readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  loading = false;
  showPasswords = false;
  done = '';
  error = '';
  // Enlace vencido, usado o inválido: se pide uno nuevo.
  linkError = this.token ? '' : 'El enlace no es válido. Solicita uno nuevo.';
  // Igual que en el registro: solo se ve el error del campo donde está el cursor (o el que falta al enviar).
  activeField: Field | null = null;
  private flagged: Field | null = null;

  form = this.fb.nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(64), Validators.pattern(PASSWORD_PATTERN)]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordsMatch });

  constructor(private readonly fb: FormBuilder, private readonly http: HttpClient, private readonly route: ActivatedRoute, private readonly auth: AuthService) {}

  errorFor(field: Field): string {
    if (field !== this.activeField && field !== this.flagged) return '';
    const control = this.form.controls[field];
    return control.dirty || this.flagged === field ? this.rawError(field) : '';
  }

  onFocus(field: Field): void {
    this.activeField = field;
  }

  onBlur(field: Field): void {
    if (this.activeField === field) this.activeField = null;
    if (this.flagged === field && !this.rawError(field)) this.flagged = null;
  }

  submit(): void {
    if (this.loading) return;
    const firstInvalid = (['password', 'confirmPassword'] as Field[]).find(field => this.rawError(field));
    if (firstInvalid) {
      this.flagged = firstInvalid;
      document.getElementById(firstInvalid)?.focus();
      return;
    }

    this.loading = true;
    this.error = '';
    this.http.post<{ message: string }>('/api/auth/password/reset', { token: this.token, ...this.form.getRawValue() })
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: response => {
          this.auth.endSession();
          this.done = response.message;
        },
        error: error => {
          if (error?.status === 429) {
            this.error = 'Hiciste muchos intentos seguidos. Espera un minuto e intenta otra vez.';
            return;
          }
          this.linkError = error?.error?.message ?? 'El enlace no es válido o ya venció. Solicita uno nuevo.';
        }
      });
  }

  private rawError(field: Field): string {
    const control = this.form.controls[field];
    if (field === 'password') {
      if (control.hasError('required')) return 'La contraseña es obligatoria.';
      if (control.hasError('minlength')) return 'Mínimo 8 caracteres.';
      if (control.hasError('maxlength')) return 'Máximo 64 caracteres.';
      if (control.hasError('pattern')) return 'Debe tener al menos una letra y un número.';
      return '';
    }
    if (control.hasError('required')) return 'Confirma tu contraseña.';
    return this.form.hasError('passwordMismatch') ? 'Las contraseñas no coinciden.' : '';
  }
}
