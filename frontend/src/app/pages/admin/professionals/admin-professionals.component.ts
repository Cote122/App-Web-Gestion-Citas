import { Component, ElementRef, HostListener, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { Subscription, finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AuthService } from '../../../core/auth.service';
import { AdminProfessional, AdminSpecialty } from '../../../models/models';
import { ConfirmModalComponent } from '../confirm-modal/confirm-modal.component';
import { ListPagerComponent } from '../../../shared/list-pager/list-pager.component';

// Mismas reglas del registro (HU-38).
const EMAIL_PATTERN = /^[^@\s<>()[\],;:"]+@[^@\s<>()[\],;:"]+\.[A-Za-z]{2,}$/;
const PASSWORD_PATTERN = /^(?=.*[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])(?=.*\d).+$/;

interface Credentials {
  fullName: string;
  specialtyName: string;
  email: string;
  dni: string;
  password: string;
}

type CredentialField = 'email' | 'dni' | 'password';

@Component({
  selector: 'app-admin-professionals',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ConfirmModalComponent, ListPagerComponent],
  templateUrl: './admin-professionals.component.html',
  styleUrls: ['../admin.css', '../modal.css', './admin-professionals.component.css']
})
export class AdminProfessionalsComponent implements OnInit {
  @ViewChild('firstField') firstField?: ElementRef<HTMLInputElement>;
  // Al editar, el cursor empieza en Consultorio (DNI, nombre y correo no se cambian).
  @ViewChild('officeField') officeField?: ElementRef<HTMLInputElement>;

  items: AdminProfessional[] = [];
  specialties: AdminSpecialty[] = [];
  editing?: AdminProfessional;
  deactivating?: AdminProfessional;
  modalOpen = false;
  changingStatus = false;
  loading = false;
  saving = false;
  error = '';
  formError = '';
  message = '';
  page = 1;
  readonly pageSize = 8;
  searchText = '';
  get filteredItems(): AdminProfessional[] {
    const query = this.searchText.trim().toLocaleLowerCase('es-PE');
    return this.items.filter(x => !query || [x.fullName, x.specialtyName, x.office, x.userEmail ?? '', x.userDni ?? ''].some(value => value.toLocaleLowerCase('es-PE').includes(query)));
  }
  get visibleItems(): AdminProfessional[] { return this.filteredItems.slice((this.page - 1) * this.pageSize, this.page * this.pageSize); }
  filter(text: string): void { this.searchText = text; this.page = 1; }

  // HU-44: verificación del DNI y datos de acceso de la cuenta recién creada.
  dniStatus: 'idle' | 'checking' | 'valid' | 'error' = 'idle';
  dniMessage = '';
  credentials: Credentials | null = null;
  // Dato que se acaba de copiar (para mostrar el check en su botón).
  copiedField: CredentialField | null = null;
  private dniRequest?: Subscription;
  // Nombre que se llenó solo con el DNI, para no pisar lo que escriba el admin (por ejemplo, "Dra.").
  private autoName = '';

