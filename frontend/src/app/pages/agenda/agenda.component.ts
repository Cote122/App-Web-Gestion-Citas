import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ProfessionalAgendaItem, ProfessionalAppointmentDetail } from '../../models/models';
import { MedicalRecordFormComponent } from '../appointments/medical-record-form.component';

@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [CommonModule, DatePipe, MedicalRecordFormComponent],
  templateUrl: './agenda.component.html',
  styleUrls: ['./agenda.component.css']
})
export class AgendaComponent implements OnInit {
  items: ProfessionalAgendaItem[] = [];
  detail: ProfessionalAppointmentDetail | null = null;
  recordAppointmentId: string | null = null;
  recordPatientId: string | null = null;
  loading = false;
  loadingDetail = false;
  error = '';

  constructor(private readonly api: ApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    this.detail = null;
    this.recordAppointmentId = null;
    this.recordPatientId = null;
    this.api.professionalAgenda().pipe(finalize(() => this.loading = false)).subscribe({
      next: data => this.items = data,
      error: error => this.error = error?.error?.message ?? 'No se pudo consultar la agenda.'
    });
  }

  select(item: ProfessionalAgendaItem): void {
    this.loadingDetail = true;
    this.error = '';
    this.api.professionalAppointmentDetail(item.id).pipe(finalize(() => this.loadingDetail = false)).subscribe({
      next: data => this.detail = data,
      error: error => this.error = error?.error?.message ?? 'No se pudo consultar el detalle del paciente.'
    });
  }

  openRecordForm(appointmentId: string, patientId: string): void {
    this.recordAppointmentId = appointmentId;
    this.recordPatientId = patientId;
  }

  closeRecordForm(): void {
    this.recordAppointmentId = null;
    this.recordPatientId = null;
  }

  closeDetail(): void {
    this.detail = null;
  }

  badgeClass(status: string): string {
    if (status === 'Confirmada') return 'badge-green';
    if (status === 'Atendida') return 'badge-blue';
    return 'badge-gray';
  }
}
