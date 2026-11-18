import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { PatientIndication, Prescription, PrescriptionItem } from '../../models/models';
import { AlertModalComponent } from '../../shared/alert-modal/alert-modal.component';

@Component({ selector: 'app-prescriptions', standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, AlertModalComponent],
  templateUrl: './prescriptions.component.html', styleUrl: './prescriptions.component.css' })
export class PrescriptionsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  readonly appointmentId = this.route.snapshot.paramMap.get('appointmentId');
  prescriptions: Prescription[] = [];
  indications: PatientIndication[] = [];
  selected?: Prescription;
  selectedIndication?: PatientIndication;
  creating = false;
  canIssue = false;
  loading = false;
  busy = false;
  detailLoading = false;
  patientName = '';
  generalIndications = '';
  items: PrescriptionItem[] = [];
  alertMessage = '';
  success = '';

  ngOnInit(): void { this.load(); }
  load(): void {
    this.loading = true; this.selected = undefined; this.selectedIndication = undefined; this.creating = false;
    if (this.appointmentId) {
      forkJoin({ prescriptions: this.api.appointmentPrescriptions(this.appointmentId), appointment: this.api.professionalAppointmentDetail(this.appointmentId) })
        .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false)).subscribe({
          next: data => { this.prescriptions = data.prescriptions; this.patientName = data.appointment.patient.fullName; this.canIssue = data.appointment.status === 'Atendida'; },
          error: error => this.showError(error)
        });
    } else {
      forkJoin({ prescriptions: this.api.myPrescriptions(), indications: this.api.myIndications() })
        .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false)).subscribe({
          next: data => { this.prescriptions = data.prescriptions; this.indications = data.indications; }, error: error => this.showError(error)
        });
    }
  }
  open(id: string): void {
    if (this.busy || this.detailLoading) return;
    this.selected = undefined; this.selectedIndication = undefined; this.creating = false; this.detailLoading = true;
    this.api.prescription(id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.detailLoading = false)).subscribe({
      next: value => this.selected = value, error: error => this.showError(error)
    });
  }
  openIndication(id: string): void {
    if (this.busy || this.detailLoading) return;
    this.selected = undefined; this.selectedIndication = undefined; this.creating = false; this.detailLoading = true;
    this.api.indication(id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.detailLoading = false)).subscribe({
      next: value => this.selectedIndication = value, error: error => this.showError(error)
    });
  }
  startNew(): void {
    if (!this.canIssue || this.busy) return;
    this.creating = true; this.selected = undefined; this.selectedIndication = undefined; this.items = []; this.generalIndications = ''; this.success = '';
  }
  addItem(): void {
    if (!this.busy && this.items.length < 20) this.items.push({ medication: '', dose: '', frequency: '', duration: '', instructions: '' });
  }
  issue(): void {
    if (this.busy || !this.appointmentId || !this.canIssue) return;
    if (!this.items.length && !this.generalIndications.trim()) { this.alertMessage = 'Registra al menos un medicamento o indicaciones generales.'; return; }
    if (this.items.some(x => !x.medication.trim())) return;
    this.busy = true; this.success = '';
    this.api.issuePrescription(this.appointmentId, { generalIndications: this.generalIndications.trim() || null, items: this.items })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: value => { this.prescriptions = [value, ...this.prescriptions]; this.selected = value; this.creating = false; this.success = 'Receta emitida y disponible para el paciente.'; },
        error: error => this.showError(error)
      });
  }
  deactivate(value: Prescription): void {
    if (!value.canDeactivate || this.busy || !window.confirm('¿Deseas anular esta receta? El paciente dejará de verla.')) return;
    this.busy = true; this.success = '';
    this.api.deactivatePrescription(value.id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
      next: updated => { this.selected = updated; this.prescriptions = this.prescriptions.map(x => x.id === updated.id ? updated : x); this.success = 'Receta anulada. El registro se conserva en el historial del profesional.'; },
      error: error => this.showError(error)
    });
  }
  private showError(error: any): void {
    this.alertMessage = error?.error?.message ?? (error?.status === 404 ? 'Receta o indicación no disponible o sin permiso de acceso.' : 'No se pudo completar la operación. Intenta nuevamente.');
  }
}
