import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

/** Modal de confirmación para acciones como desactivar (reemplaza al confirm() del navegador). */
@Component({
  selector: 'app-confirm-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './confirm-modal.component.html',
  styleUrl: '../modal.css'
})
export class ConfirmModalComponent {
  @Input() title = '¿Estás seguro?';
  @Input() message = '';
  @Input() confirmText = 'Confirmar';
  @Input() busy = false;
  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  @HostListener('document:keydown.escape')
  cancel(): void {
    if (!this.busy) this.cancelled.emit();
  }
}
