import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, OnDestroy, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PrescriptionItem } from '../../models/models';

@Component({ selector: 'app-prescription-review', standalone: true, imports: [CommonModule],
  templateUrl: './prescription-review.component.html', styleUrl: './prescription-review.component.css' })
export class PrescriptionReviewComponent implements AfterViewInit, OnDestroy {
  @Input() title = 'Verificar receta antes de emitir';
  @Input() patient = '';
  @Input() professional = '';
  @Input() generalIndications: string | null = null;
  @Input() items: PrescriptionItem[] = [];
  @Input() note = '';
  @Input() warning = 'Revisa cuidadosamente los datos. Una vez emitida, solo un administrador podrá corregir el contenido de esta receta.';
  @Input() confirmText = 'Confirmar emisión';
  @Input() busy = false;
  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
  @ViewChild('dialog', { static: true }) dialog!: ElementRef<HTMLElement>;
  private readonly returnFocus = document.activeElement as HTMLElement | null;
  ngAfterViewInit(): void { this.dialog.nativeElement.focus(); }
  ngOnDestroy(): void { if (this.returnFocus?.isConnected) this.returnFocus.focus(); }
  @HostListener('document:keydown.escape') cancel(): void { if (!this.busy) this.cancelled.emit(); }
  confirm(): void { if (!this.busy) this.confirmed.emit(); }
  trapFocus(event: Event): void {
    const buttons = Array.from(this.dialog.nativeElement.querySelectorAll<HTMLButtonElement>('button:not([disabled])'));
    const first = buttons[0], last = buttons[buttons.length - 1];
    const active = document.activeElement;
    if (!first) { event.preventDefault(); return; }
    if ((event as KeyboardEvent).shiftKey && (active === first || active === this.dialog.nativeElement)) { event.preventDefault(); last.focus(); }
    else if (!(event as KeyboardEvent).shiftKey && (active === last || active === this.dialog.nativeElement)) { event.preventDefault(); first.focus(); }
  }
}
