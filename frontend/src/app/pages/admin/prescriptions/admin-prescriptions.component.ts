import { Component, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AdminPrescription, CorrectPrescription, PrescriptionCorrection, PrescriptionItem } from '../../../models/models';
import { AlertModalComponent } from '../../../shared/alert-modal/alert-modal.component';
import { PrescriptionReviewComponent } from '../../../shared/prescription-review/prescription-review.component';
import { ListPagerComponent } from '../../../shared/list-pager/list-pager.component';

@Component({ selector: 'app-admin-prescriptions', standalone: true,
  imports: [CommonModule, FormsModule, AlertModalComponent, PrescriptionReviewComponent, ListPagerComponent],
  templateUrl: './admin-prescriptions.component.html',
  styleUrls: ['../../prescriptions/prescriptions.component.css', './admin-prescriptions.component.css'] })
export class AdminPrescriptionsComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  patientDni = '';
  rows: AdminPrescription[] = [];
  fromDate = '';
  toDate = '';
  appliedFrom = '';
  appliedTo = '';
  dateError = '';
  page = 1;
  readonly pageSize = 8;
  get filteredRows(): AdminPrescription[] {
    return this.rows.filter(x => {
      const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'America/Lima', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(x.issuedAt));
      const get = (type: string) => parts.find(p => p.type === type)?.value ?? '';
      const day = `${get('year')}-${get('month')}-${get('day')}`;
      return (!this.appliedFrom || day >= this.appliedFrom) && (!this.appliedTo || day <= this.appliedTo);
    });
  }
  get visibleRows(): AdminPrescription[] { return this.filteredRows.slice((this.page - 1) * this.pageSize, this.page * this.pageSize); }
  selected?: AdminPrescription;
  history: PrescriptionCorrection[] = [];
  items: PrescriptionItem[] = [];
  generalIndications = '';
  reason = '';
  loading = false;
  saving = false;
  searched = false;
  success = '';
  alertMessage = '';
  pending?: { id: string; patient: string; professional: string; request: CorrectPrescription };
  search(): void {
    if (this.loading || this.saving || this.pending) return;
    if (!!this.fromDate !== !!this.toDate || (this.fromDate && this.fromDate > this.toDate)) {
      this.dateError = 'Selecciona ambas fechas en orden válido.'; return;
    }
    this.dateError = '';
    this.appliedFrom = this.fromDate; this.appliedTo = this.toDate; this.page = 1;
    if (!/^[0-9]{8}$/.test(this.patientDni)) { this.alertMessage = 'Ingresa el DNI del paciente de 8 dígitos.'; return; }
    this.loading = true; this.searched = true; this.rows = []; this.selected = undefined; this.success = '';
    this.api.adminPrescriptions(this.patientDni).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false))
      .subscribe({ next: rows => this.rows = rows, error: error => this.showError(error) });
  }
  open(id: string): void {
    if (this.loading || this.saving || this.pending) return;
    this.loading = true; this.selected = undefined; this.history = []; this.success = '';
    forkJoin({ row: this.api.adminPrescription(id), history: this.api.prescriptionCorrections(id) })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false)).subscribe({
        next: data => { this.fill(data.row); this.history = data.history; }, error: error => this.showError(error)
      });
  }
  private fill(row: AdminPrescription): void {
    this.selected = row; this.items = row.items.map(x => ({ ...x })); this.generalIndications = row.generalIndications ?? ''; this.reason = '';
  }
  addItem(): void { if (!this.saving && this.items.length < 20) this.items.push({ medication: '', dose: '', frequency: '', duration: '', instructions: '' }); }
  review(): void {
    if (!this.selected?.isActive || this.saving || this.pending) return;
    if (!this.reason.trim() || this.items.some(x => !x.medication.trim()) || (!this.items.length && !this.generalIndications.trim())) {
      this.alertMessage = 'Registra el motivo de corrección y al menos un medicamento o indicaciones generales.'; return;
    }
    this.pending = { id: this.selected.id, patient: this.selected.patient + ' · DNI ' + this.selected.patientDni, professional: this.selected.professional,
      request: { expectedVersion: this.selected.version, reason: this.reason.trim(), content: {
        generalIndications: this.generalIndications.trim() || null,
        items: this.items.map(x => ({ medication: x.medication.trim(), dose: x.dose?.trim() || null, frequency: x.frequency?.trim() || null,
          duration: x.duration?.trim() || null, instructions: x.instructions?.trim() || null })) } } };
  }
  confirm(): void {
    if (!this.pending || this.saving) return;
    const pending = this.pending; this.saving = true; this.success = '';
    this.api.correctPrescription(pending.id, pending.request).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving = false)).subscribe({
      next: row => {
        this.pending = undefined; this.fill(row); this.rows = this.rows.map(x => x.id === row.id ? row : x); this.success = 'Corrección guardada con su motivo e historial.';
        this.api.prescriptionCorrections(row.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: values => this.history = values, error: error => this.showError(error) });
      }, error: error => { this.pending = undefined; this.showError(error); }
    });
  }
  private showError(error: any): void { this.alertMessage = error?.error?.message ?? 'No se pudo completar la operación. Intenta nuevamente.'; }
}
