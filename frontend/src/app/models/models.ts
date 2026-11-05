export type UserRole = 'Paciente' | 'Profesional' | 'Administrador' | 'Caja';

export interface User {
  id: string;
  dni: string;
  email: string;
  fullName: string;
  role: UserRole;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface RegisterResponse {
  message: string;
  user: User;
}

export interface AdminUser extends User {
  isActive: boolean;
}

export interface RoleDefinition {
  role: UserRole;
  permissions: string[];
}

export interface Specialty {
  id: string;
  name: string;
  description: string;
  price: number;
}

export interface Professional {
  id: string;
  fullName: string;
  office: string;
  specialtyId: string;
}

export interface AdminSpecialty {
  id: string;
  name: string;
  description: string;
  price: number;
  isActive: boolean;
  professionalCount: number;
}

export interface AdminProfessional {
  id: string;
  fullName: string;
  office: string;
  specialtyId: string;
  specialtyName: string;
  specialtyIsActive: boolean;
  isActive: boolean;
}

export interface AvailabilitySlot {
  id: string;
  startAt: string;
  endAt: string;
  isAvailable: boolean;
}

export interface WaitlistResponse {
  id: string;
  status: string;
  position: number;
  createdAt: string;
  message: string;
}

export interface WaitlistItem {
  id: string;
  slotId: string;
  specialty: string;
  professional: string;
  office: string;
  startAt: string;
  endAt: string;
  status: string;
  position: number;
  createdAt: string;
}

// HU-14: vacaciones y licencias del profesional.
export type AbsenceType = 'Vacaciones' | 'Licencia';

export interface ProfessionalAbsence {
  id: string;
  type: AbsenceType;
  startDate: string;
  endDate: string;
  reason: string;
  status: 'Programada' | 'En curso' | 'Finalizada' | 'Anulada';
  affectedCount: number;
  canCancel: boolean;
}

export interface AffectedAppointment {
  id: string;
  code: string;
  patientName: string;
  patientEmail: string;
  startAt: string;
}

export interface CreateAbsenceResponse {
  absence: ProfessionalAbsence;
  affectedAppointments: AffectedAppointment[];
}

export type AttendanceStatus = 'Pendiente' | 'Confirmada' | 'NoAsistira';

export interface Appointment {
  id: string;
  code: string;
  specialtyId: string;
  specialty: string;
  professionalId: string;
  professional: string;
  office: string;
  startAt: string;
  endAt: string;
  status: string;
  price: number;
  reason?: string; // Opcional: motivo de consulta
  attendanceStatus: AttendanceStatus;
  attendanceRequestedAt?: string | null;
  attendanceRespondedAt?: string | null;
}

export interface ProfessionalPatient {
  id: string;
  dni: string;
  fullName: string;
  email: string;
}

export interface ProfessionalAgendaItem {
  id: string;
  code: string;
  patientId: string;
  patientName: string;
  startAt: string;
  endAt: string;
  specialty: string;
  reason?: string;
  status: string;
  office: string;
}

export interface ProfessionalAppointmentDetail {
  id: string;
  code: string;
  startAt: string;
  endAt: string;
  specialty: string;
  office: string;
  reason?: string;
  status: string;
  patient: ProfessionalPatient;
}

export interface MedicalRecord {
  id: string;
  patientId: string;
  professionalId: string;
  appointmentId: string;
  reason?: string;
  observations?: string;
  diagnosis?: string;
  recommendations?: string;
  attentionDate: string;
  createdAt: string;
  updatedAt?: string;
}

