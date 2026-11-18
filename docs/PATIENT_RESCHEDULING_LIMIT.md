# HU-43 — Dos reprogramaciones por cita

[SCRUM-205](https://plataforma-gestion-citas.atlassian.net/browse/SCRUM-205). Máximo confirmado por el responsable: **2 cambios realizados por el paciente por cadena de cita**.

El paciente ve en Mis citas y en la selección del nuevo horario el máximo, los cambios usados y los disponibles. Al llegar a dos el botón se deshabilita y la API rechaza una tercera operación, aunque se invoque directamente. Los intentos fallidos no consumen cuota ni cambian horarios, notificaciones o auditoría.

Se recorre PreviousAppointmentId y se cuentan los eventos históricos Rescheduled del paciente. Un nuevo GUID/código de cita o un cambio del profesional conservan la cuota. Los cambios realizados por el profesional no generan ese evento ni consumen intentos del paciente. Una reserva independiente empieza su propia cadena.

No se agregan tablas ni columnas: se reutilizan Appointments y AppointmentAuditLogs del flujo HU-09. Se incluyen eventos previos que ya estén registrados; no se inventa la autoría de cambios antiguos sin auditoría. Cadenas con enlaces ausentes, a otro paciente o cíclicos bloquean la reprogramación hasta revisar el historial.

Configuración: AppointmentPolicy:MaxPatientReschedules = 2 en appsettings.json; puede sobrescribirse mediante AppointmentPolicy__MaxPatientReschedules y reiniciar el backend. SQL Server serializa la lectura/cambio de una misma cita con UPDLOCK/HOLDLOCK en la transacción existente. No hay script SQL adicional para esta historia.

Validación manual: reservar una cita futura, reprogramarla dos veces y comprobar 0/2 → 1/2 → 2/2, incluida la nueva referencia de cada cita. La tercera debe bloquearse. Repetir con un cambio intermedio del profesional y verificar que el contador del paciente se conserva. Las pruebas cubren persistencia, cambios fallidos/ajenos, configuración, historial inconsistente y dos solicitudes simultáneas al último intento en SQL Server real.
