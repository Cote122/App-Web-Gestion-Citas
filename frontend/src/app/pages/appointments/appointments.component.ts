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

  waitlistToCancel?: WaitlistItem;

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

  cancel(item: Appointment): void {
    if (!confirm(`¿Cancelar la cita ${item.code}?`)) return;

    this.api.cancelAppointment(item.id).subscribe({
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
