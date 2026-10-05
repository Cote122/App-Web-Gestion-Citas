# App Web Gestión de Citas

Repositorio base para convertir el prototipo académico de **Gestión Salud** en una aplicación web real y versionable en GitHub.

## Arquitectura

```text
Angular (frontend) -> ASP.NET Core Web API -> Entity Framework Core -> SQL Server
```

El repositorio está preparado como monorepo para Visual Studio Code:

```text
App-Web-Gestion-Citas/
├─ frontend/                    # Angular
├─ backend/GestionCitas.Api/    # ASP.NET Core Web API
├─ database/                    # scripts y notas SQL Server
├─ postman/                     # colección de pruebas
├─ docs/                        # arquitectura, estado de HU y prototipo original
├─ .github/                     # plantillas de Issues / Pull Requests
└─ .vscode/                     # tareas y extensiones sugeridas
```

## Alcance del primer incremento técnico

Esta base deja funcionando o encaminado el flujo principal del paciente:

- HU-01 Registro de paciente.
- HU-03 Inicio de sesión con JWT y roles.
- HU-10 Búsqueda por especialidad/profesional.
- HU-11 Consulta de disponibilidad.
- HU-07 Reserva de cita con verificación de cupo.
- HU-12 Consulta de citas.
- HU-08 Cancelación.
- HU-09 Reprogramación.
- HU-13 Lista de espera.

La administración completa de roles/permisos y el CRUD de profesionales/especialidades quedan como siguiente incremento; la base de autorización por rol ya está preparada.

## Requisitos locales

- Visual Studio Code.
- Git.
- Node.js 22 o compatible.
- .NET 10 SDK.
- SQL Server (Developer, Express o una instancia accesible por el equipo).

> Si un integrante todavía usa otro SDK de .NET, puede cambiar `net10.0` en el `.csproj`, pero el equipo debe acordar una sola versión antes de iniciar el Sprint 1.

## 1. Abrir el proyecto

En VS Code, abrir `GestionCitas.code-workspace` o la carpeta raíz.

Extensiones sugeridas en `.vscode/extensions.json`:

- C# Dev Kit
- Angular Language Service
- SQL Server (mssql)
- Prettier

## 2. Configurar SQL Server

La conexión de ejemplo está en:

`backend/GestionCitas.Api/appsettings.json`

Por defecto:

```text
Server=localhost;Database=GestionCitasDb;Trusted_Connection=True;TrustServerCertificate=True;
```

Para SQL Express puede usarse, por ejemplo:

```text
Server=.\\SQLEXPRESS;Database=GestionCitasDb;Trusted_Connection=True;TrustServerCertificate=True;
```

En desarrollo la API usa `EnsureCreated()` y carga datos ficticios la primera vez. El script `database/01_create_database.sql` es opcional si se desea crear la base manualmente.

## 3. Configurar secreto JWT local

Copiar:

```text
backend/GestionCitas.Api/appsettings.Development.example.json
```

a:

```text
backend/GestionCitas.Api/appsettings.Development.json
```

Ese archivo está ignorado por Git.

## 4. Ejecutar backend

```bash
cd backend/GestionCitas.Api
dotnet restore
dotnet run
```

API local: `http://localhost:5099`

Health check:

```text
GET http://localhost:5099/api/health
```

## 5. Ejecutar frontend

En otra terminal:

```bash
cd frontend
npm install
npm start
```

Aplicación: `http://localhost:4200`

El proxy de Angular redirige `/api` al backend local.

## Usuarios demo

| Rol | DNI / usuario | Contraseña |
|---|---|---|
| Paciente | `70000011` | `Demo#2026` |
| Administrador | `70000001` | `Demo#2026` |
| Admisión / Caja | `70000002` | `Demo#2026` |
| Profesional | `70000003` | `Demo#2026` |

Todos son datos ficticios.

## Flujo para subir a GitHub

Desde la raíz:

```bash
git init
git add .
git commit -m "chore: crear estructura inicial de Gestion de Citas"
git branch -M main
git remote add origin https://github.com/ORGANIZACION/app-web-gestion-citas.git
git push -u origin main
```

Después, cada integrante debería trabajar en una rama por historia o tarea, por ejemplo:

```bash
git checkout -b feature/HU-07-reserva-cita
```

No subir secretos, contraseñas reales, archivos `.env`, `appsettings.Development.json` ni información clínica real.

## Qué sigue para Scrum

El repositorio ya incluye `docs/scrum/README.md`, las plantillas de GitHub y `docs/IMPLEMENTATION_STATUS.md`. El siguiente paso es convertir las HU priorizadas en Issues, estimarlas con Story Points y distribuirlas entre los cuatro sprints de desarrollo.
