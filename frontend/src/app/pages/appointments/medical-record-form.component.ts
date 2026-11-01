import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { MedicalRecord } from '../../models/models';

@Component({
  selector: 'app-medical-record-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './medical-record-form.component.html',
  styleUrl: './medical-record-form.component.css'
})
export class MedicalRecordFormComponent {
  appointmentId = input.required<string>();
  patientId = input.required<string>();
  professionalId = input.required<string>();
  close = output<void>();

  record: Partial<MedicalRecord> = {
    attentionDate: new Date().toISOString().split('T')[0],
    reason: '',
    observations: '',
    diagnosis: '',
    recommendations: ''
  };

  loading = false;
  error = '';

  constructor(private readonly api: ApiService) {}

  save(): void {
    if (!this.record.attentionDate) {
      this.error = 'La fecha de atención es obligatoria.';
      return;
    }
    if (!this.record.reason?.trim()) {
      this.error = 'El motivo de consulta es obligatorio.';
      return;
    }
    if (!this.record.diagnosis?.trim()) {
      this.error = 'El diagnóstico es obligatorio.';
      return;
    }

    this.loading = true;
    this.error = '';

    const payload = {
      appointmentId: this.appointmentId(),
      patientId: this.patientId(),
      professionalId: this.professionalId(),
      attentionDate: this.record.attentionDate,
      reason: this.record.reason,
      observations: this.record.observations,
      diagnosis: this.record.diagnosis,
      recommendations: this.record.recommendations
    };

    this.api.createMedicalRecord(payload).pipe(finalize(() => this.loading = false)).subscribe({
      next: () => this.close.emit(),
      error: error => this.error = error?.error?.message ?? 'No se pudo registrar la atención.'
    });
  }
}
