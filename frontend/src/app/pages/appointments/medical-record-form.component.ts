import { Component, input, output, OnInit } from '@angular/core';
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
export class MedicalRecordFormComponent implements OnInit {

  appointmentId = input.required<string>();
  patientId = input.required<string>();
  professionalId = input.required<string>();

  readonly = input(false);

  close = output<void>();

  record: Partial<MedicalRecord> = {
    attentionDate: this.getToday(),
    reason: '',
    observations: '',
    diagnosis: '',
    recommendations: ''
  };

  loading = false;
  loadingRecord = false;
  error = '';

  constructor(private readonly api: ApiService) {}

  ngOnInit(): void {
    if (this.readonly()) {
      this.loadExistingRecord();
    }
  }

  private getToday(): string {
    const now = new Date();

    const localDate = new Date(
      now.getTime() - now.getTimezoneOffset() * 60000
    );

    return localDate.toISOString().split('T')[0];
  }

  loadExistingRecord(): void {
    this.loadingRecord = true;
    this.error = '';

    this.api.getMedicalRecordByAppointment(this.appointmentId())
      .pipe(finalize(() => this.loadingRecord = false))
      .subscribe({
        next: data => {
          this.record = {
            attentionDate: data.attentionDate
              ? data.attentionDate.split('T')[0]
              : '',
            reason: data.reason ?? '',
            observations: data.observations ?? '',
            diagnosis: data.diagnosis ?? '',
            recommendations: data.recommendations ?? ''
          };
        },
        error: err => {
          this.error = err?.error?.message ??
            'No se pudo cargar el registro médico.';
        }
      });
  }

  save(): void {
    // Evitamos cualquier intento de guardar en modo lectura.
    if (this.readonly()) return;

    if (
      !this.record.attentionDate ||
      !this.record.reason?.trim() ||
      !this.record.diagnosis?.trim()
    ) {
      this.error = 'Completa los campos obligatorios antes de continuar.';
      return;
    }

    this.loading = true;
    this.error = '';

    const payload = {
      appointmentId: this.appointmentId(),
      patientId: this.patientId(),
      professionalId: this.professionalId(),
      attentionDate: this.record.attentionDate,
      reason: this.record.reason.trim(),
      observations: this.record.observations?.trim() ?? '',
      diagnosis: this.record.diagnosis.trim(),
      recommendations: this.record.recommendations?.trim() ?? ''
    };

    this.api.createMedicalRecord(payload)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: () => this.close.emit(),
        error: err => {
          this.error = err?.error?.message ??
            'No se pudo registrar la atención médica.';
        }
      });
  }
}