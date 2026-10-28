import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AvailabilitySlot, Professional, Specialty } from '../../models/models';
import { ConfirmModalComponent } from '../../shared/confirm-modal/confirm-modal.component';
import { AlertModalComponent } from '../../shared/alert-modal/alert-modal.component';

interface CalendarDay {
  date: Date | null;
  key: string;
  hasSlots: boolean;
  availableCount: number;
  occupiedCount: number;
}

@Component({
  selector: 'app-booking',
  standalone: true,
  imports: [CommonModule, DatePipe, ConfirmModalComponent, AlertModalComponent],
  templateUrl: './booking.component.html',
  styleUrl: './booking.component.css'
})
export class BookingComponent implements OnInit {
  specialties: Specialty[] = [];
  professionals: Professional[] = [];
  slots: AvailabilitySlot[] = [];
  specialty?: Specialty;
  professional?: Professional;
  slot?: AvailabilitySlot;

  loading = false;
  loadingProfessionals = false;

  alertTitle = '';
  alertMessage = '';
  alertRedirectToAppointments = false;

  readonly appointmentId = this.route.snapshot.queryParamMap.get('appointmentId');

  selectedDateKey = '';
  calendarMonth = this.startOfMonth(new Date());

  mostrarModal = false;

  constructor(
    private readonly api: ApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.loading = true;
    this.api.specialties().pipe(finalize(() => this.loading = false)).subscribe({
      next: data => this.specialties = data,
      error: () => this.showAlert('No se pudo cargar', 'No se pudieron cargar las especialidades.')
    });
  }

  chooseSpecialty(item: Specialty): void {
    this.specialty = item;
    this.professional = undefined;
    this.slot = undefined;
    this.slots = [];
    this.professionals = [];
    this.selectedDateKey = '';

    this.loadingProfessionals = true;
    this.api.professionals(item.id).pipe(finalize(() => this.loadingProfessionals = false)).subscribe({
      next: data => { if (this.specialty?.id === item.id) this.professionals = data; },
      error: () => this.showAlert('No se pudo cargar', 'No se pudieron cargar los profesionales.')
    });
  }

  chooseProfessional(item: Professional): void {
    this.professional = item;
    this.slot = undefined;
    this.selectedDateKey = '';
    this.loading = true;

    this.api.availability(item.id).pipe(finalize(() => this.loading = false)).subscribe({
      next: data => {
        this.slots = data;

        if (data.length) {
          const firstDate = new Date(data[0].startAt);
          this.calendarMonth = this.startOfMonth(firstDate);
          this.selectedDateKey = this.dateKey(firstDate);
        }
      },
      error: () => this.showAlert('No se pudo consultar', 'No se pudo consultar la disponibilidad.')
    });
  }

  chooseDate(day: CalendarDay): void {
    if (!day.date || !day.hasSlots) return;
    this.selectedDateKey = day.key;
    this.slot = undefined;
  }

  chooseSlot(item: AvailabilitySlot): void {
    if (!item.isAvailable) return;
    this.slot = item;
  }

  previousMonth(): void {
    this.calendarMonth = new Date(
      this.calendarMonth.getFullYear(),
      this.calendarMonth.getMonth() - 1,
      1
    );
  }

  nextMonth(): void {
    this.calendarMonth = new Date(
      this.calendarMonth.getFullYear(),
      this.calendarMonth.getMonth() + 1,
      1
    );
  }

  confirm(): void {
    if (!this.slot || this.loading || !this.slot.isAvailable) return;
    this.mostrarModal = true;
  }

