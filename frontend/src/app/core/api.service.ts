import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Appointment, AvailabilitySlot, Professional, Specialty } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private readonly http: HttpClient) {}

  specialties() {
    return this.http.get<Specialty[]>('/api/specialties');
  }

  professionals(specialtyId: string) {
    return this.http.get<Professional[]>('/api/professionals', { params: { specialtyId } });
  }

  availability(professionalId: string) {
    const from = new Date().toISOString();
    const to = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString();
    return this.http.get<AvailabilitySlot[]>('/api/availability', { params: { professionalId, from, to } });
  }

  myAppointments() {
    return this.http.get<Appointment[]>('/api/appointments/mine');
  }

  createAppointment(slotId: string) {
    return this.http.post<Appointment>('/api/appointments', { slotId });
  }

  cancelAppointment(id: string) {
    return this.http.delete<void>(`/api/appointments/${id}`);
  }

  rescheduleAppointment(id: string, slotId: string) {
    return this.http.put<Appointment>(`/api/appointments/${id}/reschedule`, { slotId });
  }

  joinWaitlist(specialtyId: string, professionalId?: string) {
    return this.http.post('/api/waitlist', {
      specialtyId,
      professionalId: professionalId || null,
      preferredFrom: null,
      preferredTo: null,
      notes: 'Solicitud creada desde el flujo web inicial.'
    });
  }
}
