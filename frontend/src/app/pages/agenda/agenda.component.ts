import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
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
  
  // Nuevas variables para la reprogramación
  rescheduleAppointment: ProfessionalAgendaItem | null = null;
  availableSlots: any[] = [];
  selectedNewSlotId: string | null = null;
  loadingSlots = false;
  savingReschedule = false;

  loading = false;
  loadingDetail = false;
  error = '';
  successMessage = '';

  constructor(private readonly api: ApiService, private readonly auth: AuthService) {}

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

  groupedSlots: { dateKey: string; dateFormatted: string; slots: any[] }[] = [];

  openRescheduleModal(item: ProfessionalAgendaItem): void {
    this.rescheduleAppointment = item;
    this.selectedNewSlotId = null;
    this.availableSlots = [];
    this.groupedSlots = [];
    this.loadingSlots = true;
    this.error = '';
    this.successMessage = '';

    this.api.getProfessionalAvailableSlots().pipe(finalize(() => this.loadingSlots = false)).subscribe({
      next: slots => {
        this.availableSlots = slots;
        this.groupSlotsByDate(slots); // Agrupamos al cargar
      },
      error: err => this.error = err?.error?.message || 'Error al cargar horarios disponibles.'
    });
  }

  // Agrupa la lista plana de slots por fecha (ej: '2026-11-10')
  groupSlotsByDate(slots: any[]): void {
    const map = new Map<string, any[]>();
    
    slots.forEach(slot => {
      const dateKey = slot.startAt.split('T')[0]; // Extrae YYYY-MM-DD
      if (!map.has(dateKey)) {
        map.set(dateKey, []);
      }
      map.get(dateKey)!.push(slot);
    });

    this.groupedSlots = Array.from(map.entries()).map(([dateKey, daySlots]) => ({
      dateKey,
      dateFormatted: dateKey, // O puedes formatearlo como prefieras
      slots: daySlots.sort((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime())
    }));
  }

  closeRescheduleModal(): void {
    this.rescheduleAppointment = null;
    this.selectedNewSlotId = null;
    this.availableSlots = [];
  }

  confirmReschedule(): void {
    if (!this.rescheduleAppointment || !this.selectedNewSlotId) return;

    this.savingReschedule = true;
    this.error = '';
    this.successMessage = '';

    this.api.rescheduleProfessionalAppointment(this.rescheduleAppointment.id, this.selectedNewSlotId)
      .pipe(finalize(() => this.savingReschedule = false))
      .subscribe({
        next: () => {
          this.successMessage = 'Cita reprogramada con éxito.';
          this.closeRescheduleModal();
          this.load();
        },
        error: err => this.error = err?.error?.message || 'No se pudo reprogramar la cita.'
      });
  }

  cancelAppointment(appointmentId: string): void {
    // Si prefieres quitar el confirm nativo, puedes cambiarlo después por un modal tuyo, 
    // pero por ahora limpiamos los alerts.
    if (!confirm('¿Estás seguro de que deseas cancelar esta cita? Se liberará el cupo.')) return;

    this.error = '';
    this.successMessage = '';

    this.api.cancelProfessionalAppointment(appointmentId).subscribe({
      next: () => {
        this.successMessage = 'Cita cancelada con éxito.';
        this.load();
      },
      error: err => this.error = err?.error?.message || 'No se pudo cancelar la cita.'
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
    this.load();
  }

  closeDetail(): void {
    this.detail = null;
  }

  badgeClass(status: string): string {
    if (status === 'Confirmada') return 'badge-green';
    if (status === 'Atendida') return 'badge-blue';
    return 'badge-gray';
  }

  get currentUserId(): string {
    return this.auth.user()?.id ?? '';
  }

  get confirmedAppointments(): ProfessionalAgendaItem[] {
    return this.items.filter(item => item.status === 'Confirmada');
  }

  get otherAppointments(): ProfessionalAgendaItem[] {
    return this.items.filter(item => item.status !== 'Confirmada');
  }
}