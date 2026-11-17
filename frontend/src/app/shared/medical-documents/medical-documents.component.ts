import { AfterViewInit, Component, DestroyRef, ElementRef, EventEmitter, HostListener, Input, OnDestroy, OnInit, Output, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { MedicalDocument, MedicalDocumentLimits } from '../../models/models';

@Component({
  selector: 'app-medical-documents',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './medical-documents.component.html',
  styleUrl: './medical-documents.component.css'
})
export class MedicalDocumentsComponent implements OnInit, AfterViewInit, OnDestroy {
  @Input({ required: true }) appointmentId = '';
  @Input() appointmentLabel = '';
  @Input() canUpload = true;
  @Output() closed = new EventEmitter<void>();
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;
  @ViewChild('dialog', { static: true }) dialog!: ElementRef<HTMLElement>;
  private readonly returnFocus = window.document.activeElement as HTMLElement | null;

  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  documents: MedicalDocument[] = [];
  limits?: MedicalDocumentLimits;
  file?: File;
  loading = false;
  uploading = false;
  downloadingId = '';
  error = '';
  success = '';

  ngOnInit(): void { this.load(); }

  ngAfterViewInit(): void { this.dialog.nativeElement.focus(); }
  ngOnDestroy(): void { this.returnFocus?.focus(); }

  trapFocus(event: Event): void {
    const shiftKey = (event as KeyboardEvent).shiftKey;
    const elements = Array.from(this.dialog.nativeElement.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), [tabindex="0"]'));
    const first = elements[0];
    const last = elements[elements.length - 1];
    const active = window.document.activeElement;
    if (shiftKey && (active === first || active === this.dialog.nativeElement)) {
      event.preventDefault(); last?.focus();
    } else if (!shiftKey && active === last) {
      event.preventDefault(); first?.focus();
    }
  }

  load(): void {
    this.loading = true;
    this.error = '';
    forkJoin({ documents: this.api.medicalDocuments(this.appointmentId), limits: this.api.medicalDocumentLimits() })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false))
      .subscribe({
        next: result => { this.documents = result.documents; this.limits = result.limits; },
        error: error => { void this.showError(error, 'No se pudieron consultar los documentos.'); }
      });
  }

  selectFile(event: Event): void {
    this.error = '';
    this.success = '';
    this.file = undefined;
    const input = event.target as HTMLInputElement;
    const selected = input.files?.[0];
    if (!selected || !this.limits) return;
    const extension = '.' + (selected.name.split('.').pop() ?? '').toLowerCase();
    if (!this.limits.extensions.includes(extension)) {
      this.error = 'Solo se permiten archivos PDF, JPG y PNG.';
    } else if (!selected.size || selected.size > this.limits.maxFileSizeBytes) {
      this.error = `Selecciona un archivo no vacío de hasta ${this.maxSizeMb} MB.`;
    } else {
      this.file = selected;
    }
    if (!this.file) input.value = '';
  }

  upload(): void {
    if (!this.file || !this.canUpload || this.uploading) return;
    this.uploading = true;
    this.error = '';
    this.success = '';
    this.api.uploadMedicalDocument(this.appointmentId, this.file)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.uploading = false))
      .subscribe({
        next: document => {
          this.documents = [document, ...this.documents];
          this.file = undefined;
          if (this.fileInput) this.fileInput.nativeElement.value = '';
          this.success = 'Documento guardado correctamente.';
        },
        error: error => { void this.showError(error, 'No se pudo guardar el documento.'); }
      });
  }

  download(document: MedicalDocument): void {
    if (this.downloadingId) return;
    this.downloadingId = document.id;
    this.error = '';
    this.success = '';
    this.api.downloadMedicalDocument(document.id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.downloadingId = ''))
      .subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob);
          const link = window.document.createElement('a');
          link.href = url;
          link.download = document.fileName;
          window.document.body.appendChild(link);
          link.click();
          link.remove();
          setTimeout(() => URL.revokeObjectURL(url), 1000);
          this.success = 'Descarga iniciada.';
        },
        error: error => { void this.showError(error, 'No se pudo descargar el documento.'); }
      });
  }

  get maxSizeMb(): number { return (this.limits?.maxFileSizeBytes ?? 0) / 1024 / 1024; }
  get acceptedExtensions(): string { return this.limits?.extensions.join(',') ?? ''; }

  @HostListener('document:keydown.escape')
  close(): void { if (!this.uploading) this.closed.emit(); }

  private async showError(error: any, fallback: string): Promise<void> {
    if (error?.status === 413) {
      this.error = 'El archivo supera el límite de carga del servidor.';
      return;
    }
    let body = error?.error;
    if (body instanceof Blob) {
      try { body = JSON.parse(await body.text()); } catch { body = undefined; }
    }
    this.error = body?.message ?? (error?.status === 0 ? 'No hay conexión con el servidor. Intenta nuevamente.' : fallback);
  }
}
