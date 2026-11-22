import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { ApiService } from '../../../core/api.service';
import { AppointmentChangeRequest } from '../../../models/models';

type RequestFilter =
  | 'Todas'
  | 'PendienteAprobacion'
  | 'Aprobada'
  | 'Rechazada';

@Component({
  selector: 'app-change-requests',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './change-requests.component.html',
  styleUrls: ['./change-requests.component.css']
})
export class ProfessionalChangeRequestsComponent implements OnInit {

  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  requests: AppointmentChangeRequest[] = [];

  loading = false;
  error = '';

  activeFilter: RequestFilter = 'Todas';

  readonly filters: { value: RequestFilter; label: string }[] = [
    { value: 'Todas', label: 'Todas' },
    { value: 'PendienteAprobacion', label: 'Pendientes' },
    { value: 'Aprobada', label: 'Aprobadas' },
    { value: 'Rechazada', label: 'Rechazadas' }
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading) return;

    this.loading = true;
    this.error = '';

    this.api.professionalChangeRequests()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading = false)
      )
      .subscribe({
        next: data => {
          this.requests = data ?? [];
        },
        error: error => {
          this.error =
            error?.error?.message ??
            error?.error?.title ??
            'No se pudieron cargar tus solicitudes.';
        }
      });
  }

  setFilter(filter: RequestFilter): void {
    this.activeFilter = filter;
  }

  get filteredRequests(): AppointmentChangeRequest[] {
    if (this.activeFilter === 'Todas') {
      return this.requests;
    }

    return this.requests.filter(
      request => request.status === this.activeFilter
    );
  }

  get pendingCount(): number {
    return this.requests.filter(
      request => request.status === 'PendienteAprobacion'
    ).length;
  }

  get approvedCount(): number {
    return this.requests.filter(
      request => request.status === 'Aprobada'
    ).length;
  }

  get rejectedCount(): number {
    return this.requests.filter(
      request => request.status === 'Rechazada'
    ).length;
  }

  typeLabel(type: string): string {
    switch (type) {
      case 'Cancelacion':
        return 'Cancelación';
      case 'Reprogramacion':
        return 'Reprogramación';
      default:
        return type;
    }
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'PendienteAprobacion':
        return 'Pendiente de aprobación';
      case 'Aprobada':
        return 'Aprobada';
      case 'Rechazada':
        return 'Rechazada';
      default:
        return status;
    }
  }

  statusDescription(status: string): string {
    switch (status) {
      case 'PendienteAprobacion':
        return 'Tu solicitud está esperando la revisión del administrador.';
      case 'Aprobada':
        return 'El administrador aprobó tu solicitud.';
      case 'Rechazada':
        return 'El administrador rechazó tu solicitud. La cita original se mantiene.';
      default:
        return '';
    }
  }

  trackById(
    index: number,
    request: AppointmentChangeRequest
  ): string {
    return request.id;
  }
}