import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AbsenceType, AdminProfessional, AdminSpecialty, AdminUser, AffectedAppointment, Appointment, AvailabilitySlot, CreateAbsenceResponse, MedicalRecord, Professional, ProfessionalAbsence, ProfessionalAgendaItem, ProfessionalAppointmentDetail, RoleDefinition, Specialty, UserRole, WaitlistItem, WaitlistResponse } from '../models/models';
import { Observable } from 'rxjs';
import { MedicalDocument, MedicalDocumentLimits } from '../models/models';
import { ClinicalResult, ClinicalResultFile, SaveClinicalResult } from '../models/models';
import { Prescription, PrescriptionItem, PatientIndication } from '../models/models';
import { AdminPrescription, CorrectPrescription, PrescriptionCorrection } from '../models/models';
@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private readonly http: HttpClient) {}
  adminPrescriptions(patientDni: string) { return this.http.get<AdminPrescription[]>('/api/admin/prescriptions', { params: { patientDni } }); }
  adminPrescription(id: string) { return this.http.get<AdminPrescription>(`/api/admin/prescriptions/${id}`); }
  prescriptionCorrections(id: string) { return this.http.get<PrescriptionCorrection[]>(`/api/admin/prescriptions/${id}/corrections`); }
  correctPrescription(id: string, request: CorrectPrescription) { return this.http.put<AdminPrescription>(`/api/admin/prescriptions/${id}`, request); }
  myPrescriptions() { return this.http.get<Prescription[]>('/api/prescriptions/mine'); }
  myIndications() { return this.http.get<PatientIndication[]>('/api/prescriptions/indications/mine'); }
  prescription(id: string) { return this.http.get<Prescription>(`/api/prescriptions/${id}`); }
  indication(id: string) { return this.http.get<PatientIndication>(`/api/prescriptions/indications/${id}`); }
  appointmentPrescriptions(id: string) { return this.http.get<Prescription[]>(`/api/prescriptions/appointments/${id}`); }
  issuePrescription(id: string, payload: { generalIndications: string | null; items: PrescriptionItem[] }) { return this.http.post<Prescription>(`/api/prescriptions/appointments/${id}`, payload); }
  deactivatePrescription(id: string) { return this.http.delete<Prescription>(`/api/prescriptions/${id}`); }

  myClinicalResults() { return this.http.get<ClinicalResult[]>('/api/clinical-results/mine'); }
  appointmentResults(id: string) { return this.http.get<ClinicalResult[]>(`/api/clinical-results/appointments/${id}`); }
  clinicalResult(id: string) { return this.http.get<ClinicalResult>(`/api/clinical-results/${id}`); }
  createClinicalResult(id: string, payload: SaveClinicalResult) {
    return this.http.post<ClinicalResult>(`/api/clinical-results/appointments/${id}`, payload);
  }
  updateClinicalResult(id: string, payload: SaveClinicalResult) {
    return this.http.put<ClinicalResult>(`/api/clinical-results/${id}`, payload);
  }
  publishClinicalResult(id: string, publish: boolean) {
    return this.http.patch<ClinicalResult>(`/api/clinical-results/${id}/publication`, { publish });
  }
  uploadClinicalResultFile(id: string, file: File) {
    const payload = new FormData(); payload.append('file', file);
    return this.http.post<ClinicalResultFile>(`/api/clinical-results/${id}/files`, payload);
  }
  downloadClinicalResultFile(id: string, fileId: string) {
    return this.http.get(`/api/clinical-results/${id}/files/${fileId}/download`, { responseType: 'blob' });
  }

  medicalDocumentLimits() {
    return this.http.get<MedicalDocumentLimits>('/api/medical-documents/limits');
  }

  medicalDocuments(appointmentId: string) {
    return this.http.get<MedicalDocument[]>(`/api/medical-documents/appointments/${appointmentId}`);
  }

  uploadMedicalDocument(appointmentId: string, file: File, isVisibleToPatient?: boolean) {
    const payload = new FormData();
    payload.append('file', file);
    if (isVisibleToPatient !== undefined) payload.append('isVisibleToPatient', String(isVisibleToPatient));
    return this.http.post<MedicalDocument>(`/api/medical-documents/appointments/${appointmentId}`, payload);
  }

  downloadMedicalDocument(id: string) {
    return this.http.get(`/api/medical-documents/${id}/download`, { responseType: 'blob' });
  }

  updateMedicalDocumentVisibility(id: string, isVisibleToPatient: boolean) {
    return this.http.patch<MedicalDocument>(`/api/medical-documents/${id}/visibility`, { isVisibleToPatient });
  }

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

  respondAttendance(id: string, response: 'Confirmada' | 'NoAsistira') {
    return this.http.put<Appointment>(`/api/appointments/${id}/attendance`, { response });
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

  // HU-44: registra al profesional junto con su cuenta (rol Profesional).
  createProfessional(payload: { dni: string; email: string; password: string; fullName: string; office: string; specialtyId: string }) {
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

  professionalAgenda(date?: string) {
    return this.http.get<ProfessionalAgendaItem[]>('/api/professional/agenda', { params: { date: date || '' } });
  }

  professionalAppointmentDetail(id: string) {
    return this.http.get<ProfessionalAppointmentDetail>(`/api/professional/agenda/${id}`);
  }

  getMedicalRecordByAppointment(appointmentId: string): Observable<MedicalRecord> {
    return this.http.get<MedicalRecord>(`/api/medicalrecords/appointment/${appointmentId}`);
  }

  createMedicalRecord(payload: any): Observable<any> {
    return this.http.post('/api/medicalrecords', payload);
  }

  getProfessionalAvailableSlots() {
    return this.http.get<any[]>('/api/professional/agenda/available-slots');
  }

  cancelProfessionalAppointment(appointmentId: string) {
    return this.http.patch<any>(`/api/professional/agenda/${appointmentId}/cancel`, {});
  }

  rescheduleProfessionalAppointment(appointmentId: string, slotId: string) {
    return this.http.patch<any>(`/api/professional/agenda/${appointmentId}/reschedule`, { slotId });
  }
  // HU-14: ausencias del profesional
  myAbsences() {
    return this.http.get<ProfessionalAbsence[]>('/api/professional/absences');
  }

  createAbsence(payload: { type: AbsenceType; startDate: string; endDate: string; reason: string }) {
    return this.http.post<CreateAbsenceResponse>('/api/professional/absences', payload);
  }

  absenceAffected(id: string) {
    return this.http.get<AffectedAppointment[]>(`/api/professional/absences/${id}/affected`);
  }

  cancelAbsence(id: string) {
    return this.http.delete<ProfessionalAbsence>(`/api/professional/absences/${id}`);
  }
}
