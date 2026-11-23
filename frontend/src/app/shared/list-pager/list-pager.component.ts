import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-list-pager', standalone: true, imports: [CommonModule],
  template: `<nav class="pager" *ngIf="pages > 1" aria-label="Paginación de registros">
    <button class="btn btn-secondary" type="button" [disabled]="page <= 1" (click)="pageChange.emit(page - 1)">Anterior</button>
    <span>Página {{ page }} de {{ pages }}</span>
    <button class="btn btn-secondary" type="button" [disabled]="page >= pages" (click)="pageChange.emit(page + 1)">Siguiente</button>
  </nav>`,
  styles: [`.pager { display:flex; flex-wrap:wrap; align-items:center; justify-content:center; gap:12px; padding:14px 0; color:var(--muted); font-size:13px; } .pager .btn { min-height:38px; }`]
})
export class ListPagerComponent {
  @Input() page = 1;
  @Input() total = 0;
  @Input() pageSize = 8;
  @Output() pageChange = new EventEmitter<number>();
  get pages(): number { return Math.max(1, Math.ceil(this.total / this.pageSize)); }
}
