# Comunicaciones de citas — HU-20 / HU-23

## Configuración

`backend/GestionCitas.Api/appsettings.json`:

```json
"Communications": {
  "ReminderWindowHours": 24,
  "AttendanceConfirmationWindowHours": 48,
  "ProcessIntervalMinutes": 5
}
```

- **ReminderWindowHours**: cuántas horas antes de la cita se genera el recordatorio.
- **AttendanceConfirmationWindowHours**: cuántas horas antes se solicita confirmación de asistencia.
- **ProcessIntervalMinutes**: frecuencia con la que el proceso automático revisa citas elegibles.

Los valores pueden sobrescribirse en `appsettings.Development.json` sin recompilar.

## HU-20 — Recordatorios

El worker solo considera citas:

- Estado `Confirmada`.
- Futuras.
- Dentro de `ReminderWindowHours`.

Excluye automáticamente citas canceladas, reprogramadas o atendidas.

Genera una fila única por cita:

```text
Type = AppointmentReminder
Status = Pendiente
RelatedAppointmentId = <Id de cita>
```

La restricción única `Type + RelatedAppointmentId` evita recordatorios duplicados aunque el worker corra repetidamente.

## HU-23 — Confirmación de asistencia

Cuando una cita confirmada entra en la ventana de confirmación:

1. Se registra `AttendanceRequestedAt`.
2. Se genera `AttendanceConfirmationRequest` en `NotificationOutbox`.
3. En Mis citas aparecen:
   - `Sí, asistiré`
   - `No asistiré`

Estados:

```text
Pendiente
Confirmada
NoAsistira
```

`NoAsistira` no cancela automáticamente la cita. La respuesta queda visible para gestión operativa.

## Endpoints administrativos

Procesar inmediatamente las citas elegibles:

```http
POST /api/admin/communications/process
```

Solicitar manualmente confirmación de una cita futura confirmada:

```http
POST /api/admin/communications/appointments/{appointmentId}/request-attendance
```

Consultar estado operativo:

```http
GET /api/admin/communications/attendance
```

## Canal de correo

Hasta integrar Brevo/Resend/Mailjet, las comunicaciones quedan en `NotificationOutbox` con estado `Pendiente`. La lógica de elegibilidad, deduplicación y respuesta ya funciona independientemente del proveedor.
