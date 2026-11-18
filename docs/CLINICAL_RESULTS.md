# HU-15 — Resultados clínicos

[SCRUM-26](https://plataforma-gestion-citas.atlassian.net/browse/SCRUM-26).

El paciente entra a **Resultados clínicos** y consulta únicamente sus resultados publicados. Puede buscar por título o tipo de examen, abrir el contenido y descargar informes con su sesión. El detalle admite un enlace `/app/results?resultado=<GUID>`; la API vuelve a validar cada acceso. No hay edición clínica para pacientes.

## Registro y publicación

En **Mi agenda → Resultados**, el profesional asignado puede crear un resultado para una cita confirmada o atendida. El paciente y el autor se obtienen en el servidor. El formulario registra tipo de examen, título, fecha opcional y contenido/resumen. Puede adjuntar PDF, JPG/JPEG o PNG con los límites y validación de HU-18. Este flujo mínimo cubre la dependencia de registro/publicación de HU-15 y no modifica los registros de atención de HU-17.

- `Pendiente`: estado inicial confirmado en la base; no se muestra al paciente.
- `Publicado`: contrato de esta entrega, definido al no existir registros ni CHECK previos. Exige contenido o al menos un informe. Al publicar se asignan `IsVisibleToPatient = 1` y `PublishedAt = UTC actual`.
- El paciente requiere las tres condiciones, fecha de publicación no futura y coincidencia de paciente con las relaciones de cita/atención, cuando existen.
- **Retirar publicación** vuelve a Pendiente, oculta el resultado y limpia PublishedAt. Una nueva descarga del paciente devuelve 404. No puede retirar un archivo que el paciente ya descargó.
- Solo el autor profesional de una cita que sigue teniendo asignada puede modificar o publicar el resultado. Otros profesionales asignados pueden consultarlo, sin controles de edición si no son autores.
- Para editar o adjuntar otro informe debe retirarse primero la publicación. Los controles de concurrencia impiden guardar contenido o adjuntos contra una publicación que cambió mientras se trabajaba.
- Otros estados del catálogo quedan excluidos de la consulta del paciente y de estas operaciones de edición/publicación. No se convierten automáticamente.

Se admiten resultados previos vinculados solo al paciente o a un encuentro coherente para la consulta del propio paciente. El flujo del profesional trabaja sobre citas; un registro sin AppointmentId no se administra desde esta pantalla.

## Base de datos

Se utilizan exactamente las columnas reportadas de **ClinicalResults (13), ClinicalResultFiles (8) y ClinicalEncounters (12)**. Los tamaños nvarchar del modelo se expresan en caracteres; `sys.columns.max_length` los reporta en bytes.

Ejecutar `database/10_resultados_clinicos.sql` antes de publicar. Valida tablas, nombres, tipos, longitudes y nulabilidad, y agrega tres índices no únicos. No crea el catálogo clínico ausente ni altera columnas, defaults, estados, CHECK, claves o registros existentes. Es repetible y usa `COLLATE CATALOG_DEFAULT`, compatible con la base parcialmente contenida. Debe terminar con `GestionCitasDb | OK | OK | OK`.

No se requiere insertar resultados mediante SQL: el profesional registra un resultado ficticio desde su cita y lo publica desde la aplicación.

## Archivos y despliegue

Los bytes usan `IDocumentStorage` y la carpeta privada de HU-18. Se generan claves GUID independientes; nombre y ruta no determinan el acceso. El SHA-256 de una nueva carga se guarda en FileHash para trazabilidad. No es una firma clínica ni un análisis antivirus. La descarga usa Authorization, attachment, nosniff y no-store. No se devuelven StoragePath, FileHash ni CreatedBy.

Los archivos antiguos con rutas físicas o claves ajenas al proveedor requieren una migración específica; no se interpretan como rutas que la API pueda abrir. Los archivos ausentes devuelven un error controlado. Publicar backend y frontend y conservar la carpeta privada y su configuración como en [HU-18](MEDICAL_DOCUMENTS.md).

## Prueba manual

1. Profesional: Mi agenda → Resultados de una cita propia → Nuevo resultado. Guardar título y contenido ficticios; comprobar Pendiente.
2. Paciente: Resultados clínicos. El pendiente no debe aparecer.
3. Profesional: abrir el pendiente, adjuntar un PDF ficticio y Publicar para el paciente.
4. Paciente: Actualizar, filtrar por tipo, abrir detalle y descargar el mismo informe. Tipo, fecha y disponibilidad deben verse correctamente.
5. Otro paciente: intentar el enlace directo de detalle o descarga; debe recibir 404 sin contenido clínico.
6. Profesional: retirar publicación. El paciente deja de listarlo y una nueva descarga es rechazada, incluso conservando el enlace anterior.
7. Verificar que pacientes, Caja y Administrador no pueden registrar, editar, adjuntar o publicar mediante la API; otro profesional no puede administrar resultados ajenos.
8. Comprobar carga inválida y persistencia del mismo informe tras reiniciar y volver a publicar.

Las pruebas HTTP verifican pertenencia, roles, estados, enlaces manipulados, publicación/retirada, limpieza ante fallos y restricciones de archivos. CI añade SQL Server 2025 sobre el esquema nativo: ejecución repetida, colaciones distintas, lectura/escritura EF, consultas reales de autorización y concurrencia. Solo las pruebas SQL se omiten si no se configura CI_SQLSERVER_CONNECTION contra una instancia desechable de pruebas.
