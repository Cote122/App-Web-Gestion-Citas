# Base de datos

La API utiliza Entity Framework Core. En el entorno compartido del equipo ya existe una base de datos creada, por lo que **no se debe depender de `EnsureCreated()` para modificar tablas existentes**.

## Scripts

- `01_create_database.sql`: creación inicial opcional.
- `02_waitlist_notifications.sql`: actualización requerida para HU-13 (lista de espera por horario concreto + bandeja de notificaciones).
- `07_historia_medica.sql`: incorpora historial médico y motivo de consulta. La versión corregida crea un índice no único en la auditoría.
- `08_corregir_indice_auditoria.sql`: reparación idempotente para bases donde ya se ejecutó la versión anterior de `07_historia_medica.sql`. Sustituye el índice único por uno no único, sin eliminar datos.

Antes de desplegar la corrección de historial médico/auditoría sobre una base existente, ejecutar `08_corregir_indice_auditoria.sql` y comprobar que `IsUnique = 0`. No volver a ejecutar `07_historia_medica.sql` en una base donde ya se aplicó: contiene operaciones de creación de tablas y columnas.

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
