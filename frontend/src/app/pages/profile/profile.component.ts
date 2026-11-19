import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';

// HU-05: mismas reglas que el registro (HU-38) y que valida la API (UpdateProfileRequest).
const EMAIL_PATTERN = /^[^@\s<>()[\],;:"]+@[^@\s<>()[\],;:"]+\.[A-Za-z]{2,}$/;
const PHONE_PATTERN = /^9\d{8}$/;

type Field = 'email' | 'phone' | 'currentPassword';

interface Profile {
  dni: string;
  fullName: string;
  role: string;
  email: string;
  phone: string | null;
}

const MESSAGES: Record<Field, Record<string, string>> = {
  email: {
    required: 'El correo es obligatorio.',
    maxlength: 'Máximo 100 caracteres.',
    pattern: 'Ingresa un correo válido.'
  },
  phone: {
    required: 'El celular es obligatorio.',
    pattern: 'Debe tener 9 dígitos y empezar con 9.'
  },
  currentPassword: {
    required: 'Escribe tu contraseña actual para cambiar el correo.'
  }
};

/** HU-05: el usuario revisa sus datos y edita solo el correo y el celular. */
@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './profile.component.html',
  styleUrls: ['../admin/admin.css', './profile.component.css']
})
export class ProfileComponent implements OnInit {
  profile: Profile | null = null;
  loading = true;
  saving = false;
  loadError = '';
  message = '';
  showPassword = false;
  // Igual que en el registro: solo se muestra el error del campo donde está el cursor.
  activeField: Field | null = null;
  private flagged: Field | null = null;
  // Error que devolvió la API para un campo (por ejemplo, correo repetido).
  private serverError: { field: Field | null; message: string } | null = null;

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(EMAIL_PATTERN)]],
    phone: ['', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
    currentPassword: ['']
  });

  constructor(private readonly fb: FormBuilder, private readonly http: HttpClient) {}

  ngOnInit(): void {
    this.http.get<Profile>('/api/profile')
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: profile => this.setProfile(profile),
        error: () => this.loadError = 'No pudimos cargar tu perfil. Intenta otra vez.'
      });
  }

  get emailChanged(): boolean {
    return !!this.profile && this.form.controls.email.value.trim().toLowerCase() !== this.profile.email.toLowerCase();
  }

  get hasChanges(): boolean {
    return !!this.profile && (this.emailChanged || this.form.controls.phone.value !== (this.profile.phone ?? ''));
  }

  get generalError(): string {
    return this.serverError && !this.serverError.field ? this.serverError.message : '';
  }

  errorFor(field: Field): string {
    if (this.serverError?.field === field) return this.serverError.message;
    if (field !== this.activeField) return '';
    const control = this.form.controls[field];
    return control.dirty || this.flagged === field ? this.rawError(field) : '';
  }

  onFocus(field: Field): void {
    this.activeField = field;
  }

  onBlur(field: Field): void {
    if (field === 'email') this.normalizeEmail();
    if (this.activeField === field) this.activeField = null;
  }

  onInput(field: Field): void {
    this.message = '';
    if (this.serverError?.field === field || !this.serverError?.field) this.serverError = null;
    if (field === 'email') {
      const control = this.form.controls.email;
      const clean = control.value.replace(/\s/g, '');
      if (clean !== control.value) control.setValue(clean);
      if (!this.emailChanged) this.form.controls.currentPassword.setValue('');
    }
    if (field === 'phone') {
      const control = this.form.controls.phone;
      // Solo números y el primero siempre 9 (celular peruano).
      const clean = control.value.replace(/\D/g, '').replace(/^[^9]+/, '').slice(0, 9);
      if (clean !== control.value) control.setValue(clean);
    }
  }

  cancel(): void {
    this.message = '';
    if (this.profile) this.setProfile(this.profile);
  }

  save(): void {
    this.normalizeEmail();
    if (this.saving || !this.hasChanges) return;

    const order: Field[] = this.emailChanged ? ['email', 'phone', 'currentPassword'] : ['email', 'phone'];
    const firstInvalid = order.find(field => this.rawError(field));
    if (firstInvalid) {
      this.flagged = firstInvalid;
      document.getElementById(firstInvalid)?.focus();
      return;
    }

    const value = this.form.getRawValue();
    this.saving = true;
    this.serverError = null;
    this.http.put<Profile>('/api/profile', {
      email: value.email,
      phone: value.phone,
      currentPassword: this.emailChanged ? value.currentPassword : null
    })
      .pipe(finalize(() => this.saving = false))
      .subscribe({
        next: profile => {
          this.setProfile(profile);
          this.message = 'Tus datos se guardaron correctamente.';
        },
        error: error => {
          const body = error?.error;
          const field = (['email', 'phone', 'currentPassword'] as Field[]).includes(body?.field) ? body.field as Field : null;
          const first = body?.errors ? Object.values(body.errors as Record<string, string[]>)[0]?.[0] : null;
          this.serverError = { field, message: body?.message ?? first ?? 'No se pudieron guardar los cambios.' };
          if (field) document.getElementById(field)?.focus();
        }
      });
  }

  private rawError(field: Field): string {
    if (field === 'currentPassword') {
      return this.emailChanged && !this.form.controls.currentPassword.value ? MESSAGES.currentPassword['required'] : '';
    }
    const control = this.form.controls[field];
    const key = control.errors ? Object.keys(control.errors)[0] : null;
    return key ? MESSAGES[field][key] ?? '' : '';
  }

  private normalizeEmail(): void {
    const control = this.form.controls.email;
    const clean = control.value.trim().toLowerCase();
    if (clean !== control.value) control.setValue(clean);
  }

  private setProfile(profile: Profile): void {
    this.profile = profile;
    this.flagged = null;
    this.serverError = null;
    this.showPassword = false;
    this.form.reset({ email: profile.email, phone: profile.phone ?? '', currentPassword: '' });
  }
}
