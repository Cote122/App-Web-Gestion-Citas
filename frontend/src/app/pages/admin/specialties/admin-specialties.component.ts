import { Component, ElementRef, HostListener, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AdminSpecialty } from '../../../models/models';
import { ConfirmModalComponent } from '../confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-admin-specialties',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ConfirmModalComponent],
  templateUrl: './admin-specialties.component.html',
  styleUrls: ['../admin.css', '../modal.css']
})
export class AdminSpecialtiesComponent implements OnInit {
  @ViewChild('firstField') firstField?: ElementRef<HTMLInputElement>;

  items: AdminSpecialty[] = [];
  editing?: AdminSpecialty;
  deactivating?: AdminSpecialty;
  modalOpen = false;
  changingStatus = false;
  loading = false;
  saving = false;
  error = '';
  formError = '';
  message = '';

  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
    price: [0, [Validators.required, Validators.min(0), Validators.max(99999)]],
    description: ['', [Validators.maxLength(300)]]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.api.adminSpecialties().pipe(finalize(() => this.loading = false)).subscribe({
      next: data => this.items = data,
      error: () => this.error = 'No se pudieron cargar las especialidades.'
    });
  }

  openNew(): void {
    this.editing = undefined;
    this.form.reset();
    this.openModal();
  }

  edit(item: AdminSpecialty): void {
    this.editing = item;
    this.form.setValue({ name: item.name, price: item.price, description: item.description });
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
      ? this.api.updateSpecialty(this.editing.id, payload)
      : this.api.createSpecialty(payload);

    request.pipe(finalize(() => this.saving = false)).subscribe({
      next: saved => {
        this.message = wasEditing ? `Especialidad "${saved.name}" actualizada.` : `Especialidad "${saved.name}" registrada.`;
        this.saving = false;
        this.closeModal();
        this.load();
      },
      error: error => this.formError = error?.error?.message ?? 'No se pudo guardar la especialidad.'
    });
  }

  // Desactivar pide confirmación en un modal; activar es directo.
  toggle(item: AdminSpecialty): void {
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

  private changeStatus(item: AdminSpecialty, isActive: boolean): void {
    this.changingStatus = true;
    this.error = '';
    this.message = '';
    this.api.setSpecialtyActive(item.id, isActive)
      .pipe(finalize(() => {
        this.changingStatus = false;
        this.deactivating = undefined;
      }))
      .subscribe({
        next: saved => {
          this.message = `Especialidad "${saved.name}" ${saved.isActive ? 'activada' : 'desactivada'}.`;
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
