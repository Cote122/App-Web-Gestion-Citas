export type UserRole = 'Paciente' | 'Profesional' | 'Administrador' | 'Caja';

export interface PrescriptionItem { medication: string; dose: string | null; frequency: string | null; duration: string | null; instructions: string | null; }
export interface Prescription { id: string; issuedAt: string; professional: string; generalIndications: string | null; isActive: boolean; canDeactivate: boolean; items: PrescriptionItem[]; }
export interface PatientIndication { id: string; attentionDate: string; professional: string; content: string; }
export interface PrescriptionDraft { generalIndications: string | null; items: PrescriptionItem[]; }
export interface AdminPrescription { id: string; issuedAt: string; patient: string; patientDni: string; professional: string; isActive: boolean; generalIndications: string | null; items: PrescriptionItem[]; version: string; }
export interface PrescriptionCorrection { id: string; correctedAt: string; administrator: string; reason: string; }
export interface CorrectPrescription { expectedVersion: string; reason: string; content: PrescriptionDraft; }

export interface ClinicalResultFile {
  id: string; fileName: string; contentType: string; sizeBytes: number | null; uploadedAt: string;
}
export interface ClinicalResult {
  id: string; resultType: string; title: string; summary: string | null; status: string;
  resultDate: string | null; publishedAt: string | null; isVisibleToPatient: boolean; canManage: boolean;
  files: ClinicalResultFile[];
}
export interface SaveClinicalResult {
  resultType: string; title: string; summary: string | null; resultDate: string | null;
}

export interface MedicalDocument {
  id: string;
  appointmentId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number | null;
  createdAt: string;
  isVisibleToPatient: boolean;
  canChangeVisibility: boolean;
}

export interface MedicalDocumentLimits {
  maxFileSizeBytes: number;
  extensions: string[];
}

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
  // HU-44: correo y DNI de la cuenta con la que inicia sesión (null en profesionales antiguos de prueba).
  userEmail: string | null;
  userDni: string | null;
}

export interface AvailabilitySlot {
  id: string;
  startAt: string;
  endAt: string;
  isAvailable: boolean;
}

export interface WeeklyScheduleEntry {
  dayOfWeek: number; // Domingo=0, lunes=1 ... (hora de Perú).
  startTime: string;
  isEnabled: boolean;
}

export interface ProfessionalSchedule {
  id: string;
  fullName: string;
  scheduleConfigured: boolean;
  slots: WeeklyScheduleEntry[];
  createdSlots?: number;
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
  rescheduling: { maximum: number; used: number; remaining: number; canReschedule: boolean; historyComplete: boolean } | null;
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
  professional?: string;
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

export interface AdminAppointmentChangeRequest {
  id: string;
  appointmentId: string;
  appointmentCode: string;
  patientName: string;
  professionalName: string;
  specialtyName: string;
  type: string;
  status: string;
  reason: string | null;
  originalSlotStartAt: string;
  targetSlotStartAt: string | null;
  createdAt: string;
}

export interface AppointmentChangeRequest {
  id: string;
  appointmentId: string;
  appointmentCode: string;
  type: string;
  status: string;
  reason: string | null;
  targetSlotId: string | null;
  originalSlotStartAt: string;
  targetSlotStartAt: string | null;
  createdAt: string;
  reviewedAt: string | null;
  adminComment: string | null;
}