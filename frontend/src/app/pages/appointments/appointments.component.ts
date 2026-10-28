import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Appointment, WaitlistItem } from '../../models/models';
import { AlertModalComponent } from '../../shared/alert-modal/alert-modal.component';
import { ConfirmModalComponent } from '../../shared/confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-appointments',
  standalone: true,
  imports: [CommonModule, DatePipe, AlertModalComponent, ConfirmModalComponent],
  templateUrl: './appointments.component.html',
  styleUrl: './appointments.component.css'
})
export class AppointmentsComponent implements OnInit {
  items: Appointment[] = [];
  waitlist: WaitlistItem[] = [];

  loading = false;

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
