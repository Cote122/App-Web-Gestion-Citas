namespace GestionCitas.Api.Models;

public enum AppointmentStatus
{
    Confirmada,
    Cancelada,
    Reprogramada,
    Atendida
}

public enum WaitlistStatus
{
    Activa,
    Asignada,
    Notificada,
    Cerrada
}

public enum AttendanceConfirmationStatus
{
    Pendiente,
    Confirmada,
    NoAsistira
}

public enum AppointmentChangeRequestType
{
    Cancelacion,
    Reprogramacion
}

public enum AppointmentChangeRequestStatus
{
    PendienteAprobacion,
    Aprobada,
    Rechazada
}
