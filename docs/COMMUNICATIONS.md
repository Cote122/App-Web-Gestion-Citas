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

## Canal de correo compartido

Los recordatorios y solicitudes de confirmación generados por `AppointmentCommunicationService` se guardan en `NotificationOutbox` con estado `Pendiente`.

Los avisos del profesional de HU-22/HU-41, incorporados en el PR #13, también usan esa tabla:

| Acción | Productor | Type |
| --- | --- | --- |
| Cancelación por el profesional | `ProfessionalAgendaService.CancelAppointmentAsync` | `AppointmentCancelled` |
| Reprogramación por el profesional | `ProfessionalAgendaService.RescheduleAppointmentAsync` | `AppointmentRescheduled` |

Ambos métodos crean la notificación en la misma transacción que modifica la cita. No envían correo directamente ni usan otro proveedor.

`EmailOutboxWorker` llama a `EmailOutboxService.ProcessPendingAsync`, que selecciona las filas `Pendiente` sin filtrar por `Type`. Por eso procesa los avisos del profesional junto con las demás comunicaciones. El envío usa `SmtpEmailSender` (MailKit) y la configuración SMTP existente. El estado cambia a `Enviado` o `Error`, con `SentAt` y `LastError` según el resultado.

Para despacho automático se necesitan `Email:Enabled=true` y `Email:AutomaticDispatchEnabled=true`, además de credenciales SMTP válidas. No se deben publicar las credenciales ni activar el envío de filas históricas de prueba sin revisarlas. Ver [EMAIL_SMTP.md](EMAIL_SMTP.md).

El PR #13 reutiliza la cola y el envío existentes, aunque construye los mensajes dentro de `ProfessionalAgendaService`. La extracción de esa construcción a un servicio común sigue pendiente; la rama `refactor/notification-service` es actualmente un punto de partida, sin esa refactorización aplicada.
