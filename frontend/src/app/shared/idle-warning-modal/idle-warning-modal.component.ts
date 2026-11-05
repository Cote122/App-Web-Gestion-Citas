import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

/** HU-34: aviso de cierre de sesión por inactividad, con cuenta regresiva. */
@Component({
  selector: 'app-idle-warning-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './idle-warning-modal.component.html',
  styleUrl: './idle-warning-modal.component.css'
})
export class IdleWarningModalComponent {
  @Input() secondsLeft = 0;
  @Input() totalSeconds = 60;
  @Output() stay = new EventEmitter<void>();
  @Output() leave = new EventEmitter<void>();

  readonly radius = 42;
  readonly circumference = 2 * Math.PI * this.radius;

  // El anillo se vacía a medida que bajan los segundos.
  get dashOffset(): number {
    const ratio = this.totalSeconds > 0 ? Math.max(0, Math.min(1, this.secondsLeft / this.totalSeconds)) : 0;
    return this.circumference * (1 - ratio);
  }
}
