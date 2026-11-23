# Enlace de recuperación de contraseña en producción

El servicio de recuperación agrega `/reset-password?token=...` a `App:FrontendUrl`.
El archivo `backend/GestionCitas.Api/appsettings.Production.json` configura la
URL base de la web pública como `https://regal-gingersnap-c000be.netlify.app`.
Así, los correos nuevos dirigen a la pantalla de restablecimiento en Netlify.

En desarrollo se conserva `http://localhost:4200` como URL base. Si en
MonsterASP existe la variable `App__FrontendUrl`, su valor tiene prioridad:
ajustarlo a la URL base de Netlify, sin `/login` ni `/reset-password`.

Publicar nuevamente el backend y solicitar un correo de recuperación nuevo para
verificar el enlace. Los correos enviados antes de actualizar conservan su URL
anterior y los tokens vencen a los 30 minutos. No requiere cambios en SQL.
