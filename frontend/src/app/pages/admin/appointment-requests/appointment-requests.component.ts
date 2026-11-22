import { Component, DestroyRef, HostListener, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { ApiService } from '../../../core/api.service';
import { AdminAppointmentChangeRequest } from '../../../models/models';

@Component({
  selector: 'app-admin-appointment-requests',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './appointment-requests.component.html',
  styleUrls: ['./appointment-requests.component.css']
})
export class AdminAppointmentRequestsComponent implements OnInit {

  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  rows: AdminAppointmentChangeRequest[] = [];

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
        next: rows => this.rows = rows,
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