import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

// Misma regla de correo del registro (HU-38).
const EMAIL_PATTERN = /^[^@\s<>()[\],;:"]+@[^@\s<>()[\],;:"]+\.[A-Za-z]{2,}$/;

/** HU-04: el usuario pide un enlace para restablecer su contraseña. */
@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.component.html',
  styleUrl: './password.css'
})
export class ForgotPasswordComponent {
  loading = false;
  submitted = false;
  sentMessage = '';
  error = '';

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(EMAIL_PATTERN)]]
  });

  constructor(private readonly fb: FormBuilder, private readonly http: HttpClient) {}

  get emailError(): string {
    const control = this.form.controls.email;
    if (!this.submitted && !control.touched) return '';
    if (control.hasError('required')) return 'Ingresa tu correo.';
    if (control.hasError('pattern') || control.hasError('maxlength')) return 'Ingresa un correo válido.';
    return '';
  }

  /** Correo sin espacios y en minúsculas. */
  onEmailInput(): void {
    const control = this.form.controls.email;
    const clean = control.value.replace(/\s/g, '').toLowerCase();
    if (clean !== control.value) control.setValue(clean);
    this.error = '';
  }

  submit(): void {
    this.submitted = true;
    if (this.form.invalid || this.loading) return;

    this.loading = true;
    this.error = '';
    this.http.post<{ message: string }>('/api/auth/password/forgot', this.form.getRawValue())
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: response => this.sentMessage = response.message,
        error: error => this.error = error?.status === 429
          ? 'Hiciste muchas solicitudes seguidas. Espera un minuto e intenta otra vez.'
          : 'No pudimos procesar tu solicitud. Intenta otra vez.'
      });
  }
}
