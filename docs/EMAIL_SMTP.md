# SMTP seguro — GoDaddy Professional Email powered by Titan

Cuenta de envío del proyecto:

```text
no-reply@mu-tante.com
```

## Servidor

Configuración publicada por GoDaddy para Professional Email powered by Titan:

```text
SMTP host: smtpout.secureserver.net
Port: 465
Security: SSL/TLS
Authentication: required
```

La contraseña nunca debe almacenarse en GitHub, Angular ni `appsettings.json`.

## Configuración versionada

`appsettings.json` contiene únicamente valores no secretos y el envío está desactivado por defecto:

```json
"Email": {
  "Enabled": false,
  "AutomaticDispatchEnabled": false,
  "SmtpHost": "smtpout.secureserver.net",
  "SmtpPort": 465,
  "UseSsl": true,
  "Username": "",
  "Password": "",
  "FromAddress": "no-reply@mu-tante.com",
  "FromName": "Gestión de Citas",
  "TestRecipientOverride": "",
  "ProcessIntervalSeconds": 30,
  "BatchSize": 20
}
```

## Desarrollo local — .NET User Secrets

El proyecto ya tiene `UserSecretsId`.

Desde `backend/GestionCitas.Api`:

```powershell
dotnet user-secrets set "Email:Username" "no-reply@mu-tante.com"
dotnet user-secrets set "Email:Password" "TU_PASSWORD_REAL"
dotnet user-secrets set "Email:Enabled" "true"
```

No activar todavía:

```text
Email:AutomaticDispatchEnabled
```

Debe permanecer en `false` durante la primera prueba para que no se envíen las filas históricas de `NotificationOutbox`.

## Prueba SMTP controlada

Iniciar sesión como administrador y ejecutar:

```http
POST /api/admin/communications/test-email
Content-Type: application/json

{
  "email": "correo-real-de-prueba@dominio.com"
}
```

Resultado esperado:

```json
{
  "message": "Correo de prueba enviado correctamente."
}
```

Esta operación envía un único mensaje y no procesa `NotificationOutbox`.

## Antes de activar el envío automático

La base de desarrollo contiene correos ficticios y filas históricas `Pendiente`.
Antes de activar el worker se deben revisar y archivar esas filas de prueba.

Consulta:

```sql
SELECT Id, RecipientEmail, Type, Status, CreatedAt
FROM dbo.NotificationOutbox
WHERE Status = 'Pendiente'
ORDER BY CreatedAt;
```

Solo cuando se confirme que esas filas son de pruebas puede usarse:

```sql
UPDATE dbo.NotificationOutbox
SET
    Status = 'Omitido',
    LastError = 'Registro de prueba anterior a la activación SMTP.'
WHERE Status = 'Pendiente';
```

Luego habilitar:

```powershell
dotnet user-secrets set "Email:AutomaticDispatchEnabled" "true"
```

A partir de ese momento, nuevas filas `Pendiente` se enviarán por SMTP y pasarán a:

```text
Enviado
SentAt = hora Perú
LastError = NULL
```

Si ocurre un error:

```text
Error
SentAt = NULL
LastError = detalle técnico
```

## Producción

En producción usar variables de entorno del servidor, nunca archivos versionados:

```text
Email__Enabled=true
Email__AutomaticDispatchEnabled=true
Email__Username=no-reply@mu-tante.com
Email__Password=<secreto>
```

Los demás valores pueden permanecer en `appsettings.json`.

## Netlify

El frontend Angular puede alojarse en Netlify, pero el secreto SMTP no debe estar allí si el envío lo realiza ASP.NET Core.
La contraseña debe configurarse exclusivamente en el entorno donde se ejecute la API.
