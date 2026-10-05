export interface User {
  id: string;
  dni: string;
  email: string;
  fullName: string;
  role: 'Paciente' | 'Profesional' | 'Administrador' | 'Caja';
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: User;
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

export interface AvailabilitySlot {
  id: string;
  startAt: string;
  endAt: string;
}

export interface Appointment {
  id: string;
  code: string;
  specialty: string;
  professional: string;
  office: string;
  startAt: string;
  endAt: string;
  status: string;
  price: number;
}
