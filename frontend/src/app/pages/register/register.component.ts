import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription, finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { AlertModalComponent } from '../../shared/alert-modal/alert-modal.component';

// HU-38: mismas reglas que valida la API (RegisterPatientRequest).
const LETTERS = 'A-Za-zÁÉÍÓÚÜÑáéíóúüñ';
const EMAIL_PATTERN = /^[^@\s<>()[\],;:"]+@[^@\s<>()[\],;:"]+\.[A-Za-z]{2,}$/;
const PHONE_PATTERN = /^9\d{8}$/;
const PASSWORD_PATTERN = new RegExp(`^(?=.*[${LETTERS}])(?=.*\\d).+$`);

type Field = 'fullName' | 'dni' | 'email' | 'phone' | 'password' | 'confirmPassword';

const MESSAGES: Record<Field, Record<string, string>> = {
  fullName: {
    required: 'Se completa al verificar tu DNI.'
  },
  dni: {
    required: 'El DNI es obligatorio.',
    pattern: 'El DNI debe tener exactamente 8 números.'
  },
  email: {
    required: 'El correo es obligatorio.',
    maxlength: 'Máximo 100 caracteres.',
    pattern: 'Ingresa un correo válido.'
  },
  phone: {
    required: 'El celular es obligatorio.',
    pattern: 'Debe tener 9 dígitos y empezar con 9.'
  },
  password: {
    required: 'La contraseña es obligatoria.',
    minlength: 'Mínimo 8 caracteres.',
    maxlength: 'Máximo 64 caracteres.',
    pattern: 'Debe tener al menos una letra y un número.'
  },
  confirmPassword: {
    required: 'Confirma tu contraseña.',
    mismatch: 'Las contraseñas no coinciden.'
  }
};

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, AlertModalComponent],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css'
})
export class RegisterComponent {
  loading = false;
  error = '';
  successMessage = '';
  // Un solo estado: el ojito muestra u oculta las dos contraseñas a la vez.
  showPasswords = false;
  // Solo se muestra el error del campo donde está el cursor.
  activeField: Field | null = null;
  // Campo señalado al intentar enviar (el primero que falta).
  private flagged: Field | null = null;
  private readonly order: Field[] = ['dni', 'fullName', 'email', 'phone', 'password', 'confirmPassword'];

  // HU-37: estado de la validación del DNI con el servicio externo.
  dniStatus: 'idle' | 'checking' | 'valid' | 'notFound' | 'error' = 'idle';
  private dniStatusMessage = '';
  private checkedDni = '';
  private dniRequest?: Subscription;

