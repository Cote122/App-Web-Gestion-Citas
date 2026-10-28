# Reglas de negocio — Reprogramación y cancelación

Estas reglas corresponden a HU-09 y HU-08 y reflejan los criterios actualmente definidos en Jira. No se ha inventado una ventana mínima de 12/24/48 horas porque la historia no especifica una.

## HU-09 — Reprogramación

Una cita puede reprogramarse cuando:

1. Pertenece al paciente autenticado.
2. Su estado actual es `Confirmada`.
3. El horario actual todavía no ha iniciado.
4. El nuevo horario existe.
5. El nuevo horario es futuro.
6. El nuevo horario está disponible al momento de confirmar.
7. El profesional y la especialidad del nuevo horario continúan activos.
8. El nuevo horario es diferente del horario actual.

La operación se ejecuta dentro de una transacción serializable. Si falla la validación del nuevo horario, la cita original no se modifica.

Tras una reprogramación exitosa:

- La cita anterior pasa a `Reprogramada`.
- Se crea una nueva cita `Confirmada` enlazada mediante `PreviousAppointmentId`.
- El nuevo horario queda ocupado.
- El horario anterior se libera o se asigna automáticamente al primero de su lista de espera.
- Se registra un evento de auditoría.
- Se genera una notificación pendiente en `NotificationOutbox`.

## HU-08 — Cancelación

Una cita puede cancelarse cuando:

1. Pertenece al paciente autenticado.
2. Su estado actual es `Confirmada`.
3. El horario todavía no ha iniciado.

Tras una cancelación exitosa:

- La cita pasa a `Cancelada`.
- Se registra la fecha de modificación.
- El horario se libera o se asigna automáticamente al primero de la lista de espera.
- Se registra un evento de auditoría.
- Se genera una notificación pendiente en `NotificationOutbox`.

## Canal de notificación

Mientras no exista un proveedor de correo configurado, las notificaciones quedan con estado `Pendiente` en `NotificationOutbox`. La lógica de negocio no depende de Brevo, Resend, Mailjet u otro proveedor.
