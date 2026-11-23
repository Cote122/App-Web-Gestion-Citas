import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Appointment, WaitlistItem } from '../../models/models';
import { AlertModalComponent } from '../../shared/alert-modal/alert-modal.component';
import { ConfirmModalComponent } from '../../shared/confirm-modal/confirm-modal.component';
import { MedicalDocumentsComponent } from '../../shared/medical-documents/medical-documents.component';

@Component({
  selector: 'app-appointments',
  standalone: true,
  imports: [CommonModule, DatePipe, AlertModalComponent, ConfirmModalComponent, MedicalDocumentsComponent],
  templateUrl: './appointments.component.html',
  styleUrl: './appointments.component.css'
})
export class AppointmentsComponent implements OnInit {
  readonly weekdays = ['L', 'M', 'M', 'J', 'V', 'S', 'D'];
  readonly pageSize = 6;
  items: Appointment[] = [];
  waitlist: WaitlistItem[] = [];
  documentsAppointment?: Appointment;

  loading = false;
  fromDate = '';
  toDate = '';
  dateError = '';
  appliedFrom = '';
  appliedTo = '';
  displayedMonth = new Date().toISOString().slice(0, 7);
  expandedDay: string | null = null;
  historyPage = 1;
  upcomingPage = 1;
  waitlistPage = 1;

  alertTitle = '';
  alertMessage = '';

  appointmentToCancel?: Appointment;
  waitlistToCancel?: WaitlistItem;

  attendanceAppointment?: Appointment;
  attendanceResponse?: 'Confirmada' | 'NoAsistira';

