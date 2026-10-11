import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

/** Modal informativo con un solo botón (para avisos y errores al usuario). */
@Component({
  selector: 'app-alert-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './alert-modal.component.html',
  styleUrl: './alert-modal.component.css'
})
export class AlertModalComponent {
  @Input() title = 'Aviso';
  @Input() message = '';
  @Input() buttonText = 'Entendido';
  @Output() closed = new EventEmitter<void>();

  @HostListener('document:keydown.escape')
  close(): void {
    this.closed.emit();
  }
}
