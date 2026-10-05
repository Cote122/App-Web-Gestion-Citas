# Base de datos

Durante el Sprint 0 / primer incremento la API utiliza Entity Framework Core y `EnsureCreated()` para facilitar el arranque local.

Para un entorno compartido del equipo se recomienda cambiar a migraciones EF Core antes del Sprint 1:

```bash
cd backend/GestionCitas.Api
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

No ejecutar migraciones de forma independiente sobre una base compartida sin coordinación del equipo.
