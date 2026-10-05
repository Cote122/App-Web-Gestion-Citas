import { Component, ElementRef, HostListener, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AdminProfessional, AdminSpecialty } from '../../../models/models';
import { ConfirmModalComponent } from '../confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-admin-professionals',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ConfirmModalComponent],
  templateUrl: './admin-professionals.component.html',
  styleUrls: ['../admin.css', '../modal.css']
})
export class AdminProfessionalsComponent implements OnInit {
  @ViewChild('firstField') firstField?: ElementRef<HTMLInputElement>;

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

  form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
    office: ['', [Validators.maxLength(100)]],
    specialtyId: ['', [Validators.required]]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}

  // Solo se ofrecen especialidades activas; si se edita un profesional de una inactiva, esa también aparece.
  get specialtyOptions(): AdminSpecialty[] {
    return this.specialties.filter(x => x.isActive || x.id === this.editing?.specialtyId);
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
          this.specialties = specialties;
        },
        error: () => this.error = 'No se pudieron cargar los profesionales.'
      });
  }

  openNew(): void {
    this.editing = undefined;
    this.form.reset();
    this.openModal();
  }

  edit(item: AdminProfessional): void {
    this.editing = item;
    this.form.setValue({ fullName: item.fullName, office: item.office, specialtyId: item.specialtyId });
    this.openModal();
  }

  @HostListener('document:keydown.escape')
  closeModal(): void {
    if (this.saving) return;
    this.modalOpen = false;
    this.editing = undefined;
    this.formError = '';
    this.form.reset();
  }

  save(): void {
    if (this.form.invalid || this.saving) return;
    this.saving = true;
    this.formError = '';
    const wasEditing = !!this.editing;
    const payload = this.form.getRawValue();
    const request = this.editing
      ? this.api.updateProfessional(this.editing.id, payload)
      : this.api.createProfessional(payload);

    request.pipe(finalize(() => this.saving = false)).subscribe({
      next: saved => {
        this.message = wasEditing ? `Profesional "${saved.fullName}" actualizado.` : `Profesional "${saved.fullName}" registrado.`;
        this.saving = false;
        this.closeModal();
        this.load();
      },
      error: error => this.formError = error?.error?.message ?? 'No se pudo guardar el profesional.'
    });
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
    setTimeout(() => this.firstField?.nativeElement.focus());
  }
}
