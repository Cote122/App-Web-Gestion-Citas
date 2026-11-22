# HU-48 · Usabilidad y visualización móvil

Historia: [SCRUM-210](https://plataforma-gestion-citas.atlassian.net/browse/SCRUM-210). La historia aún indica que las pantallas concretas deben definirse en refinamiento. Esta entrega cubre **Mis citas**, navegación común, calendario de reserva y listados de resultados y recetas en móvil.

En **Mis citas**, Próximas citas y Lista de espera se dividen en páginas de seis registros. El historial se agrupa por fecha y muestra el número de citas en cada día del calendario. Al pulsar un día, se despliega el detalle sin generar una columna de todas las citas. El listado de fechas también se pagina de seis en seis. Un filtro inclusivo Desde/Hasta permite buscar entre fechas; se rechazan rangos incompletos o invertidos y se reinicia la página al aplicar o limpiar.

En pantallas estrechas, el menú lateral se abre bajo demanda; el calendario de reserva cabe en el ancho disponible y las tablas de resultados clínicos y recetas se presentan como tarjetas con etiquetas visibles. No cambia el esquema SQL ni los endpoints.

## Comprobación manual

1. En Mis citas con más de seis registros por sección, navegar adelante y atrás; verificar que todos aparecen una sola vez.
2. Filtrar un rango que incluya el primer y último día con citas, comprobar cantidades, calendario y páginas. Ingresar inicio posterior a fin y comprobar el mensaje sin perder el filtro anterior. Limpiar.
3. Elegir un día con varias citas en el calendario, desplegarlo y volver a cerrarlo. Elegir un día fuera de la página actual y comprobar que se muestra la página correcta.
4. Comprobar con ancho de teléfono (aprox. 320–390 px) y tableta (aprox. 768 px): menú, filtros, calendario, botones de reserva, resultados y recetas sin desplazamiento horizontal de la página.

Para completar el alcance de “pantallas específicas” en Jira, el equipo debe acordar si el mismo filtro/paginación se aplica también a Mi agenda del profesional, resultados clínicos, recetas y tablas administrativas. Una lista grande cargada completamente en el navegador puede requerir paginación en la API en un incremento posterior.
