import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AdminProfessional, ProfessionalSchedule, WeeklyScheduleEntry } from '../../../models/models';

@Component({
  selector: 'app-admin-schedules',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './admin-schedules.component.html',
  styleUrls: ['../admin.css', './admin-schedules.component.css']
})
export class AdminSchedulesComponent implements OnInit {
  readonly days = [
    { number: 1, name: 'Lunes' }, { number: 2, name: 'Martes' },
    { number: 3, name: 'Miércoles' }, { number: 4, name: 'Jueves' },
    { number: 5, name: 'Viernes' }, { number: 6, name: 'Sábado' },
    { number: 0, name: 'Domingo' }
  ];
  professionals: AdminProfessional[] = [];
  selectedId = '';
  schedule?: ProfessionalSchedule;
  slots: WeeklyScheduleEntry[] = [];
  loading = false;
  saving = false;
  error = '';
  message = '';

  get enabledCount(): number { return this.slots.filter(x => x.isEnabled).length; }

  constructor(private readonly api: ApiService, private readonly route: ActivatedRoute) {}

  ngOnInit(): void {
    this.loading = true;
    this.api.adminProfessionals().subscribe({
      next: items => {
        this.professionals = items;
        const requested = this.route.snapshot.queryParamMap.get('professionalId');
        this.loadSchedule(items.some(x => x.id === requested) ? requested! : (items[0]?.id ?? ''));
      },
      error: () => {
        this.loading = false;
        this.error = 'No se pudieron cargar los profesionales.';
      }
    });
  }

  loadSchedule(id: string): void {
    this.selectedId = id;
    this.schedule = undefined;
    this.slots = [];
    this.message = '';
    this.error = '';
    if (!id) { this.loading = false; return; }
    this.loading = true;
    this.api.professionalSchedule(id).pipe(finalize(() => { if (this.selectedId === id) this.loading = false; })).subscribe({
      next: data => {
        if (this.selectedId !== id) return;
        this.schedule = data;
        this.slots = data.slots.map(x => ({ ...x }));
      },
      error: err => { if (this.selectedId === id) this.error = err?.error?.message || 'No se pudo cargar la agenda del profesional.'; }
    });
  }

  daySlots(day: number): WeeklyScheduleEntry[] {
    return this.slots.filter(x => x.dayOfWeek === day).sort((a, b) => a.startTime.localeCompare(b.startTime));
  }

  add(day: number): void {
    const used = new Set(this.daySlots(day).map(x => x.startTime));
    const options = Array.from({ length: 58 }, (_, i) => {
      const minutes = 9 * 60 + i * 15;
      return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
    });
    const startTime = options.find(time => !used.has(time) && this.daySlots(day)
      .filter(x => x.isEnabled)
      .every(x => Math.abs(this.minutes(x.startTime) - this.minutes(time)) >= 45)) ?? '08:00';
    this.slots.push({ dayOfWeek: day, startTime, isEnabled: true });
    this.message = '';
  }

  remove(slot: WeeklyScheduleEntry): void {
    this.slots = this.slots.filter(x => x !== slot);
    this.message = '';
  }

  save(): void {
    if (!this.schedule || this.saving) return;
    this.error = '';
    this.message = '';
    const grouped = this.days.map(day => this.daySlots(day.number));
    for (const entries of grouped) {
      if (entries.some(x => !/^([01]\d|2[0-3]):(00|15|30|45)$/.test(x.startTime) || x.startTime > '23:15') ||
          entries.some((x, index) => entries.some((y, other) => index !== other && x.startTime === y.startTime)) ||
          entries.filter(x => x.isEnabled).some((x, i, enabled) => i > 0 &&
            this.minutes(enabled[i - 1].startTime) + 45 > this.minutes(x.startTime))) {
        this.error = 'Corrige las horas duplicadas, superpuestas o inválidas antes de guardar.';
        return;
      }
    }
    this.saving = true;
    this.api.saveProfessionalSchedule(this.selectedId, this.slots)
      .pipe(finalize(() => this.saving = false))
      .subscribe({
        next: data => {
          this.schedule = data;
          this.slots = data.slots.map(x => ({ ...x }));
          this.message = `Agenda guardada. Se generaron ${data.createdSlots ?? 0} cupos para los próximos 21 días.`;
        },
        error: err => this.error = err?.error?.message || 'No se pudo guardar la agenda.'
      });
  }

  private minutes(time: string): number {
    const [hour, minute] = time.split(':').map(Number);
    return hour * 60 + minute;
  }
}
