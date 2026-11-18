# HU-16 — Recetas e indicaciones

[SCRUM-27](https://plataforma-gestion-citas.atlassian.net/browse/SCRUM-27). El paciente consulta Recetas e indicaciones: recetas activas, fecha de emisión, profesional, medicamentos y sus pautas, e indicaciones generales. También puede consultar las Recomendaciones ya registradas en MedicalRecords de sus citas atendidas. Son fuentes diferenciadas en pantalla; no se exponen notas privadas, observaciones o diagnósticos.

El profesional abre Mi agenda → Recetas. Para emitir una receta la cita debe estar Atendida y tener una atención clínica registrada. Puede registrar hasta 20 medicamentos con nombre (obligatorio por línea), dosis, frecuencia, duración e instrucciones, o emitir solamente indicaciones generales. El profesional introduce esos textos. Al pulsar Emitir receta aparece un resumen de paciente/DNI, emisor, medicamentos y pautas, e indicaciones. Volver a revisar conserva el formulario; Confirmar emisión guarda exactamente la copia mostrada. Mientras se guarda, la confirmación bloquea nuevos envíos y su cierre.

La emisión crea una receta activa con fecha del servidor y pacientes/profesionales derivados de la cita, ignorando identificadores enviados por el cliente. El paciente no tiene endpoints de edición. Solo el administrador puede corregir el contenido de una receta emitida. El profesional conserva la opción de anularla, sin editar su contenido; una anulada se conserva en su historial y deja de estar accesible al paciente. La consulta/retirada de recetas requiere pertenencia y coherencia entre paciente, profesional, atención y cita.

## Compatibilidad con HU-17 y la base existente

Se usan las tablas reportadas: Prescriptions (7 columnas), PrescriptionItems (8), ClinicalEncounters (12) y MedicalRecords (10). Se conserva la captura actual de HU-17: MedicalRecordsController no se modifica.

Si la cita atendida ya tiene un ClinicalEncounter coherente, se reutiliza sin cambiar su estado ni sus notas. Si no existe y la atención está registrada en MedicalRecords, al emitir se crea un ClinicalEncounter asociado como puente, con una copia de la atención y estado Cerrada. El default existente Abierta se conserva. Varias atenciones para la misma cita requieren revisión antes de emitir; no se elige una arbitrariamente. La emisión y sus ítems se guardan en una transacción; solicitudes simultáneas reutilizan el mismo encuentro.

Los registros de atención siguen siendo la fuente de recomendaciones. El puente es una copia al momento de la primera emisión: no reemplaza ni sincroniza automáticamente toda la historia clínica. HU-17, asignada a otro integrante, podrá adoptar ClinicalEncounters sin cambiar el contrato de recetas.

Ejecutar database/11_recetas_indicaciones.sql antes de probar/publicar. Valida el esquema y agrega índices no únicos; no crea el catálogo ausente ni modifica columnas, defaults, claves, CHECK o registros. Utiliza COLLATE CATALOG_DEFAULT para la base parcialmente contenida. Resultado esperado: GestionCitasDb | OK | OK | OK | OK.

## Medicamentos

PrescriptionItems.Medication conserva el nombre indicado por el profesional; los demás campos guardan la pauta emitida. No es necesario cargar todos los medicamentos para esta historia. Un catálogo futuro puede ofrecer autocompletado con una selección revisada, manteniendo el texto histórico de cada receta aunque el catálogo se actualice.

Fuentes consultadas: [RxNorm/RxNav APIs](https://lhncbc.nlm.nih.gov/RxNav/APIs/index.html) y [consulta de productos farmacéuticos DIGEMID](https://www.digemid.minsa.gob.pe/webDigemid/productos-farmaceuticos/). Una consulta web de DIGEMID no equivale a una API pública documentada. Esta entrega no incorpora dependencias externas para emitir o consultar recetas.

## Prueba manual

1. Aplicar SQL11 y actualizar backend/frontend de esta rama.
2. Profesional: registrar atención desde Mi agenda → Atender, incluyendo Recomendaciones, y guardar. Abrir Recetas de esa cita.
3. Emitir una receta con textos ficticios. Comprobar medicamento, dosis, frecuencia, duración e instrucciones.
4. Paciente: Recetas e indicaciones → Ver receta. Revisar fecha, profesional y contenido de solo lectura; abrir también las indicaciones de la atención.
5. Otro paciente: intentar el enlace directo a receta o indicación; debe ser 404 sin contenido clínico. Caja y Administrador reciben 403.
6. Anular como profesional y comprobar que el paciente ya no la lista ni abre mediante el enlace conservado. El historial del profesional mantiene la receta anulada.
7. Verificar datos incompletos, campos excesivos, atención ajena/no registrada y receta de otro profesional. No deben crearse registros parciales.

CI cubre autorización HTTP y SQL Server 2025 sobre el esquema nativo, defaults, colaciones, lectura/escritura, rollback ante fallo de un ítem y emisión simultánea. El flujo de recetas no requiere archivos ni cambia el almacenamiento de HU-18.

## Correcciones por administrador

Administrador → Corrección de recetas permite buscar por el DNI exacto de un paciente y abrir una receta. La búsqueda exige DNI y devuelve hasta las 50 recetas más recientes de ese paciente. Solo el administrador tiene acceso a estos endpoints; los demás roles reciben 403 incluso con una ruta directa.

El administrador corrige medicamentos/pautas e indicaciones de una receta activa, registra un motivo obligatorio y confirma un resumen. Se conservan el paciente, el profesional emisor, la atención y la fecha originales. Las recetas anuladas mantienen su contenido histórico. La corrección se guarda junto con usuario, fecha, motivo, contenido anterior y nuevo, en PrescriptionCorrections. Los ítems y la auditoría se guardan en una misma transacción. Una comprobación de versión del contenido y el bloqueo del registro rechazan escrituras obsoletas en lugar de sobrescribir otra corrección.

Ejecutar **database/12_correcciones_recetas.sql** después del 11. Crea la tabla independiente de historial sin agregar columnas a Prescriptions o PrescriptionItems, y valida su esquema al repetirlo. Resultado esperado: GestionCitasDb | OK. La pantalla muestra administrador, fecha y motivo de cada corrección; las copias completas se conservan en la tabla de historial y no se exponen en las rutas del paciente/profesional.

Pruebas manuales adicionales:

1. Profesional: completar una receta, pulsar Emitir receta y revisar el resumen y el aviso de corrección exclusiva del administrador. Volver a revisar debe conservar los valores sin emitir. Confirmar emisión debe crear una sola receta.
2. Administrador: buscar por DNI, abrirla, corregir con motivo y confirmar. Paciente: volver a abrir la receta y verificar el nuevo contenido. Profesional: comprobar que sigue sin tener edición del contenido.
3. Abrir dos veces la misma receta antes de corregir. Guardar la primera y luego intentar guardar la segunda: debe pedir volver a abrir y revisar, conservando la primera corrección.
4. Consultar el historial del administrador y comprobar que la fecha/profesional originales siguen intactos.

## Mejora para el próximo entregable

Incorporar un buscador/autocompletado de medicamentos mediante un catálogo revisado o una API oficial, después de evaluar cobertura local, permisos de uso y disponibilidad. El catálogo sugerirá nombres; la pauta será registrada por el profesional. Cada receta conservará el texto emitido aunque la fuente del catálogo cambie. Esta mejora queda fuera de la entrega actual y no bloquea la emisión.
