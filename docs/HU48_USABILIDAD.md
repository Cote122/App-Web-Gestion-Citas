# HU-48 · Usabilidad y visualización móvil

Historia: [SCRUM-210](https://plataforma-gestion-citas.atlassian.net/browse/SCRUM-210). El alcance acordado incluye **Mis citas**, **Mi agenda**, todos los listados administrativos, resultados, recetas, ausencias y reserva. Se adaptan los listados y la navegación a pantallas móviles.

En **Mis citas**, Próximas citas y Lista de espera se dividen en páginas de seis registros. El historial se agrupa por fecha y muestra el número de citas en cada día del calendario. Al pulsar un día, se despliega el detalle sin generar una columna de todas las citas. El listado de fechas también se pagina de seis en seis. Un filtro inclusivo Desde/Hasta permite buscar entre fechas; se rechazan rangos incompletos o invertidos y se reinicia la página al aplicar o limpiar.

En pantallas estrechas, el menú lateral se abre bajo demanda; el calendario de reserva cabe en el ancho disponible y las tablas de resultados clínicos y recetas se presentan como tarjetas con etiquetas visibles. No cambia el esquema SQL ni los endpoints.

**Mi agenda** muestra un calendario con el número de citas por fecha, filtros inclusivos Desde/Hasta, validación de rango y paginación independiente para confirmadas, atendidas y otros estados. Seleccionar una fecha filtra sus secciones y restablece las páginas. En móvil, las acciones de cada cita se distribuyen en filas y los horarios de reprogramación dejan de exigir desplazamiento horizontal.

**Administración**: usuarios y permisos, especialidades y profesionales muestran ocho registros por página, ofrecen búsqueda textual y sus tablas se transforman en tarjetas etiquetadas en teléfono. La corrección de recetas conserva la búsqueda por DNI, incorpora fechas y páginas, y ya no recorta silenciosamente el listado a 50 recetas. Los catálogos sin fecha ofrecen filtros por texto según sus campos.

**Otros listados**: resultados clínicos se filtran por nombre, tipo y fechas; recetas e indicaciones por profesional y fechas, con páginas independientes; ausencias por estado y fechas que se solapan con el rango, y sus citas afectadas por paciente/código. La reserva permite buscar y paginar especialidades, profesionales y horarios del día elegido. En cada búsqueda se vuelve a la primera página. Los formularios y vistas de detalle no requieren paginación de sus campos.

## Comprobación manual

1. En Mis citas con más de seis registros por sección, navegar adelante y atrás; verificar que todos aparecen una sola vez.
2. Filtrar un rango que incluya el primer y último día con citas, comprobar cantidades, calendario y páginas. Ingresar inicio posterior a fin y comprobar el mensaje sin perder el filtro anterior. Limpiar.
3. Elegir un día con varias citas en el calendario, desplegarlo y volver a cerrarlo. Elegir un día fuera de la página actual y comprobar que se muestra la página correcta.
4. Comprobar con ancho de teléfono (aprox. 320–390 px) y tableta (aprox. 768 px): menú, filtros, calendario, botones de reserva, resultados y recetas sin desplazamiento horizontal de la página.
5. Con Mi agenda, filtrar y seleccionar una fecha con varios estados; navegar las tres paginaciones y comprobar acciones de atención, reprogramación y documentos en móvil.
6. Con administrador, recorrer más de ocho registros por tabla, editar un registro en otra página y verificar que se mantiene accesible. En Corrección de recetas, buscar por DNI con rango válido e inválido y navegar todas las recetas encontradas.
7. Buscar en cada catálogo, en resultados y en recetas; cambiar fechas/estado con una página posterior seleccionada y comprobar que regresa a la primera. Repetir con ausencias y sus citas afectadas.
8. En reserva, buscar especialidades y profesionales, navegar sus páginas y elegir una fecha con más de doce horarios. Confirmar que la selección conserva la cita correcta.

La paginación se realiza sobre los registros que devuelve cada consulta. Si el volumen de datos crece, conviene añadir paginación en la API para evitar descargar todo el historial en cada visita.
