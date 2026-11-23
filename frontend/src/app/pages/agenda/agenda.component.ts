import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ProfessionalAgendaItem, ProfessionalAppointmentDetail } from '../../models/models';
import { MedicalRecordFormComponent } from '../appointments/medical-record-form.component';
import { MedicalDocumentsComponent } from '../../shared/medical-documents/medical-documents.component';
import { ListPagerComponent } from '../../shared/list-pager/list-pager.component';
@Component({
  selector: 'app-agenda',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, MedicalRecordFormComponent, MedicalDocumentsComponent, FormsModule, ListPagerComponent],
  templateUrl: './agenda.component.html',
  styleUrls: ['./agenda.component.css']
})
export class AgendaComponent implements OnInit {
  items: ProfessionalAgendaItem[] = [];
  readonly weekdays = ['L', 'M', 'M', 'J', 'V', 'S', 'D'];
  readonly pageSize = 8;
  confirmedPage = 1;
  attendedPage = 1;
  otherPage = 1;
  fromDate = '';
  toDate = '';
  appliedFrom = '';
  appliedTo = '';
  dateError = '';
  selectedDay: string | null = null;
  displayedMonth = new Date().toISOString().slice(0, 7);
  documentsAppointment?: ProfessionalAgendaItem;
  detail: ProfessionalAppointmentDetail | null = null;
  recordAppointmentId: string | null = null;
  recordPatientId: string | null = null;
  
  appointmentToCancelId: string | null = null;
  cancelReason: string = '';

