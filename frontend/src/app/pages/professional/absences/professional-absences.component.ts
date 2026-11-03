import { Component, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AbsenceType, AffectedAppointment, ProfessionalAbsence } from '../../../models/models';
import { ConfirmModalComponent } from '../../admin/confirm-modal/confirm-modal.component';

const MAX_DAYS = 90;

/** HU-14: el profesional registra vacaciones o licencias y revisa las citas afectadas. */
@Component({
  selector: 'app-professional-absences',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ConfirmModalComponent],
  templateUrl: './professional-absences.component.html',
  styleUrls: ['../../admin/admin.css', '../../admin/modal.css', './professional-absences.component.css']
})
export class ProfessionalAbsencesComponent implements OnInit {
  items: ProfessionalAbsence[] = [];
  loading = false;
  saving = false;
  error = '';
  formError = '';
  message = '';
  modalOpen = false;

  // Modal de citas afectadas (al guardar o con "Ver citas").
  affectedOpen = false;
  affectedTitle = '';
  affected: AffectedAppointment[] = [];
  loadingAffected = false;

  cancelling?: ProfessionalAbsence;
  changingStatus = false;

  readonly today = this.toIsoDate(new Date());

  form = this.fb.nonNullable.group({
    type: ['Vacaciones' as AbsenceType, [Validators.required]],
    startDate: ['', [Validators.required]],
    endDate: ['', [Validators.required]],
    reason: ['', [Validators.maxLength(200)]]
  });

  constructor(private readonly fb: FormBuilder, private readonly api: ApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.api.myAbsences().pipe(finalize(() => this.loading = false)).subscribe({
      next: data => this.items = data,
      error: error => this.error = error?.error?.message ?? 'No se pudieron cargar tus ausencias.'
    });
  }

  openNew(): void {
    this.form.reset({ type: 'Vacaciones', startDate: '', endDate: '', reason: '' });
    this.formError = '';
    this.message = '';
    this.error = '';
    this.modalOpen = true;
  }

  @HostListener('document:keydown.escape')
  closeModals(): void {
    if (this.saving || this.changingStatus) return;
    this.modalOpen = false;
    this.affectedOpen = false;
  }

  /** Motivo: sin símbolos que no hacen falta (<, >, llaves, corchetes, barra invertida). */
  cleanReason(): void {
    const control = this.form.controls.reason;
    const clean = control.value.replace(/[<>{}[\]\\]/g, '');
    if (clean !== control.value) control.setValue(clean);
  }

  get dateError(): string {
    const { startDate, endDate } = this.form.getRawValue();
    if (startDate && startDate < this.today) return 'La fecha de inicio no puede ser anterior a hoy.';
    if (startDate && endDate && endDate < startDate) return 'La fecha de fin debe ser igual o posterior a la de inicio.';
    if (startDate && endDate && this.daysBetween(startDate, endDate) > MAX_DAYS) return `La ausencia no puede durar más de ${MAX_DAYS} días.`;
    return '';
  }

  save(): void {
    if (this.saving) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.formError = 'Completa el tipo y las fechas de la ausencia.';
      return;
    }
    if (this.dateError) {
      this.formError = this.dateError;
      return;
    }

    const value = this.form.getRawValue();
    this.saving = true;
    this.formError = '';
    this.api.createAbsence({ ...value, reason: value.reason.replace(/\s+/g, ' ').trim() })
      .pipe(finalize(() => this.saving = false))
      .subscribe({
        next: result => {
          this.saving = false;
          this.modalOpen = false;
          this.message = `${result.absence.type} registrada del ${this.formatDate(result.absence.startDate)} al ${this.formatDate(result.absence.endDate)}.`;
          this.showAffected(result.absence, result.affectedAppointments);
          this.load();
        },
        error: error => this.formError = this.readError(error, 'No se pudo registrar la ausencia.')
      });
  }

  viewAffected(item: ProfessionalAbsence): void {
    this.affectedTitle = this.affectedHeading(item);
    this.affected = [];
    this.affectedOpen = true;
    this.loadingAffected = true;
    this.api.absenceAffected(item.id).pipe(finalize(() => this.loadingAffected = false)).subscribe({
      next: data => this.affected = data,
      error: () => this.affectedOpen = false
    });
  }

  confirmCancel(): void {
    if (!this.cancelling) return;
    this.changingStatus = true;
    this.api.cancelAbsence(this.cancelling.id)
      .pipe(finalize(() => {
        this.changingStatus = false;
        this.cancelling = undefined;
      }))
      .subscribe({
        next: () => {
          this.message = 'Ausencia anulada. Tus horarios de esas fechas vuelven a estar disponibles.';
          this.load();
        },
        error: error => this.error = this.readError(error, 'No se pudo anular la ausencia.')
      });
  }

  cancelCancel(): void {
    if (!this.changingStatus) this.cancelling = undefined;
  }

  /** Las fechas llegan como "aaaa-mm-dd"; se muestran sin convertir zona horaria. */
  formatDate(value: string): string {
    const [year, month, day] = value.split('-');
    return `${day}/${month}/${year}`;
  }

  badgeClass(status: ProfessionalAbsence['status']): string {
    return status === 'Programada' ? 'badge-blue' : status === 'En curso' ? 'badge-green' : 'badge-gray';
  }

  private showAffected(absence: ProfessionalAbsence, list: AffectedAppointment[]): void {
    this.affectedTitle = this.affectedHeading(absence);
    this.affected = list;
    this.loadingAffected = false;
    this.affectedOpen = true;
  }

  private affectedHeading(item: ProfessionalAbsence): string {
    return `Citas del ${this.formatDate(item.startDate)} al ${this.formatDate(item.endDate)}`;
  }

  private daysBetween(start: string, end: string): number {
    return Math.round((Date.parse(end) - Date.parse(start)) / 86_400_000) + 1;
  }

  private toIsoDate(date: Date): string {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
  }

  private readError(error: any, fallback: string): string {
    const body = error?.error;
    if (body?.message) return body.message;
    const first = body?.errors ? Object.values(body.errors as Record<string, string[]>)[0]?.[0] : null;
    return first ?? fallback;
  }
}
