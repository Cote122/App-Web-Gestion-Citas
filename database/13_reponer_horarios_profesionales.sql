-- Recuperación puntual de cupos: próximos 21 días, hora de Perú.
-- Ejecutar en GestionCitasDb. No altera citas ni cupos existentes.
SET XACT_ABORT ON;
IF DB_NAME() <> N'GestionCitasDb'
    THROW 50001, 'Selecciona la base GestionCitasDb antes de ejecutar.', 1;
-- Tras instalar la agenda administrada, reponer cupos desde la API al iniciar.
-- Este script usa horas fijas y podría volver a crear horarios retirados.
IF COL_LENGTH('dbo.Professionals', 'ScheduleConfigured') IS NOT NULL
    THROW 50002, 'Agenda administrada activa: reinicia la API para reponer cupos según la configuración.', 1;

DECLARE @HoyPeru date = CONVERT(date, SWITCHOFFSET(SYSDATETIMEOFFSET(), '-05:00'));
DECLARE @Insertados table (ProfessionalId uniqueidentifier NOT NULL, StartAt datetimeoffset NOT NULL);

BEGIN TRY
    BEGIN TRANSACTION;

    ;WITH Dias AS
    (
        SELECT DATEADD(DAY, v.n, @HoyPeru) AS Fecha
        FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),
                     (12),(13),(14),(15),(16),(17),(18),(19),(20),(21)) v(n)
    ),
    Horas AS
    (
        SELECT v.Hora FROM (VALUES (9),(10),(11),(15),(16)) v(Hora)
    )
    INSERT INTO dbo.AvailabilitySlots (Id, ProfessionalId, StartAt, EndAt, IsAvailable)
    OUTPUT inserted.ProfessionalId, inserted.StartAt INTO @Insertados
    SELECT NEWID(), p.Id, inicio.StartAt, DATEADD(MINUTE, 45, inicio.StartAt), 1
    FROM dbo.Professionals p
    JOIN dbo.Specialties e ON e.Id = p.SpecialtyId
    CROSS JOIN Dias d
    CROSS JOIN Horas h
    CROSS APPLY (VALUES (
        TODATETIMEOFFSET(DATEADD(HOUR, h.Hora, CONVERT(datetime2(0), d.Fecha)), '-05:00')
    )) inicio(StartAt)
    WHERE p.IsActive = 1
      AND e.IsActive = 1
      -- El 07/01/1900 fue domingo; evita depender de SET DATEFIRST.
      AND DATEDIFF(DAY, CONVERT(date, '19000107', 112), d.Fecha) % 7 <> 0
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.ProfessionalAbsences a
          WHERE a.ProfessionalId = p.Id AND a.IsActive = 1
            AND a.StartAt < DATEADD(MINUTE, 45, inicio.StartAt)
            AND a.EndAt > inicio.StartAt
      )
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.AvailabilitySlots actual WITH (UPDLOCK, HOLDLOCK)
          WHERE actual.ProfessionalId = p.Id AND actual.StartAt = inicio.StartAt
      );

    COMMIT TRANSACTION;

    SELECT p.FullName AS Profesional, COUNT(*) AS HorariosCreados
    FROM @Insertados i
    JOIN dbo.Professionals p ON p.Id = i.ProfessionalId
    GROUP BY p.FullName
    ORDER BY p.FullName;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
