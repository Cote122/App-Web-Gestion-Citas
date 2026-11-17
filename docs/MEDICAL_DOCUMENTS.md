# HU-18 — Documentos médicos

Pacientes y profesionales pueden adjuntar y descargar documentos de una cita autorizada desde **Mis citas → Documentos** o **Mi agenda → Documentos**.

## Reglas de esta entrega

- PDF, JPG/JPEG y PNG; máximo predeterminado **5 MiB (5 242 880 bytes)** por archivo.
- Extensión, tamaño y firma inicial del contenido se validan en el backend; el MIME del navegador no se considera confiable. Esta validación de formato no sustituye un análisis antivirus.
- Solo el paciente propietario y el profesional asignado a esa cita pueden listar, subir y descargar. Administrador y Caja no obtienen acceso clínico por su rol.
- Se permite adjuntar a citas Confirmada o Atendida. Los documentos existentes de citas Cancelada/Reprogramada se conservan y pueden descargarse por sus participantes.
- Cada documento se vincula a una cita; no queda compartido automáticamente con otros profesionales ni se copia al reprogramar. La vinculación únicamente al paciente queda fuera de esta entrega.
- Los documentos son compartidos entre el paciente y el profesional de la cita. Usar archivos ficticios en la demostración académica.
- Las cargas nuevas se guardan con `DocumentType = Documento` y título derivado del nombre. El profesional puede marcar/desmarcar **Visible para el paciente** al adjuntar (marcado inicialmente); la API guarda esa selección. Los archivos que adjunta el paciente son visibles para él; un intento de enviar `isVisibleToPatient=false` como paciente se rechaza.
- En los documentos que él mismo adjuntó a una cita que sigue teniendo asignada, el profesional dispone de **Ocultar al paciente / Mostrar al paciente**. La API verifica autor y pertenencia; el paciente, Caja, Administrador y otros profesionales no pueden cambiar esta opción. Los documentos adjuntados por el paciente conservan su visibilidad.
- Los documentos ocultos no se listan ni descargan para el paciente, incluso mediante la API. El profesional asignado mantiene acceso. Cambiar la visibilidad no modifica el archivo, su clave ni su fecha original.
- La eliminación y la edición del contenido permanecen fuera de esta HU. El ajuste de visibilidad se realiza sobre la columna existente; no necesita cambios SQL adicionales después de ejecutar el script 09 actualizado.

## Almacenamiento y permisos

SQL Server almacena los metadatos en `MedicalDocuments`: identificador, cita, usuario que adjuntó, nombre original, MIME validado, tamaño, fecha UTC y clave interna. El paciente y el profesional se resuelven desde la cita en el servidor, nunca desde identificadores enviados por el cliente.

Se utiliza el catálogo clínico existente de 13 columnas. El modelo EF conserva los nombres internos del servicio mediante este mapeo:

| Propiedad del servicio | Columna existente |
| --- | --- |
| `CreatedAt` | `UploadedAt` |
| `UploadedByUserId` | `UploadedBy` |
| `SizeBytes` (nullable) | `FileSizeBytes` |
| `StorageKey` | `StoragePath` |

Se conservan `PatientId`, `EncounterId`, `AppointmentId` nullable, `DocumentType`, `Title` e `IsVisibleToPatient`. El paciente del documento debe coincidir con el de la cita para autorizar el acceso. Los registros vinculados únicamente a un encuentro clínico permanecen fuera de esta API de citas. Tamaños no registrados se muestran como tales.

Para archivos nuevos, `StoragePath` contiene la clave GUID privada. Los registros anteriores con rutas físicas u otras referencias no se reinterpretan como claves: si existen, requieren una migración de archivos específica. No se borran ni se cambian sus metadatos.

`IDocumentStorage` separa el almacenamiento de la API y sus permisos. La implementación inicial `LocalDocumentStorage` guarda los bytes usando una clave GUID generada en el servidor (sin el nombre original), crea archivos sin sobrescribir y elimina cargas parciales. Si falla la persistencia de metadatos, intenta retirar el archivo creado y registra cualquier fallo de limpieza para su revisión.

Las descargas usan JWT en el encabezado Authorization, devuelven `Content-Disposition: attachment`, `X-Content-Type-Options: nosniff` y `Cache-Control: private, no-store`. La API no devuelve rutas físicas ni claves de almacenamiento. La carpeta nunca se sirve como contenido estático.

## Configuración y publicación en MonsterASP

1. Ejecutar **`database/09_documentos_medicos.sql`** sobre `GestionCitasDb` antes de publicar el backend. Es repetible: crea el catálogo de 13 columnas si falta; si existe, valida nombres, tipos, longitudes y nulabilidad, y agrega índices no únicos. Conserva columnas, documentos, índices y restricciones anteriores. Un esquema diferente produce un error explícito con las columnas incompatibles; no agrega campos duplicados. En una base nueva, `EnsureCreated` también incluye la entidad; en una base existente **no** agrega la tabla automáticamente. Si se creó la tabla simplificada de la primera versión de esta rama, revisar su migración antes de continuar; el script no la transforma automáticamente.
2. Publicar backend y frontend de esta rama siguiendo el flujo habitual. Este cambio no actualiza el hosting automáticamente.
3. Mantener una carpeta **fuera del directorio de publicación y de toda carpeta pública**. Configuración predeterminada:

   ```json
   "MedicalDocuments": {
     "RootPath": "../private-medical-documents",
     "MaxFileSizeBytes": 5242880
   }
   ```

   `RootPath` se resuelve respecto a `ContentRootPath` del backend. Si el backend se publica en la carpeta `/wwwroot` del sitio de MonsterASP, el resultado es una carpeta hermana `/private-medical-documents`, dentro del espacio del mismo sitio. Si la publicación usa otro subdirectorio, configurar la ruta privada correcta mediante `MedicalDocuments__RootPath` o `appsettings.Production.json`. La aplicación rechaza rutas dentro de su directorio de publicación o del directorio público.
