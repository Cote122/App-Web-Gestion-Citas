# HU-16 — Recetas e indicaciones

[SCRUM-27](https://plataforma-gestion-citas.atlassian.net/browse/SCRUM-27). El paciente consulta Recetas e indicaciones: recetas activas, fecha de emisión, profesional, medicamentos y sus pautas, e indicaciones generales. También puede consultar las Recomendaciones ya registradas en MedicalRecords de sus citas atendidas. Son fuentes diferenciadas en pantalla; no se exponen notas privadas, observaciones o diagnósticos.

El profesional abre Mi agenda → Recetas. Para emitir una receta la cita debe estar Atendida y tener una atención clínica registrada. Puede registrar hasta 20 medicamentos con nombre (obligatorio por línea), dosis, frecuencia, duración e instrucciones, o emitir solamente indicaciones generales. El profesional introduce esos textos; no hay selección ni cálculo automático de tratamientos.

La emisión crea una receta activa con fecha del servidor y pacientes/profesionales derivados de la cita, ignorando identificadores enviados por el cliente. El paciente no tiene endpoints de edición. Para corregir una receta emitida, el profesional puede anularla y emitir una nueva; la anterior se conserva para él y deja de estar accesible al paciente. La consulta/retirada de recetas requiere pertenencia y coherencia entre paciente, profesional, atención y cita.

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