  constructor(
    private readonly api: ApiService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;

    forkJoin({
      appointments: this.api.myAppointments(),
      waitlist: this.api.myWaitlist()
    })
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: result => {
          this.items = result.appointments;
          this.waitlist = result.waitlist;
          if (!this.expandedDay && this.appointmentHistory.length) {
            this.displayedMonth = this.localDate(this.appointmentHistory[0].startAt).slice(0, 7);
          }
          this.clampPages();
        },
        error: error => {
          this.showAlert(
            'No se pudo cargar la información',
            error?.error?.message ?? 'No se pudieron consultar tus citas y lista de espera.'
          );
        }
      });
  }

  get upcomingAppointments(): Appointment[] {
    const now = Date.now();

    return this.items
      .filter(item =>
        item.status === 'Confirmada' &&
        new Date(item.startAt).getTime() > now
      )
      .sort((a, b) =>
        new Date(a.startAt).getTime() - new Date(b.startAt).getTime()
      );
  }

  get appointmentHistory(): Appointment[] {
    const upcomingIds = new Set(this.upcomingAppointments.map(x => x.id));

    return this.items
      .filter(item => !upcomingIds.has(item.id))
      .sort((a, b) =>
        new Date(b.startAt).getTime() - new Date(a.startAt).getTime()
      );
  }

  get filteredHistory(): Appointment[] {
    return this.appointmentHistory.filter(item => {
      const day = this.localDate(item.startAt);
      return (!this.appliedFrom || day >= this.appliedFrom) && (!this.appliedTo || day <= this.appliedTo);
    });
  }

  get historyDays(): string[] {
    return [...new Set(this.filteredHistory.map(x => this.localDate(x.startAt)))].sort().reverse();
  }

  get visibleHistoryDays(): string[] {
    return this.historyDays.slice((this.historyPage - 1) * this.pageSize, this.historyPage * this.pageSize);
  }

  get historyPages(): number { return Math.max(1, Math.ceil(this.historyDays.length / this.pageSize)); }
  get upcomingPages(): number { return Math.max(1, Math.ceil(this.upcomingAppointments.length / this.pageSize)); }
  get waitlistPages(): number { return Math.max(1, Math.ceil(this.waitlist.length / this.pageSize)); }
  get visibleUpcoming(): Appointment[] { return this.upcomingAppointments.slice((this.upcomingPage - 1) * this.pageSize, this.upcomingPage * this.pageSize); }
  get visibleWaitlist(): WaitlistItem[] { return this.waitlist.slice((this.waitlistPage - 1) * this.pageSize, this.waitlistPage * this.pageSize); }

  get calendarDays(): Array<{ date: string; count: number } | null> {
    const [year, month] = this.displayedMonth.split('-').map(Number);
    const first = new Date(Date.UTC(year, month - 1, 1));
    const blanks = (first.getUTCDay() + 6) % 7;
    const countByDay = new Map<string, number>();
    for (const item of this.filteredHistory) {
      const date = this.localDate(item.startAt);
      countByDay.set(date, (countByDay.get(date) ?? 0) + 1);
    }
    const days = new Date(Date.UTC(year, month, 0)).getUTCDate();
    return [...Array(blanks).fill(null), ...Array.from({ length: days }, (_, i) => {
      const date = `${year}-${String(month).padStart(2, '0')}-${String(i + 1).padStart(2, '0')}`;
      return { date, count: countByDay.get(date) ?? 0 };
    })];
  }

  get monthLabel(): string {
    const [year, month] = this.displayedMonth.split('-').map(Number);
    return new Intl.DateTimeFormat('es-PE', { month: 'long', year: 'numeric', timeZone: 'UTC' })
      .format(new Date(Date.UTC(year, month - 1, 1)));
  }

  applyDates(): void {
    if ((this.fromDate && this.toDate && this.fromDate > this.toDate) || (!!this.fromDate !== !!this.toDate)) {
      this.dateError = 'Selecciona ambas fechas y comprueba que la fecha inicial no sea posterior a la final.';
      return;
    }
    this.dateError = '';
    this.appliedFrom = this.fromDate;
    this.appliedTo = this.toDate;
    this.historyPage = 1;
    this.expandedDay = null;
    const first = this.historyDays[0];
    if (first) this.displayedMonth = first.slice(0, 7);
    else if (this.fromDate) this.displayedMonth = this.fromDate.slice(0, 7);
  }

  clearDates(): void {
    this.fromDate = this.toDate = this.appliedFrom = this.appliedTo = this.dateError = '';
    this.historyPage = 1;
    this.expandedDay = null;
    if (this.historyDays.length) this.displayedMonth = this.historyDays[0].slice(0, 7);
  }

  moveMonth(delta: number): void {
    const [year, month] = this.displayedMonth.split('-').map(Number);
    this.displayedMonth = new Date(Date.UTC(year, month - 1 + delta, 1)).toISOString().slice(0, 7);
  }

  selectDay(day: string): void {
    const index = this.historyDays.indexOf(day);
    if (index < 0) return;
    this.historyPage = Math.floor(index / this.pageSize) + 1;
    this.expandedDay = this.expandedDay === day ? null : day;
  }

  appointmentsOn(day: string): Appointment[] {
    return this.filteredHistory.filter(item => this.localDate(item.startAt) === day);
  }

  localDate(value: string): string {
    const parts = new Intl.DateTimeFormat('en-US', {
      timeZone: 'America/Lima', year: 'numeric', month: '2-digit', day: '2-digit'
    }).formatToParts(new Date(value));
    const get = (name: string) => parts.find(x => x.type === name)?.value ?? '';
    return `${get('year')}-${get('month')}-${get('day')}`;
  }

  private clampPages(): void {
    this.historyPage = Math.min(this.historyPage, this.historyPages);
    this.upcomingPage = Math.min(this.upcomingPage, this.upcomingPages);
    this.waitlistPage = Math.min(this.waitlistPage, this.waitlistPages);
    if (this.expandedDay && !this.historyDays.includes(this.expandedDay)) this.expandedDay = null;
  }

  requestCancelAppointment(item: Appointment): void {
    this.appointmentToCancel = item;
  }

  confirmCancelAppointment(): void {
    if (!this.appointmentToCancel) return;

    const id = this.appointmentToCancel.id;
    this.appointmentToCancel = undefined;

    this.api.cancelAppointment(id).subscribe({
      next: () => {
        this.showAlert('Cita cancelada', 'La cita fue cancelada correctamente.');
        this.load();
      },
      error: error => this.showAlert(
        'No se pudo cancelar la cita',
        error?.error?.message ?? 'No se pudo cancelar la cita.'
      )
    });
  }

  cancelAppointmentModal(): void {
    this.appointmentToCancel = undefined;
  }

  requestCancelWaitlist(item: WaitlistItem): void {
    this.waitlistToCancel = item;
  }

  confirmCancelWaitlist(): void {
    if (!this.waitlistToCancel) return;

    const id = this.waitlistToCancel.id;
    this.waitlistToCancel = undefined;

    this.api.cancelWaitlist(id).subscribe({
      next: response => {
        this.showAlert('Lista de espera actualizada', response.message);
        this.load();
      },
      error: error => this.showAlert(
        'No se pudo salir de la lista de espera',
        error?.error?.message ?? 'No se pudo cancelar la lista de espera.'
      )
    });
  }

  cancelWaitlistModal(): void {
    this.waitlistToCancel = undefined;
  }

  requestAttendanceResponse(
    item: Appointment,
    response: 'Confirmada' | 'NoAsistira'
  ): void {
    this.attendanceAppointment = item;
    this.attendanceResponse = response;
  }

  confirmAttendanceResponse(): void {
    if (!this.attendanceAppointment || !this.attendanceResponse) return;

    const id = this.attendanceAppointment.id;
    const response = this.attendanceResponse;

    this.attendanceAppointment = undefined;
    this.attendanceResponse = undefined;

    this.api.respondAttendance(id, response).subscribe({
      next: () => {
        this.showAlert(
          'Confirmación registrada',
          response === 'Confirmada'
            ? 'Confirmaste que asistirás a tu cita.'
            : 'Registramos que no asistirás. La cita no ha sido cancelada automáticamente.'
        );
        this.load();
      },
      error: error => this.showAlert(
        'No se pudo registrar la respuesta',
        error?.error?.message ?? 'No se pudo registrar la confirmación de asistencia.'
      )
    });
  }

  cancelAttendanceModal(): void {
    this.attendanceAppointment = undefined;
    this.attendanceResponse = undefined;
  }

  newAppointment(): void {
    this.router.navigate(['/app/book']);
  }

  reschedule(item: Appointment): void {
    if (!item.rescheduling?.canReschedule) {
      this.showAlert('No se puede reprogramar', item.rescheduling?.historyComplete
        ? `Alcanzaste el límite de ${item.rescheduling.maximum} reprogramaciones permitidas para esta cita.`
        : 'No se pudo validar el historial de reprogramaciones. Actualiza la información.');
      return;
    }
    this.router.navigate(['/app/book'], {
      queryParams: { appointmentId: item.id }
    });
  }

  isActive(item: Appointment): boolean {
    return item.status === 'Confirmada' &&
      new Date(item.startAt).getTime() > Date.now();
  }

  canRespondAttendance(item: Appointment): boolean {
    return this.isActive(item) &&
      !!item.attendanceRequestedAt &&
      item.attendanceStatus === 'Pendiente';
  }

  attendanceLabel(item: Appointment): string {
    switch (item.attendanceStatus) {
      case 'Confirmada': return 'Asistencia confirmada';
      case 'NoAsistira': return 'No asistirá';
      default: return 'Pendiente de respuesta';
    }
  }

  attendanceClass(item: Appointment): string {
    switch (item.attendanceStatus) {
      case 'Confirmada': return 'attendance-confirmed';
      case 'NoAsistira': return 'attendance-declined';
      default: return 'attendance-pending';
    }
  }

  get attendanceModalTitle(): string {
    return this.attendanceResponse === 'Confirmada'
      ? 'Confirmar asistencia'
      : 'Indicar que no asistirás';
  }

  get attendanceModalConfirmText(): string {
    return this.attendanceResponse === 'Confirmada'
      ? 'Sí, asistiré'
      : 'Sí, no asistiré';
  }

  statusClass(item: Appointment): string {
    switch (item.status) {
      case 'Confirmada': return 'badge-green';
      case 'Cancelada': return 'badge-red';
      case 'Reprogramada': return 'badge-blue';
      default: return 'badge-gray';
    }
  }

  closeAlert(): void {
    this.alertTitle = '';
    this.alertMessage = '';
  }

  private showAlert(title: string, message: string): void {
    this.alertTitle = title;
    this.alertMessage = message;
  }
}