  rescheduleAppointment: ProfessionalAgendaItem | null = null;
  availableSlots: any[] = [];
  selectedNewSlotId: string | null = null;
  rescheduleReason: string = '';
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
      next: data => {
        this.items = data;
        if (data.length && !this.selectedDay) this.displayedMonth = this.localDay(data[data.length - 1].startAt).slice(0, 7);
        this.clampPages();
      },
      error: error => this.error = error?.error?.message ?? 'No se pudo consultar la agenda.'
    });
  }

  groupedSlots: { dateKey: string; dateFormatted: string; slots: any[] }[] = [];

  openRescheduleModal(item: ProfessionalAgendaItem): void {
    this.rescheduleAppointment = item;
    this.selectedNewSlotId = null;
    this.rescheduleReason = '';
    this.availableSlots = [];
    this.groupedSlots = [];
    this.loadingSlots = true;
    this.error = '';
    this.successMessage = '';

    this.api.getProfessionalAvailableSlots().pipe(finalize(() => this.loadingSlots = false)).subscribe({
      next: slots => {
        this.availableSlots = slots;
        this.groupSlotsByDate(slots);
      },
      error: err => this.error = err?.error?.message || 'Error al cargar horarios disponibles.'
    });
  }

  groupSlotsByDate(slots: any[]): void {
    const map = new Map<string, any[]>();
    
    slots.forEach(slot => {
      const dateKey = slot.startAt.split('T')[0];
      if (!map.has(dateKey)) {
        map.set(dateKey, []);
      }
      map.get(dateKey)!.push(slot);
    });

    this.groupedSlots = Array.from(map.entries()).map(([dateKey, daySlots]) => ({
      dateKey,
      dateFormatted: dateKey,
      slots: daySlots.sort((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime())
    }));
  }

  closeRescheduleModal(): void {
    this.rescheduleAppointment = null;
    this.selectedNewSlotId = null;
    this.rescheduleReason = '';
    this.availableSlots = [];
  }

  confirmReschedule(): void {
    if (!this.rescheduleAppointment || !this.selectedNewSlotId) return;

    this.savingReschedule = true;
    this.error = '';
    this.successMessage = '';

    this.api.rescheduleProfessionalAppointment(
      this.rescheduleAppointment.id, 
      this.selectedNewSlotId, 
      this.rescheduleReason
    )
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

  promptCancelAppointment(appointmentId: string): void {
    this.appointmentToCancelId = appointmentId;
    this.cancelReason = '';
  }

  executeCancelAppointment(): void {
    if (!this.appointmentToCancelId) return;

    const appointmentId = this.appointmentToCancelId;
    const reasonToSend = this.cancelReason;
    
    this.appointmentToCancelId = null;
    this.cancelReason = '';
    this.error = '';
    this.successMessage = '';

    this.api.cancelProfessionalAppointment(appointmentId, reasonToSend).subscribe({
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
    return this.filteredItems.filter(item => item.status === 'Confirmada');
  }

  get attendedAppointments(): ProfessionalAgendaItem[] {
    return this.filteredItems.filter(item => item.status === 'Atendida');
  }

  get otherAppointments(): ProfessionalAgendaItem[] {
    return this.filteredItems.filter(item => item.status !== 'Confirmada' && item.status !== 'Atendida');
  }

  get rangeItems(): ProfessionalAgendaItem[] {
    return this.items.filter(item => {
      const day = this.localDay(item.startAt);
      return (!this.appliedFrom || day >= this.appliedFrom) && (!this.appliedTo || day <= this.appliedTo);
    });
  }
  get filteredItems(): ProfessionalAgendaItem[] {
    return this.rangeItems.filter(item => !this.selectedDay || this.localDay(item.startAt) === this.selectedDay);
  }
  get visibleConfirmed(): ProfessionalAgendaItem[] { return this.confirmedAppointments.slice((this.confirmedPage - 1) * this.pageSize, this.confirmedPage * this.pageSize); }
  get visibleAttended(): ProfessionalAgendaItem[] { return this.attendedAppointments.slice((this.attendedPage - 1) * this.pageSize, this.attendedPage * this.pageSize); }
  get visibleOther(): ProfessionalAgendaItem[] { return this.otherAppointments.slice((this.otherPage - 1) * this.pageSize, this.otherPage * this.pageSize); }

  get monthLabel(): string {
    const [year, month] = this.displayedMonth.split('-').map(Number);
    return new Intl.DateTimeFormat('es-PE', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(Date.UTC(year, month - 1, 1)));
  }
  get calendarDays(): Array<{ date: string; count: number } | null> {
    const [year, month] = this.displayedMonth.split('-').map(Number);
    const offset = (new Date(Date.UTC(year, month - 1, 1)).getUTCDay() + 6) % 7;
    const counts = new Map<string, number>();
    for (const item of this.rangeItems) {
      const day = this.localDay(item.startAt);
      counts.set(day, (counts.get(day) ?? 0) + 1);
    }
    const days = new Date(Date.UTC(year, month, 0)).getUTCDate();
    return [...Array(offset).fill(null), ...Array.from({ length: days }, (_, i) => {
      const date = `${year}-${String(month).padStart(2, '0')}-${String(i + 1).padStart(2, '0')}`;
      return { date, count: counts.get(date) ?? 0 };
    })];
  }
  applyDates(): void {
    if (!!this.fromDate !== !!this.toDate || (this.fromDate && this.fromDate > this.toDate)) {
      this.dateError = 'Selecciona ambas fechas y comprueba que la fecha inicial no sea posterior a la final.';
      return;
    }
    this.dateError = '';
    this.appliedFrom = this.fromDate; this.appliedTo = this.toDate;
    this.selectedDay = null;
    this.resetPages();
    const latest = this.rangeItems.map(x => this.localDay(x.startAt)).sort().at(-1);
    if (latest) this.displayedMonth = latest.slice(0, 7);
    else if (this.fromDate) this.displayedMonth = this.fromDate.slice(0, 7);
  }
  clearDates(): void {
    this.fromDate = this.toDate = this.appliedFrom = this.appliedTo = this.dateError = '';
    this.selectedDay = null;
    this.resetPages();
  }
  moveMonth(delta: number): void {
    const [year, month] = this.displayedMonth.split('-').map(Number);
    this.displayedMonth = new Date(Date.UTC(year, month - 1 + delta, 1)).toISOString().slice(0, 7);
  }
  selectDay(date: string): void {
    this.selectedDay = this.selectedDay === date ? null : date;
    this.resetPages();
  }
  private resetPages(): void { this.confirmedPage = this.attendedPage = this.otherPage = 1; }
  private clampPages(): void {
    this.confirmedPage = Math.min(this.confirmedPage, Math.max(1, Math.ceil(this.confirmedAppointments.length / this.pageSize)));
    this.attendedPage = Math.min(this.attendedPage, Math.max(1, Math.ceil(this.attendedAppointments.length / this.pageSize)));
    this.otherPage = Math.min(this.otherPage, Math.max(1, Math.ceil(this.otherAppointments.length / this.pageSize)));
  }
  private localDay(value: string): string {
    const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'America/Lima', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(value));
    const get = (type: string) => parts.find(p => p.type === type)?.value ?? '';
    return `${get('year')}-${get('month')}-${get('day')}`;
  }
}
