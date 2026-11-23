-- Aplicar en GestionCitasDb antes de publicar el backend con agenda configurable.
-- Idempotente: conserva las citas y los cupos existentes. Los horarios antiguos
-- siguen vigentes hasta que el administrador guarde la agenda de cada profesional.
SET XACT_ABORT ON;
IF DB_NAME() <> N'GestionCitasDb'
    THROW 50001, 'Selecciona la base GestionCitasDb antes de ejecutar.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.Professionals', 'ScheduleConfigured') IS NULL
        ALTER TABLE dbo.Professionals ADD ScheduleConfigured bit NOT NULL
            CONSTRAINT DF_Professionals_ScheduleConfigured DEFAULT (0) WITH VALUES;

    IF COL_LENGTH('dbo.AvailabilitySlots', 'IsEnabled') IS NULL
        ALTER TABLE dbo.AvailabilitySlots ADD IsEnabled bit NOT NULL
            CONSTRAINT DF_AvailabilitySlots_IsEnabled DEFAULT (1) WITH VALUES;

    IF OBJECT_ID('dbo.ProfessionalWeeklySlots', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProfessionalWeeklySlots (
            Id uniqueidentifier NOT NULL CONSTRAINT PK_ProfessionalWeeklySlots PRIMARY KEY,
            ProfessionalId uniqueidentifier NOT NULL,
            DayOfWeek int NOT NULL,
            StartTime time(0) NOT NULL,
            IsEnabled bit NOT NULL,
            CONSTRAINT FK_ProfessionalWeeklySlots_Professionals_ProfessionalId
                FOREIGN KEY (ProfessionalId) REFERENCES dbo.Professionals(Id) ON DELETE CASCADE
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProfessionalWeeklySlots')
                   AND name = 'IX_ProfessionalWeeklySlots_ProfessionalId_DayOfWeek_StartTime')
        CREATE UNIQUE INDEX IX_ProfessionalWeeklySlots_ProfessionalId_DayOfWeek_StartTime
            ON dbo.ProfessionalWeeklySlots(ProfessionalId, DayOfWeek, StartTime);

    COMMIT TRANSACTION;
    SELECT COL_LENGTH('dbo.Professionals', 'ScheduleConfigured') AS ScheduleConfigured,
           COL_LENGTH('dbo.AvailabilitySlots', 'IsEnabled') AS IsEnabled,
           OBJECT_ID('dbo.ProfessionalWeeklySlots', 'U') AS WeeklySlotsTable;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