  form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required]],
    dni: ['', [Validators.required, Validators.pattern(/^\d{8}$/)]],
    email: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(EMAIL_PATTERN)]],
    phone: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(64), Validators.pattern(PASSWORD_PATTERN)]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordsMatch });

  constructor(private readonly fb: FormBuilder, private readonly auth: AuthService, private readonly router: Router) {}

  /** Mensaje de error: solo en el campo donde está el cursor, si ya escribió en él o se señaló al enviar. */
  errorFor(field: Field): string {
    if (field !== this.activeField) return '';
    const control = this.form.controls[field];
    return control.dirty || this.flagged === field ? this.rawError(field) : '';
  }

  onFocus(field: Field): void {
    this.activeField = field;
  }

  onBlur(field: Field): void {
    if (field === 'fullName' || field === 'email') this.normalize(field);
    if (this.activeField === field) this.activeField = null;
  }

  private rawError(field: Field): string {
    const control = this.form.controls[field];
    const key = control.errors ? Object.keys(control.errors)[0] : null;
    if (key) return MESSAGES[field][key] ?? '';
    if (field === 'confirmPassword' && this.form.hasError('passwordMismatch')) return MESSAGES.confirmPassword['mismatch'];
    if (field === 'dni' && this.dniStatus !== 'valid') {
      return this.dniStatus === 'checking' ? 'Estamos verificando tu DNI…' : this.dniStatusMessage || 'Verifica tu DNI.';
    }
    return '';
  }

  /** Resultado de la verificación del DNI (siempre visible, a diferencia de los errores de escritura). */
  get dniNotice(): { text: string; kind: 'ok' | 'info' | 'error' } | null {
    switch (this.dniStatus) {
      case 'checking': return { text: 'Verificando DNI…', kind: 'info' };
      case 'valid': return { text: '✓ DNI verificado', kind: 'ok' };
      case 'notFound':
      case 'error': return { text: this.dniStatusMessage, kind: 'error' };
      default: return null;
    }
  }

  retryDni(): void {
    this.checkedDni = '';
    this.onDniChange(this.form.controls.dni.value);
  }

  // Al completar los 8 números se consulta el DNI; si se borra un número, se limpia el nombre.
  private onDniChange(dni: string): void {
    if (dni.length === 8) {
      if (dni !== this.checkedDni) this.checkDni(dni);
      return;
    }
    this.dniRequest?.unsubscribe();
    this.checkedDni = '';
    this.dniStatus = 'idle';
    this.dniStatusMessage = '';
    this.form.controls.fullName.setValue('');
  }

  private checkDni(dni: string): void {
    this.dniRequest?.unsubscribe();
    this.checkedDni = dni;
    this.dniStatus = 'checking';
    this.form.controls.fullName.setValue('');
    this.dniRequest = this.auth.lookupDni(dni).subscribe({
      next: result => {
        this.dniStatus = 'valid';
        this.dniStatusMessage = '';
        this.form.controls.fullName.setValue(result.fullName);
      },
      error: error => {
        this.dniStatus = error?.status === 404 ? 'notFound' : 'error';
        this.dniStatusMessage = error?.error?.message
          ?? (this.dniStatus === 'notFound' ? 'No encontramos ese DNI. Verifica el número.' : 'No pudimos verificar tu DNI ahora. Intenta en unos minutos.');
      }
    });
  }

  /** Ojito: muestra u oculta ambas contraseñas. */
  toggleVisibility(): void {
    this.showPasswords = !this.showPasswords;
  }

  /** Deja solo números mientras se escribe (DNI y celular). */
  onlyDigits(field: 'dni' | 'phone', max: number): void {
    const control = this.form.controls[field];
    let clean = control.value.replace(/\D/g, '');
    // Celular peruano: el primer dígito siempre es 9.
    if (field === 'phone') clean = clean.replace(/^[^9]+/, '');
    clean = clean.slice(0, max);
    if (clean !== control.value) control.setValue(clean);
    if (field === 'dni') this.onDniChange(clean);
  }

  /** Correo sin espacios. */
  noSpaces(): void {
    const control = this.form.controls.email;
    const clean = control.value.replace(/\s/g, '');
    if (clean !== control.value) control.setValue(clean);
  }

  /** Limpieza al salir del campo: nombre sin espacios repetidos y correo en minúsculas. */
  normalize(field: 'fullName' | 'email'): void {
    const control = this.form.controls[field];
    const clean = field === 'fullName'
      ? control.value.replace(/\s+/g, ' ').trim()
      : control.value.trim().toLowerCase();
    if (clean !== control.value) control.setValue(clean);
  }

  submit(): void {
    this.normalize('fullName');
    this.normalize('email');
    if (this.loading) return;

    // Se señala y enfoca solo el primer campo con problema.
    const firstInvalid = this.order.find(field => this.rawError(field));
    if (firstInvalid) {
      this.flagged = firstInvalid;
      document.getElementById(firstInvalid)?.focus();
      return;
    }

    this.loading = true;
    this.error = '';
    this.auth.register(this.form.getRawValue())
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: response => this.successMessage = response?.message || 'Cuenta creada. Ya puedes iniciar sesión.',
        error: error => this.error = this.readError(error)
      });
  }

  goToLogin(): void {
    this.successMessage = '';
    void this.router.navigate(['/login']);
  }

  // La API puede responder { message } o los errores de validación por campo ({ errors: { Campo: [...] } }).
  private readError(error: any): string {
    const body = error?.error;
    if (body?.message) return body.message;
    const first = body?.errors ? Object.values(body.errors as Record<string, string[]>)[0]?.[0] : null;
    return first ?? 'No se pudo crear la cuenta.';
  }
}
