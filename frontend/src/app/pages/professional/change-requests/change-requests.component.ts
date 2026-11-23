import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { ApiService } from '../../../core/api.service';
import { AppointmentChangeRequest } from '../../../models/models';
import { ListPagerComponent } from '../../../shared/list-pager/list-pager.component';

type RequestFilter =
  | 'Todas'
  | 'PendienteAprobacion'
  | 'Aprobada'
  | 'Rechazada';

@Component({
  selector: 'app-change-requests',
  standalone: true,
  imports: [CommonModule, ListPagerComponent],
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
  fromDate = '';
  toDate = '';
  page = 1;
  readonly pageSize = 8;
  get dateError(): string { return this.fromDate && this.toDate && this.fromDate > this.toDate ? 'La fecha Desde no puede ser posterior a Hasta.' : ''; }
  filterDates(field: 'fromDate' | 'toDate', value: string): void { this[field] = value; this.page = 1; }
  get visibleRequests(): AppointmentChangeRequest[] { return this.filteredRequests.slice((this.page - 1) * this.pageSize, this.page * this.pageSize); }
  private localDay(value: string): string {
    const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'America/Lima', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(value));
    const part = (type: string) => parts.find(x => x.type === type)?.value ?? '';
    return `${part('year')}-${part('month')}-${part('day')}`;
  }

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
          this.page = 1;
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
    this.page = 1;
  }

  get filteredRequests(): AppointmentChangeRequest[] {
    if (this.dateError) return [];
    return this.requests.filter(request => {
      const day = this.localDay(request.createdAt);
      return (this.activeFilter === 'Todas' || request.status === this.activeFilter)
        && (!this.fromDate || day >= this.fromDate) && (!this.toDate || day <= this.toDate);
    });
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