  ejecutarGuardadoCita(): void {
    if (!this.slot) return;

    this.mostrarModal = false;
    this.loading = true;

    const request = this.appointmentId
      ? this.api.rescheduleAppointment(this.appointmentId, this.slot.id)
      : this.api.createAppointment(this.slot.id);

    request.pipe(finalize(() => this.loading = false)).subscribe({
      next: appointment => {
        this.showAlert(
          this.appointmentId ? 'Cita reprogramada' : 'Cita registrada',
          `${this.appointmentId ? 'La cita fue reprogramada' : 'La cita fue registrada'} correctamente. Código: ${appointment.code}.`,
          true
        );
      },
      error: error => {
        this.showAlert(
          this.appointmentId ? 'No se pudo reprogramar la cita' : 'No se pudo registrar la cita',
          error?.error?.message ?? 'No se pudo guardar la cita.'
        );

        if (error?.status === 409) this.refreshAfterConflict();
      }
    });
  }

  joinWaitlist(item: AvailabilitySlot): void {
    if (item.isAvailable || this.appointmentId) return;

    this.api.joinWaitlist(item.id).subscribe({
      next: response => {
        this.showAlert(
          'Registro en lista de espera',
          `${response.message} Tu posición actual es ${response.position}.`,
          true
        );
      },
      error: error => {
        this.showAlert(
          'No se pudo registrar en la lista de espera',
          error?.error?.message ?? 'No se pudo registrar la lista de espera.'
        );
      }
    });
  }

  closeAlert(): void {
    const shouldRedirect = this.alertRedirectToAppointments;

    this.alertTitle = '';
    this.alertMessage = '';
    this.alertRedirectToAppointments = false;

    if (shouldRedirect) {
      this.router.navigate(['/app/appointments']);
    }
  }

  private showAlert(title: string, message: string, redirectToAppointments = false): void {
    this.alertTitle = title;
    this.alertMessage = message;
    this.alertRedirectToAppointments = redirectToAppointments;
  }

  private refreshAfterConflict(): void {
    if (!this.specialty) return;

    const specialtyId = this.specialty.id;
    const professionalId = this.professional?.id;
    this.slot = undefined;

    this.api.professionals(specialtyId).subscribe({
      next: professionals => {
        if (this.specialty?.id !== specialtyId) return;
        this.professionals = professionals;

        const stillActive = professionals.find(x => x.id === professionalId);
        if (!stillActive) {
          this.professional = undefined;
          this.slots = [];
          this.selectedDateKey = '';
          return;
        }

        this.api.availability(stillActive.id).subscribe({
          next: slots => {
            if (this.professional?.id === stillActive.id) this.slots = slots;
          }
        });
      }
    });
  }

  cancelarModal(): void {
    this.mostrarModal = false;
  }

  get monthLabel(): string {
    return new Intl.DateTimeFormat('es-PE', {
      month: 'long',
      year: 'numeric'
    }).format(this.calendarMonth);
  }

  get calendarDays(): CalendarDay[] {
    const year = this.calendarMonth.getFullYear();
    const month = this.calendarMonth.getMonth();
    const first = new Date(year, month, 1);
    const last = new Date(year, month + 1, 0);
    const days: CalendarDay[] = [];

    const leadingBlanks = (first.getDay() + 6) % 7;
    for (let i = 0; i < leadingBlanks; i++) {
      days.push({ date: null, key: '', hasSlots: false, availableCount: 0, occupiedCount: 0 });
    }

    for (let day = 1; day <= last.getDate(); day++) {
      const date = new Date(year, month, day);
      const key = this.dateKey(date);
      const daySlots = this.slots.filter(x => this.dateKey(new Date(x.startAt)) === key);

      days.push({
        date,
        key,
        hasSlots: daySlots.length > 0,
        availableCount: daySlots.filter(x => x.isAvailable).length,
        occupiedCount: daySlots.filter(x => !x.isAvailable).length
      });
    }

    return days;
  }

  get selectedDaySlots(): AvailabilitySlot[] {
    if (!this.selectedDateKey) return [];

    return this.slots
      .filter(x => this.dateKey(new Date(x.startAt)) === this.selectedDateKey)
      .sort((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime());
  }

  get selectedDate(): Date | null {
    const first = this.selectedDaySlots[0];
    return first ? new Date(first.startAt) : null;
  }

  private dateKey(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private startOfMonth(date: Date): Date {
    return new Date(date.getFullYear(), date.getMonth(), 1);
  }
}
