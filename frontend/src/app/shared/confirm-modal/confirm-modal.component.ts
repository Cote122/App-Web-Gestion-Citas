import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface ResumenCita {
  especialidad: string;
  medico: string;
  fecha: string;
  hora: string;
  motivo?: string;
}

@Component({
  selector: 'app-confirm-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './confirm-modal.component.html',
  styleUrl: './confirm-modal.component.css' // Mismo CSS que alert-modal
})
export class ConfirmModalComponent {
  @Input() title = 'Confirmar Cita Médica';
  @Input() datos: ResumenCita | null = null;
  @Input() confirmText = 'Sí, confirmar cita';
  @Input() cancelText = 'Modificar datos';

  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  @HostListener('document:keydown.escape')
  onCancel(): void {
    this.cancelled.emit();
  }

  onConfirm(): void {
    this.confirmed.emit();
  }
}