  form = this.fb.nonNullable.group({
    dni: [''],
    fullName: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
    email: [''],
    password: [''],
    office: ['', [Validators.maxLength(100)]],
    specialtyId: ['', [Validators.required]]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService, private readonly auth: AuthService) {}

  // Solo se ofrecen especialidades activas; si se edita un profesional de una inactiva, esa también aparece.
  get specialtyOptions(): AdminSpecialty[] {
    return this.specialties.filter(x => x.isActive || x.id === this.editing?.specialtyId);
  }

  get canSave(): boolean {
    return this.form.valid && !this.saving && (!!this.editing || this.dniStatus === 'valid');
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    forkJoin({ professionals: this.api.adminProfessionals(), specialties: this.api.adminSpecialties() })
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: ({ professionals, specialties }) => {
          this.items = professionals;
          this.page = Math.min(this.page, Math.max(1, Math.ceil(this.filteredItems.length / this.pageSize)));
          this.specialties = specialties;
        },
        error: () => this.error = 'No se pudieron cargar los profesionales.'
      });
  }

  openNew(): void {
    this.editing = undefined;
    this.form.reset();
    this.setAccountFields(true);
    this.regeneratePassword();
    this.openModal();
  }

  // Al editar solo se cambian nombre, consultorio y especialidad; la cuenta no se toca.
  edit(item: AdminProfessional): void {
    this.editing = item;
    this.form.reset({ fullName: item.fullName, office: item.office, specialtyId: item.specialtyId });
    this.setAccountFields(false);
    this.openModal();
  }

  /** DNI: solo números; al completar los 8 se verifica y se llena el nombre. */
  onDniInput(): void {
    const control = this.form.controls.dni;
    const clean = control.value.replace(/\D/g, '').slice(0, 8);
    if (clean !== control.value) control.setValue(clean);

    this.dniRequest?.unsubscribe();
    if (clean.length !== 8) {
      this.dniStatus = 'idle';
      this.dniMessage = '';
      return;
    }

    this.dniStatus = 'checking';
    this.dniRequest = this.auth.lookupDni(clean).subscribe({
      next: result => {
        this.dniStatus = 'valid';
        this.dniMessage = '';
        // Se llena con el nombre del DNI si está vacío o si aún tiene el que se puso solo.
        const name = this.form.controls.fullName;
        if (!name.value.trim() || name.value === this.autoName) {
          name.setValue(result.fullName);
          this.autoName = result.fullName;
        }
      },
      error: error => {
        this.dniStatus = 'error';
        this.dniMessage = error?.error?.message
          ?? (error?.status === 404 ? 'No encontramos ese DNI. Verifica el número.' : 'No pudimos verificar el DNI ahora. Intenta en unos minutos.');
      }
    });
  }

  /** Correo sin espacios y en minúsculas. */
  onEmailInput(): void {
    const control = this.form.controls.email;
    const clean = control.value.replace(/\s/g, '').toLowerCase();
    if (clean !== control.value) control.setValue(clean);
  }

  /** Contraseña aleatoria de 12 caracteres con letras y números (sin 0/O ni 1/l/I, que se confunden). */
  regeneratePassword(): void {
    const letters = 'abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ';
    const digits = '23456789';
    const all = letters + digits;
    const pick = (chars: string) => chars[crypto.getRandomValues(new Uint32Array(1))[0] % chars.length];

    const result = [pick(letters), pick(digits)];
    while (result.length < 12) result.push(pick(all));
    // Mezcla para que la letra y el número obligatorios no queden siempre al inicio.
    for (let i = result.length - 1; i > 0; i--) {
      const j = crypto.getRandomValues(new Uint32Array(1))[0] % (i + 1);
      [result[i], result[j]] = [result[j], result[i]];
    }
    this.form.controls.password.setValue(result.join(''));
  }

  @HostListener('document:keydown.escape')
  closeModal(): void {
    if (this.saving) return;
    this.dniRequest?.unsubscribe();
    this.modalOpen = false;
    this.editing = undefined;
    this.formError = '';
    this.dniStatus = 'idle';
    this.dniMessage = '';
    this.autoName = '';
    this.form.reset();
  }

  save(): void {
    if (!this.canSave) return;
    this.saving = true;
    this.formError = '';
    const value = this.form.getRawValue();

    if (this.editing) {
      this.api.updateProfessional(this.editing.id, { fullName: value.fullName, office: value.office, specialtyId: value.specialtyId })
        .pipe(finalize(() => this.saving = false))
        .subscribe({
          next: saved => {
            this.message = `Profesional "${saved.fullName}" actualizado.`;
            this.saving = false;
            this.closeModal();
            this.load();
          },
          error: error => this.formError = this.readError(error)
        });
      return;
    }

    this.api.createProfessional(value)
      .pipe(finalize(() => this.saving = false))
      .subscribe({
        next: saved => {
          this.saving = false;
          this.closeModal();
          this.copiedField = null;
          this.credentials = { fullName: saved.fullName, specialtyName: saved.specialtyName, email: value.email, dni: value.dni, password: value.password };
          this.message = `Profesional "${saved.fullName}" registrado con su cuenta.`;
          this.load();
        },
        error: error => this.formError = this.readError(error)
      });
  }

  copy(field: CredentialField): void {
    if (!this.credentials) return;
    navigator.clipboard?.writeText(this.credentials[field])
      .then(() => this.copiedField = field)
      .catch(() => this.copiedField = null);
  }

  closeCredentials(): void {
    this.credentials = null;
    this.copiedField = null;
  }

  // Desactivar pide confirmación en un modal; activar es directo.
  toggle(item: AdminProfessional): void {
    if (item.isActive) {
      this.deactivating = item;
    } else {
      this.changeStatus(item, true);
    }
  }

  confirmDeactivate(): void {
    if (this.deactivating) this.changeStatus(this.deactivating, false);
  }

  cancelDeactivate(): void {
    if (!this.changingStatus) this.deactivating = undefined;
  }

  // DNI, correo y contraseña solo se piden al registrar.
  private setAccountFields(required: boolean): void {
    this.setValidators('dni', required ? [Validators.required, Validators.pattern(/^\d{8}$/)] : []);
    this.setValidators('email', required ? [Validators.required, Validators.maxLength(100), Validators.pattern(EMAIL_PATTERN)] : []);
    this.setValidators('password', required ? [Validators.required, Validators.minLength(8), Validators.maxLength(64), Validators.pattern(PASSWORD_PATTERN)] : []);
  }

  private setValidators(name: 'dni' | 'email' | 'password', validators: ValidatorFn[]): void {
    const control = this.form.controls[name];
    control.setValidators(validators);
    control.updateValueAndValidity();
  }

  // La API puede responder { message } o los errores de validación por campo ({ errors: { Campo: [...] } }).
  private readError(error: any): string {
    const body = error?.error;
    if (body?.message) return body.message;
    const first = body?.errors ? Object.values(body.errors as Record<string, string[]>)[0]?.[0] : null;
    return first ?? 'No se pudo guardar el profesional.';
  }

  private changeStatus(item: AdminProfessional, isActive: boolean): void {
    this.changingStatus = true;
    this.error = '';
    this.message = '';
    this.api.setProfessionalActive(item.id, isActive)
      .pipe(finalize(() => {
        this.changingStatus = false;
        this.deactivating = undefined;
      }))
      .subscribe({
        next: saved => {
          this.message = `Profesional "${saved.fullName}" ${saved.isActive ? 'activado' : 'desactivado'}.`;
          this.load();
        },
        error: error => this.error = error?.error?.message ?? 'No se pudo cambiar el estado.'
      });
  }

  private openModal(): void {
    this.formError = '';
    this.message = '';
    this.error = '';
    this.modalOpen = true;
    setTimeout(() => (this.firstField ?? this.officeField)?.nativeElement.focus());
  }
}