4. Dar a la identidad de la aplicación permisos de lectura/escritura sobre esa carpeta privada, sin permisos de ejecución. Se crea al subir el primer archivo si el proceso tiene permisos. No habilitar acceso HTTP ni agregar `UseStaticFiles` para esa ruta.
5. Conservar la carpeta al volver a publicar: no eliminarla al limpiar el sitio. Está fuera de `/wwwroot`; en Git Deploy, los archivos/carpetas que queden dentro del directorio desplegado requieren **Protected files**, pero esa opción no reemplaza la carpeta privada externa que exige esta implementación.
6. Probar una subida y descarga, reiniciar el sitio y publicar una nueva versión; verificar que el mismo archivo sigue disponible. Este paso debe realizarse en el hosting real para confirmar permisos y persistencia de su configuración concreta.
7. Mantener copias propias tanto de la carpeta como de la base de datos. El plan Free no incluye respaldos automáticos.

El límite configurable admite de 1 byte a 10 MiB; cada petición multipart tiene un límite HTTP adicional de 11 MiB. Si IIS impone un límite menor, ajustarlo en el hosting o reducir el tamaño de la aplicación. Los datos de configuración de almacenamiento no son secretos.

## API

| Método | Ruta | Resultado |
| --- | --- | --- |
| GET | `/api/medical-documents/limits` | Extensiones y tamaño máximo configurado. |
| GET | `/api/medical-documents/appointments/{appointmentId}` | Documentos de la cita autorizada. |
| POST | `/api/medical-documents/appointments/{appointmentId}` | Multipart con un campo `file`; devuelve 201 y metadatos. |
| PATCH | `/api/medical-documents/{documentId}/visibility` | Solo el autor profesional de una cita asignada; JSON `{"isVisibleToPatient": true/false}` obligatorio. |
| GET | `/api/medical-documents/{documentId}/download` | Archivo adjunto después de validar la pertenencia. |

El POST admite además el campo multipart opcional `isVisibleToPatient` para el profesional; al omitirlo conserva el valor predeterminado true. La respuesta de listado/carga incluye `isVisibleToPatient` y `canChangeVisibility`, calculado por el servidor para el usuario autenticado. El PATCH cambia únicamente la visibilidad; no requiere volver a subir el archivo.

401 sin sesión válida; 403 para roles no autorizados; 404 para citas/documentos inexistentes o ajenos; 400 para archivos inválidos o citas que no admiten cargas; 413 para peticiones mayores al límite HTTP; 503 para fallos de almacenamiento o persistencia.

## Comprobación

```bash
dotnet test backend/GestionCitas.Api.Tests/GestionCitas.Api.Tests.csproj --configuration Release
cd frontend
npm ci
npm run build
```

Pruebas automatizadas: acceso por rol y pertenencia, rechazos de archivo vacío/extensión/tamaño/firma, normalización de nombres, descargas sin rutas públicas, archivos faltantes, cargas en citas canceladas, conservación de archivos con nombres repetidos y limpieza si falla la base de datos. Las pruebas del almacenamiento local cubren directorios privados, claves inválidas, colisiones y cargas interrumpidas.

CI ejecuta además pruebas sobre un contenedor SQL Server 2025: tabla nueva, repetición del script, tabla existente con las 13 columnas reportadas, conservación de datos e índices, lectura/escritura EF contra ese esquema, rechazo controlado de esquemas incompatibles y una base parcialmente contenida con intercalaciones distintas para datos y catálogo. La validación de nombres usa `COLLATE CATALOG_DEFAULT`, sin cambiar las intercalaciones de la base o de sus columnas. Cada caso crea su propia base temporal. Estas cuatro pruebas SQL se omiten localmente si no se configura `CI_SQLSERVER_CONNECTION` con una instancia de pruebas que permita crear bases; la prueba de contención requiere tener habilitada esa función en la instancia de pruebas. El resto de la suite se ejecuta normalmente. CI habilita la función únicamente dentro de su contenedor efímero.

Comprobación manual: subir un PDF de prueba como paciente, descargarlo como profesional asignado, intentar acceder como otro paciente/profesional y repetir tras publicar. En móviles, verificar la selección del archivo y la descarga desde el modal.

Visibilidad: subir un documento como profesional desmarcando la casilla; comprobar que aparece para el profesional y no para el paciente de la cita. Presionar Mostrar al paciente sobre ese documento, recargar como paciente y descargarlo. Volver a ocultarlo y comprobar que desaparece y se rechaza una nueva descarga por API. En una carga realizada por el paciente no debe aparecer el botón para cambiar visibilidad. Las pruebas automatizadas cubren ambas transiciones, roles no autorizados, otros autores, parámetros manipulados y fallos al guardar.

## Evolución

Para migrar a almacenamiento privado en nube, implementar `IDocumentStorage`, copiar los objetos conservando su `StorageKey` y cambiar el registro de dependencia. No hace falta reemplazar las pantallas ni los permisos. El hosting gratuito se utiliza para aprendizaje con documentos ficticios; el uso clínico real requiere evaluar el servicio, respaldos, cifrado, análisis de archivos y obligaciones sobre datos personales.

Referencias: [carpetas privadas en MonsterASP](https://help.monsterasp.net/websites/manage/backups), [Git Deploy](https://help.monsterasp.net/websites/git-deploy/github-repository), [cargas de archivos ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0).
