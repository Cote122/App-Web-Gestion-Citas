# Monitor de disponibilidad de la API

La API publicada se verifica mediante GitHub Actions:

```text
https://app-web-gestion-citas.runasp.net/api/health
```

## Frecuencia

El workflow `.github/workflows/api-health-monitor.yml` ejecuta una petición HTTP cada 15 minutos:

```text
minuto 07
minuto 22
minuto 37
minuto 52
```

Se evita deliberadamente el inicio exacto de cada hora porque los workflows programados de GitHub Actions pueden sufrir más retrasos en esos momentos.

## Validación

La ejecución es considerada correcta únicamente cuando:

1. El endpoint responde HTTP 2xx.
2. El cuerpo contiene:

```json
{
  "status": "ok"
}
```

La petición usa un timeout de 20 segundos y hasta 2 reintentos.

## Importante

Los workflows con `schedule` de GitHub Actions solo se ejecutan desde la rama predeterminada del repositorio.

Por lo tanto, el monitor comenzará a ejecutarse automáticamente cuando el PR que agrega este archivo sea integrado a `main`.

También se incluye `workflow_dispatch` para poder ejecutarlo manualmente desde GitHub Actions una vez que el workflow exista en la rama predeterminada.

## Alcance

Este monitor sirve para:

- comprobar periódicamente que la API responde;
- generar actividad HTTP regular sobre el endpoint público de salud;
- ayudar a mantener disponibles los procesos de la API durante el periodo académico.

No reemplaza un hosting con ejecución garantizada 24/7. GitHub Actions puede retrasar ejecuciones programadas y, en repositorios públicos sin actividad durante 60 días, los workflows programados pueden deshabilitarse automáticamente.
