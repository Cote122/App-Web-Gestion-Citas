# Casos de prueba funcional — Sprint 2

## HU-09 — Reprogramación

### RP-01 Reprogramación exitosa
1. Iniciar sesión como paciente con una cita futura confirmada.
2. Ir a Mis citas y seleccionar Reprogramar.
3. Seleccionar un horario disponible diferente.
4. Confirmar.

**Resultado esperado**
- Se muestra alerta de éxito.
- La cita anterior aparece como `Reprogramada` en Historial.
- La nueva cita aparece en Próximas citas.
- El nuevo horario queda ocupado.
- El horario anterior se libera o pasa al primero de la lista de espera.
- `AppointmentAuditLogs` contiene evento `Rescheduled`.
- `NotificationOutbox` contiene `AppointmentRescheduled`.

### RP-02 Mismo horario o cambio de profesional
Intentar reprogramar a la misma fecha/hora o seleccionar un horario de otro profesional.

**Resultado esperado**
- La reprogramación conserva el mismo profesional y especialidad.
- Se exige una fecha u hora realmente distinta.
- La cita original permanece confirmada si la validación falla.

### RP-03 Nuevo horario ocupado al confirmar
1. Dos pacientes consultan el mismo horario disponible.
2. Uno lo reserva antes de que el otro confirme la reprogramación.
3. El segundo confirma.

**Resultado esperado**
- La reprogramación se rechaza.
- La cita original no cambia.

### RP-04 Cita no elegible
Intentar reprogramar una cita cancelada, reprogramada o cuyo horario ya inició.

**Resultado esperado**
- La operación se bloquea con un mensaje informativo.

## HU-08 — Cancelación

### CA-01 Cancelación exitosa sin lista de espera
1. Seleccionar una cita futura confirmada.
2. Presionar Cancelar.
3. Confirmar en el modal.

**Resultado esperado**
- Se muestra alerta de éxito.
- La cita pasa a Historial como `Cancelada`.
- El horario queda disponible.
- `AppointmentAuditLogs` contiene evento `Cancelled`.
- `NotificationOutbox` contiene `AppointmentCancelled`.

### CA-02 Cancelación con lista de espera
1. Paciente A tiene una cita.
2. Paciente B está primero en la lista de espera de ese horario.
3. A cancela.

**Resultado esperado**
- La cita de A queda cancelada.
- B recibe automáticamente una cita confirmada.
- El horario continúa ocupado.
- B desaparece de Lista de espera.
- Se genera `WaitlistSlotAssigned` para B.

### CA-03 Cancelación no permitida
Intentar cancelar una cita que ya no está confirmada o cuyo horario inició.

**Resultado esperado**
- La operación es rechazada y no modifica el horario.
