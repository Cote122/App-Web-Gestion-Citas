import { Component, DestroyRef, HostListener, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { ApiService } from '../../../core/api.service';
import { AdminAppointmentChangeRequest } from '../../../models/models';
import { ListPagerComponent } from '../../../shared/list-pager/list-pager.component';

@Component({
  selector: 'app-admin-appointment-requests',
  standalone: true,
  imports: [CommonModule, FormsModule, ListPagerComponent],
  templateUrl: './appointment-requests.component.html',
  styleUrls: ['./appointment-requests.component.css']
})
export class AdminAppointmentRequestsComponent implements OnInit {

  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  rows: AdminAppointmentChangeRequest[] = [];
  searchText = '';
  fromDate = '';
  toDate = '';
  page = 1;
  readonly pageSize = 8;
  get dateError(): string { return this.fromDate && this.toDate && this.fromDate > this.toDate ? 'La fecha Desde no puede ser posterior a Hasta.' : ''; }
  get filteredRows(): AdminAppointmentChangeRequest[] {
    if (this.dateError) return [];
    const query = this.searchText.trim().toLocaleLowerCase('es-PE');
    return this.rows.filter(x => {
      const day = this.localDay(x.createdAt);
      return (!query || `${x.appointmentCode} ${x.patientName} ${x.professionalName} ${x.specialtyName}`.toLocaleLowerCase('es-PE').includes(query))
        && (!this.fromDate || day >= this.fromDate) && (!this.toDate || day <= this.toDate);
    });
  }
  get visibleRows(): AdminAppointmentChangeRequest[] { return this.filteredRows.slice((this.page - 1) * this.pageSize, this.page * this.pageSize); }
  filterChanged(): void { this.page = 1; }
  private localDay(value: string): string {
    const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'America/Lima', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(value));
    const part = (type: string) => parts.find(x => x.type === type)?.value ?? '';
    return `${part('year')}-${part('month')}-${part('day')}`;
  }

  selected?: AdminAppointmentChangeRequest;

  adminComment = '';

  loading = false;
  saving = false;

  error = '';
  success = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (this.loading || this.saving) return;

    this.loading = true;
    this.error = '';
    this.success = '';

    this.api.adminPendingAppointmentChangeRequests()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading = false)
      )
      .subscribe({
        next: rows => { this.rows = rows; this.page = 1; },
        error: error => this.showError(error)
      });
  }

  openReview(row: AdminAppointmentChangeRequest): void {
    if (this.loading || this.saving) return;

    this.selected = row;
    this.adminComment = '';
    this.error = '';
    this.success = '';
  }

  closeReview(): void {
    if (this.saving) return;

    this.selected = undefined;
    this.adminComment = '';
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeReview();
  }

  approve(): void {
    this.review('approve');
  }

  reject(): void {
    this.review('reject');
  }

  private review(action: 'approve' | 'reject'): void {
    if (!this.selected || this.saving) return;

    const row = this.selected;
    const comment = this.adminComment.trim() || null;

    this.saving = true;
    this.error = '';
    this.success = '';

    const request = action === 'approve'
      ? this.api.approveAppointmentChangeRequest(row.id, comment)
      : this.api.rejectAppointmentChangeRequest(row.id, comment);

    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          this.rows = this.rows.filter(item => item.id !== row.id);
          this.page = Math.min(this.page, Math.max(1, Math.ceil(this.filteredRows.length / this.pageSize)));

          this.success = action === 'approve'
            ? `La solicitud de ${row.patientName} fue aprobada correctamente.`
            : `La solicitud de ${row.patientName} fue rechazada correctamente.`;

          this.selected = undefined;
          this.adminComment = '';
        },
        error: error => this.showError(error)
      });
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

  private showError(error: any): void {
    this.error =
      error?.error?.message ??
      error?.error?.title ??
      'No se pudo completar la operación. Intenta nuevamente.';
  }
}
