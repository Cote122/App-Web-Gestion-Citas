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
}
