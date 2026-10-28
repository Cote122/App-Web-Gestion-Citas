import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AdminProfessional, AdminSpecialty, AdminUser, Appointment, AvailabilitySlot, Professional, RoleDefinition, Specialty, UserRole, WaitlistItem, WaitlistResponse } from '../models/models';

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

  joinWaitlist(slotId: string) {
    return this.http.post<WaitlistResponse>('/api/waitlist', { slotId });
  }

  myWaitlist() {
    return this.http.get<WaitlistItem[]>('/api/waitlist/mine');
  }

  cancelWaitlist(id: string) {
    return this.http.delete<{ message: string }>(`/api/waitlist/${id}`);
  }

  adminSpecialties() {
    return this.http.get<AdminSpecialty[]>('/api/admin/specialties');
  }

  createSpecialty(payload: { name: string; description: string; price: number }) {
    return this.http.post<AdminSpecialty>('/api/admin/specialties', payload);
  }

  updateSpecialty(id: string, payload: { name: string; description: string; price: number }) {
    return this.http.put<AdminSpecialty>(`/api/admin/specialties/${id}`, payload);
  }

  setSpecialtyActive(id: string, isActive: boolean) {
    return this.http.patch<AdminSpecialty>(`/api/admin/specialties/${id}/status`, { isActive });
  }

  adminProfessionals() {
    return this.http.get<AdminProfessional[]>('/api/admin/professionals');
  }

  createProfessional(payload: { fullName: string; office: string; specialtyId: string }) {
    return this.http.post<AdminProfessional>('/api/admin/professionals', payload);
  }

  updateProfessional(id: string, payload: { fullName: string; office: string; specialtyId: string }) {
    return this.http.put<AdminProfessional>(`/api/admin/professionals/${id}`, payload);
  }

  setProfessionalActive(id: string, isActive: boolean) {
    return this.http.patch<AdminProfessional>(`/api/admin/professionals/${id}/status`, { isActive });
  }

  adminUsers() {
    return this.http.get<AdminUser[]>('/api/admin/users');
  }

  roleDefinitions() {
    return this.http.get<RoleDefinition[]>('/api/admin/users/roles');
  }

  updateUserRole(id: string, role: UserRole) {
    return this.http.patch<AdminUser>(`/api/admin/users/${id}/role`, { role });
  }
}
