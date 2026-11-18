import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ClinicalResult, ClinicalResultFile, MedicalDocumentLimits, SaveClinicalResult } from '../../models/models';

@Component({
  selector: 'app-results', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './results.component.html', styleUrl: './results.component.css'
})
export class ResultsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private savedForm = '';
  readonly appointmentId = this.route.snapshot.paramMap.get('appointmentId');
  results: ClinicalResult[] = [];
  selected?: ClinicalResult;
  limits?: MedicalDocumentLimits;
  canCreate = false;
  creating = false;
  patientName = '';
  loading = false;
  detailLoading = false;
  busy = false;
  downloadingId = '';
  error = '';
  success = '';
  search = '';
  type = '';
  form = { resultType: 'Laboratorio', title: '', summary: '', resultDate: '' };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.error = '';
    this.results = [];
    this.selected = undefined;
    this.creating = false;
    if (this.appointmentId) {
      forkJoin({ results: this.api.appointmentResults(this.appointmentId),
        limits: this.api.medicalDocumentLimits(), appointment: this.api.professionalAppointmentDetail(this.appointmentId) })
        .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false))
        .subscribe({ next: data => {
          this.results = data.results; this.limits = data.limits; this.patientName = data.appointment.patient.fullName;
          this.canCreate = ['Confirmada', 'Atendida'].includes(data.appointment.status);
        }, error: error => { void this.showError(error, 'No se pudieron consultar los resultados de la cita.'); } });
    } else {
      this.api.myClinicalResults().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false))
        .subscribe({ next: data => {
          this.results = data;
          const id = this.route.snapshot.queryParamMap.get('resultado');
          if (id) this.open(id);
        }, error: error => { void this.showError(error, 'No se pudieron consultar tus resultados.'); } });
    }
  }

  get types(): string[] { return [...new Set(this.results.map(x => x.resultType))].sort(); }
  get hasUnsavedChanges(): boolean { return !!this.selected && JSON.stringify(this.form) !== this.savedForm; }
  get filtered(): ClinicalResult[] {
    const query = this.search.trim().toLocaleLowerCase();
    return this.results.filter(x => (!this.type || x.resultType === this.type) &&
      (!query || `${x.title} ${x.resultType}`.toLocaleLowerCase().includes(query)));
  }

  open(id: string): void {
    if (this.busy || this.detailLoading) return;
    this.error = ''; this.success = ''; this.selected = undefined; this.creating = false; this.detailLoading = true;
    this.api.clinicalResult(id).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.detailLoading = false))
      .subscribe({ next: result => { this.selected = result; this.fillForm(result); },
        error: error => { void this.showError(error, 'No se pudo consultar el resultado.'); } });
  }

  startNew(): void {
    if (!this.canCreate || this.busy) return;
    this.selected = undefined; this.creating = true; this.error = ''; this.success = '';
    this.form = { resultType: 'Laboratorio', title: '', summary: '', resultDate: new Date().toLocaleDateString('en-CA') };
  }

  private fillForm(result: ClinicalResult): void {
    const date = result.resultDate ? new Date(result.resultDate) : undefined;
    this.form = { resultType: result.resultType, title: result.title, summary: result.summary ?? '',
      resultDate: date ? `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}` : '' };
    this.savedForm = JSON.stringify(this.form);
  }

  save(): void {
    if (this.busy || (!this.creating && !this.selected?.canManage)) return;
    const payload: SaveClinicalResult = { resultType: this.form.resultType.trim(), title: this.form.title.trim(),
      summary: this.form.summary.trim() || null,
      resultDate: this.form.resultDate ? new Date(`${this.form.resultDate}T12:00:00`).toISOString() : null };
    if (!payload.title || !payload.resultType) return;
    this.busy = true; this.error = ''; this.success = '';
    const request = this.creating
      ? this.api.createClinicalResult(this.appointmentId!, payload)
      : this.api.updateClinicalResult(this.selected!.id, payload);
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
      next: result => { this.upsert(result); this.creating = false; this.success = 'Resultado guardado como pendiente. Puedes adjuntar un informe y publicarlo cuando esté listo.'; },
      error: error => { void this.showError(error, 'No se pudo guardar el resultado.'); }
    });
  }

  publication(): void {
    if (!this.selected?.canManage || this.busy || this.hasUnsavedChanges) return;
    this.busy = true; this.error = ''; this.success = '';
    this.api.publishClinicalResult(this.selected.id, this.selected.status !== 'Publicado')
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: result => { this.upsert(result); this.success = result.status === 'Publicado'
          ? 'Resultado publicado y disponible para el paciente.' : 'Publicación retirada. El paciente ya no puede consultar ni descargar este resultado.'; },
        error: error => { void this.showError(error, 'No se pudo cambiar la publicación.'); }
      });
  }

  upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || !this.selected?.canManage || this.busy || !this.limits || this.hasUnsavedChanges) return;
    const extension = '.' + (file.name.split('.').pop() ?? '').toLowerCase();
    this.error = ''; this.success = '';
    if (!this.limits.extensions.includes(extension) || !file.size || file.size > this.limits.maxFileSizeBytes) {
      this.error = `Selecciona un PDF, JPG o PNG no vacío de hasta ${this.limits.maxFileSizeBytes / 1024 / 1024} MB.`;
      return;
    }
    this.busy = true;
    this.api.uploadClinicalResultFile(this.selected.id, file)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
        next: added => {
          if (!this.selected) return;
          this.upsert({ ...this.selected, files: [added, ...this.selected.files] }); this.success = 'Informe adjuntado. Publica el resultado cuando esté listo.';
        }, error: error => { void this.showError(error, 'No se pudo adjuntar el informe.'); }
      });
  }

  download(file: ClinicalResultFile): void {
    if (!this.selected || this.downloadingId || this.busy) return;
    this.downloadingId = file.id; this.error = ''; this.success = '';
    this.api.downloadClinicalResultFile(this.selected.id, file.id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.downloadingId = '')).subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a'); link.href = url; link.download = file.fileName;
          document.body.appendChild(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
          this.success = 'Descarga iniciada.';
        }, error: error => { void this.showError(error, 'No se pudo descargar el informe.'); }
      });
  }

  private upsert(result: ClinicalResult): void {
    this.selected = result; this.fillForm(result);
    this.results = [result, ...this.results.filter(x => x.id !== result.id)];
  }

  private async showError(error: any, fallback: string): Promise<void> {
    let body = error?.error;
    if (body instanceof Blob) { try { body = JSON.parse(await body.text()); } catch { body = undefined; } }
    this.error = body?.message ?? (error?.status === 404 ? 'Resultado no disponible o sin permiso de acceso.'
      : error?.status === 413 ? 'El archivo supera el límite de carga del servidor.'
      : error?.status === 0 ? 'No hay conexión con el servidor. Intenta nuevamente.' : fallback);
  }
}
