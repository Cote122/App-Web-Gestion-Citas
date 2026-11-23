# Base de datos

La API utiliza Entity Framework Core. En el entorno compartido del equipo ya existe una base de datos creada, por lo que **no se debe depender de `EnsureCreated()` para modificar tablas existentes**.

## Scripts

- `01_create_database.sql`: creación inicial opcional.
- `02_waitlist_notifications.sql`: actualización requerida para HU-13 (lista de espera por horario concreto + bandeja de notificaciones).
- `07_historia_medica.sql`: actualización idempotente de historial médico y motivo de consulta. Crea los objetos que faltan, conserva las claves foráneas existentes de auditoría sin depender de sus nombres y asegura un índice no único en la auditoría.
- `08_corregir_indice_auditoria.sql`: reparación idempotente para bases donde ya se ejecutó la versión anterior de `07_historia_medica.sql`. Sustituye el índice único por uno no único, sin eliminar datos.
- `09_documentos_medicos.sql`: valida el catálogo clínico de 13 columnas de HU-18 y agrega índices no únicos; crea la tabla si falta. Conserva columnas y registros existentes. Ejecutar antes de publicar el manejo de documentos; ver [configuración y pruebas](../docs/MEDICAL_DOCUMENTS.md).
- `10_resultados_clinicos.sql`: valida las tres tablas clínicas existentes de HU-15 y agrega índices no únicos sin alterar datos, estados ni defaults. Requiere el catálogo clínico instalado; ver [resultados clínicos](../docs/CLINICAL_RESULTS.md).
- `11_recetas_indicaciones.sql`: valida el catálogo existente de recetas/indicaciones y agrega índices no únicos, conservando defaults y datos. Ver [HU-16](../docs/PRESCRIPTIONS.md).
- `14_horarios_administrador.sql`: agrega la agenda semanal por profesional y el estado de habilitación de cupos existentes. Ejecutar antes de publicar el backend nuevo. No afecta citas actuales; el administrador configura cada agenda desde Profesionales → Horarios.

Si ya están creados el historial y el motivo y solo falta reparar el índice, ejecutar `08_corregir_indice_auditoria.sql` y comprobar que `IsUnique = 0`. Para instalar el historial o completar una ejecución parcial, ejecutar el `07_historia_medica.sql` actualizado; su resultado debe mostrar `Reason = OK`, `MedicalRecords = OK` y `AuditIndexIsUnique = 0`. Este 07 ya incluye la reparación del índice y no necesita ejecutar el 08 a continuación.

Si una versión anterior falló después de `BEGIN TRANSACTION`, revisar `SELECT @@TRANCOUNT AS TransaccionesAbiertas, XACT_STATE() AS EstadoTransaccion` en la misma ventana de SSMS. Si la transacción abierta corresponde exclusivamente a ese intento fallido, ejecutar `ROLLBACK TRANSACTION` antes de volver a intentar. El 07 actualizado rechaza una sesión que ya tenga una transacción abierta y usa `XACT_ABORT` y `TRY/CATCH` para revertir sus propios cambios si falla.

Antes de probar la rama de HU-13 sobre la BD compartida:

```text
1. Abrir SSMS con una cuenta con permisos de ALTER/CREATE sobre GestionCitasDb.
2. Ejecutar database/02_waitlist_notifications.sql.
3. Confirmar que el resultado final muestre NotificationOutbox = OK.
4. Recién después levantar el backend.
```

El script `02_waitlist_notifications.sql` es idempotente y no elimina registros existentes.

## Migraciones EF Core

A futuro conviene migrar el proyecto a migraciones EF Core para versionar el esquema junto con el código:

```bash
cd backend/GestionCitas.Api
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

No ejecutar migraciones de forma independiente sobre una base compartida sin coordinación del equipo.

- `12_correcciones_recetas.sql`: crea y valida el historial independiente de correcciones de recetas por administrador, sin modificar columnas ni contenido de las recetas existentes. Ejecutar después del 11.
