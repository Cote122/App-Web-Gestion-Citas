import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AvailabilitySlot, Professional, Specialty } from '../../models/models';
import { ConfirmModalComponent } from '../../shared/confirm-modal/confirm-modal.component';
import { AlertModalComponent } from '../../shared/alert-modal/alert-modal.component';

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
  message = '';
  error = '';
  bookingError = '';
  readonly appointmentId = this.route.snapshot.queryParamMap.get('appointmentId');

  mostrarModal = false;

  constructor(private readonly api: ApiService, private readonly route: ActivatedRoute, private readonly router: Router) {}

  ngOnInit(): void {
    this.loading = true;
    this.api.specialties().pipe(finalize(() => this.loading = false)).subscribe({
      next: data => this.specialties = data,
      error: () => this.error = 'No se pudieron cargar las especialidades.'
    });
  }

  chooseSpecialty(item: Specialty): void {
    this.specialty = item;
    this.professional = undefined;
    this.slot = undefined;
    this.slots = [];
    this.professionals = [];
    this.error = '';
    this.loadingProfessionals = true;
    this.api.professionals(item.id).pipe(finalize(() => this.loadingProfessionals = false)).subscribe({
      next: data => { if (this.specialty?.id === item.id) this.professionals = data; },
      error: () => this.error = 'No se pudieron cargar los profesionales.'
    });
  }

  chooseProfessional(item: Professional): void {
    this.professional = item;
    this.slot = undefined;
    this.loading = true;
    this.api.availability(item.id).pipe(finalize(() => this.loading = false)).subscribe({
      next: data => this.slots = data,
      error: () => this.error = 'No se pudo consultar la disponibilidad.'
    });
  }

  chooseSlot(item: AvailabilitySlot): void {
    this.slot = item;
  }

  confirm(): void {
    if (!this.slot || this.loading) return;
    this.mostrarModal = true;
  }

  ejecutarGuardadoCita(): void {
    if (!this.slot) return;
    this.mostrarModal = false;
    this.loading = true;
    this.error = '';
    this.message = '';

    const request = this.appointmentId
      ? this.api.rescheduleAppointment(this.appointmentId, this.slot.id)
      : this.api.createAppointment(this.slot.id);

    request.pipe(finalize(() => this.loading = false)).subscribe({
      next: appointment => {
        this.message = `${this.appointmentId ? 'Cita reprogramada' : 'Cita registrada'}: ${appointment.code}`;
        setTimeout(() => this.router.navigate(['/app/appointments']), 800);
      },
      error: error => {
        // El error de la reserva se muestra en un modal.
        this.bookingError = error?.error?.message ?? 'No se pudo guardar la cita.';
        // 409: el cupo o el profesional ya no están disponibles; se recarga la información.
        if (error?.status === 409) this.refreshAfterConflict();
      }
    });
  }

  closeBookingError(): void {
    this.bookingError = '';
  }

  // Recarga los profesionales de la especialidad y, si el elegido sigue activo, sus horarios.
  // Si fue desactivado, desaparece de la lista y se limpia la selección.
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
          return;
        }

        this.api.availability(stillActive.id).subscribe({
          next: slots => { if (this.professional?.id === stillActive.id) this.slots = slots; }
        });
      }
    });
  }

  cancelarModal(): void {
    this.mostrarModal = false;
  }

  joinWaitlist(): void {
    if (!this.specialty) return;
    this.api.joinWaitlist(this.specialty.id, this.professional?.id).subscribe({
      next: () => this.message = 'Solicitud registrada en la lista de espera.',
      error: error => this.error = error?.error?.message ?? 'No se pudo registrar la lista de espera.'
    });
  }
}
