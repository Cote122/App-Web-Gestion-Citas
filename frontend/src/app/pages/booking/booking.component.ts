import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AvailabilitySlot, Professional, Specialty } from '../../models/models';

@Component({
  selector: 'app-booking',
  standalone: true,
  imports: [CommonModule, DatePipe],
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
  message = '';
  error = '';
  readonly appointmentId = this.route.snapshot.queryParamMap.get('appointmentId');

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
    this.api.professionals(item.id).subscribe({
      next: data => this.professionals = data,
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
      error: error => this.error = error?.error?.message ?? 'No se pudo guardar la cita.'
    });
  }

  joinWaitlist(): void {
    if (!this.specialty) return;
    this.api.joinWaitlist(this.specialty.id, this.professional?.id).subscribe({
      next: () => this.message = 'Solicitud registrada en la lista de espera.',
      error: error => this.error = error?.error?.message ?? 'No se pudo registrar la lista de espera.'
    });
  }
}
