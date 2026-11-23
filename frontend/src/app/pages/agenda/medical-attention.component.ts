import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';

import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';

import {
  MedicalRecord,
  ProfessionalAgendaItem,
  ProfessionalAppointmentDetail
} from '../../models/models';

import { MedicalRecordFormComponent } from '../appointments/medical-record-form.component';
import { MedicalDocumentsComponent } from '../../shared/medical-documents/medical-documents.component';
import { ListPagerComponent } from '../../shared/list-pager/list-pager.component';

@Component({
  selector: 'app-medical-attention',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    RouterLink,
    MedicalRecordFormComponent,
    MedicalDocumentsComponent,
    ListPagerComponent
  ],
  templateUrl: './medical-attention.component.html',
  styleUrl: './medical-attention.component.css'
})
export class MedicalAttentionComponent implements OnInit {

  appointmentId = '';

  detail: ProfessionalAppointmentDetail | null = null;
  currentAppointment: ProfessionalAgendaItem | null = null;

  historyAppointments: ProfessionalAgendaItem[] = [];
  fromDate = '';
  toDate = '';
  historyPage = 1;
  readonly pageSize = 8;
  get dateError(): string { return this.fromDate && this.toDate && this.fromDate > this.toDate ? 'La fecha Desde no puede ser posterior a Hasta.' : ''; }
  get filteredHistory(): ProfessionalAgendaItem[] {
    if (this.dateError) return [];
    return this.historyAppointments.filter(item => {
      const day = this.localDay(item.startAt);
      return (!this.fromDate || day >= this.fromDate) && (!this.toDate || day <= this.toDate);
    });
  }
  get visibleHistory(): ProfessionalAgendaItem[] {
    return this.filteredHistory.slice((this.historyPage - 1) * this.pageSize, this.historyPage * this.pageSize);
  }
  filterChanged(): void { this.historyPage = 1; this.closeHistoryDetail(); }
  private localDay(value: string): string {
    const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'America/Lima', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(value));
    const part = (type: string) => parts.find(x => x.type === type)?.value ?? '';
    return `${part('year')}-${part('month')}-${part('day')}`;
  }

  selectedHistoryAppointment: ProfessionalAgendaItem | null = null;
  historyRecord: MedicalRecord | null = null;

  documentsAppointment: ProfessionalAgendaItem | null = null;

  loading = false;
  loadingHistoryRecord = false;

  existingRecord: MedicalRecord | null = null;

  error = '';
  historyError = '';

  private historyRequestId = 0;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly api: ApiService,
    private readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('appointmentId');

      if (!id) {
        this.error = 'No se especificó una cita.';
        return;
      }

      this.appointmentId = id;
      this.loadAppointment();
    });
  }

  loadAppointment(): void {
    this.loading = true;
    this.error = '';

    forkJoin({
      detail: this.api.professionalAppointmentDetail(this.appointmentId),
      appointments: this.api.professionalAgenda()
    })
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: ({ detail, appointments }) => {
          this.detail = detail;

          const current = appointments.find(
            item => item.id === this.appointmentId
          );

          if (!current) {
            this.error = 'No se encontró la cita en tu agenda.';
            return;
          }

          this.currentAppointment = current;

          const currentDate = new Date(current.startAt).getTime();

          // Recuperamos las atenciones anteriores del mismo paciente.
          this.historyAppointments = appointments
            .filter(item =>
              item.patientId === current.patientId &&
              item.id !== current.id &&
              new Date(item.startAt).getTime() < currentDate &&
              item.status === 'Atendida'
            )
            .sort((a, b) =>
              new Date(b.startAt).getTime() -
              new Date(a.startAt).getTime()
            );
          this.historyPage = 1;
          this.closeHistoryDetail();
        },
        error: err => {
          this.error = err?.error?.message ??
            'No se pudo cargar la información de la cita.';
        }
      });
  }

  selectHistory(item: ProfessionalAgendaItem): void {
    this.selectedHistoryAppointment = item;
    this.historyRecord = null;
    this.historyError = '';
    this.loadingHistoryRecord = true;

    const requestId = ++this.historyRequestId;

    this.api.getMedicalRecordByAppointment(item.id)
      .pipe(finalize(() => {
        if (requestId === this.historyRequestId) {
          this.loadingHistoryRecord = false;
        }
      }))
      .subscribe({
        next: record => {
          if (requestId === this.historyRequestId) {
            this.historyRecord = record;
          }
        },
        error: () => {
          if (requestId === this.historyRequestId) {
            this.historyError =
              'Esta cita no tiene un registro médico disponible.';
          }
        }
      });
  }

  closeHistoryDetail(): void {
    this.selectedHistoryAppointment = null;
    this.historyRecord = null;
    this.historyError = '';
    this.historyRequestId++;
    this.loadingHistoryRecord = false;
  }

  goBack(): void {
    this.router.navigate(['/app/agenda']);
  }

  badgeClass(status: string): string {
    if (status === 'Confirmada') return 'badge-green';
    if (status === 'Atendida') return 'badge-blue';
    return 'badge-gray';
  }

  get currentUserId(): string {
    return this.auth.user()?.id ?? '';
  }
}
