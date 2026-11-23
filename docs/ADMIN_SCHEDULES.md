# Horarios semanales por profesional

El administrador abre **Profesionales → Horarios** (o **Horarios profesionales** en el menú), elige un profesional y configura cada día de la semana. Puede añadir una hora de inicio, cambiarla en pasos de 15 minutos, deshabilitarla o quitarla. Cada cupo dura 45 minutos. Una agenda vacía deshabilita nuevos cupos para ese profesional.

Antes de publicar el backend, ejecutar `database/14_horarios_administrador.sql` sobre `GestionCitasDb`. El script conserva los cupos y citas existentes y se puede repetir. Los profesionales sin agenda guardada mantienen el horario inicial (lunes a sábado a las 09:00, 10:00, 11:00, 15:00 y 16:00). El antiguo script `13_reponer_horarios_profesionales.sql` no debe ejecutarse después de instalar el 14: está hecho para horas fijas. La API genera los próximos 21 días al arrancar, al guardar una agenda y cada seis horas.

En instalaciones nuevas con la tabla `Users` vacía, se debe definir `GESTIONCITAS_DEMO_PASSWORD` en el entorno antes de iniciar la API para crear usuarios de demostración. En una base existente no se cambian sus contraseñas. Como había una contraseña de prueba publicada en el código anterior, conviene cambiar las contraseñas de cualquier cuenta de prueba que se haya creado con ella.

Al guardar una agenda, los cupos futuros que ya no correspondan se ocultan de la reserva y reprogramación, incluidas llamadas directas a la API. Las citas confirmadas conservan su fecha y hora; el generador evita crear otro cupo superpuesto a una cita anterior. Se cierran las listas de espera pendientes de cupos retirados y se encola un correo de aviso. Los periodos de vacaciones y licencia siguen excluyendo cupos, aunque el día figure habilitado en la agenda semanal.

## Prueba manual

1. Con administrador, abrir los horarios de un profesional y guardar solo lunes 08:00 y miércoles 15:00.
2. Con paciente, comprobar que aparecen únicamente los cupos de esos días en los próximos 21 días, salvo ausencias. Intentar reservar por ID un cupo anterior deshabilitado debe fallar.
3. Con una cita ya confirmada, quitar su antigua hora semanal: la cita sigue en Mis citas y Mi agenda; no aparece un cupo nuevo superpuesto a ella.
4. Cambiar de nuevo la agenda y habilitar una hora: se crean los cupos futuros faltantes, sin duplicados. Guardar de nuevo no debe crearlos otra vez.
5. Como profesional, comprobar que al reprogramar solo se ofrecen cupos habilitados sin ausencias.
