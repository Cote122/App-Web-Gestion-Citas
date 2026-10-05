import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Appointment } from '../../models/models';

@Component({
  selector: 'app-appointments',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './appointments.component.html',
  styleUrl: './appointments.component.css'
})
export class AppointmentsComponent implements OnInit {
  items: Appointment[] = [];
  loading = false;
  error = '';

  constructor(private readonly api: ApiService, private readonly router: Router) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.error = '';
    this.api.myAppointments().pipe(finalize(() => this.loading = false)).subscribe({
      next: data => this.items = data,
      error: () => this.error = 'No se pudieron consultar las citas.'
    });
  }

  cancel(item: Appointment): void {
    if (!confirm(`¿Cancelar la cita ${item.code}?`)) return;
    this.api.cancelAppointment(item.id).subscribe({
      next: () => this.load(),
      error: error => this.error = error?.error?.message ?? 'No se pudo cancelar la cita.'
    });
  }

  newAppointment(): void {
    this.router.navigate(['/app/book']);
  }

  reschedule(item: Appointment): void {
    this.router.navigate(['/app/book'], { queryParams: { appointmentId: item.id } });
  }

  isActive(item: Appointment): boolean {
    return item.status === 'Confirmada' && new Date(item.startAt).getTime() > Date.now();
  }
}
