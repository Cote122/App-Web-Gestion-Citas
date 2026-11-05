/*
  Historial médico y motivo de consulta.
  Idempotente: admite una ejecución anterior completa o parcial.
  Conserva las claves foráneas existentes de AppointmentAuditLogs,
  independientemente de su nombre, y permite varios eventos por cita.
*/

USE [GestionCitasDb];
GO

-- No mezclar esta actualización con otra transacción de la misma sesión.
IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR('La sesión tiene una transacción abierta. Revisa y cierra esa transacción antes de ejecutar este script.', 16, 1);
    RETURN;
END;

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID('dbo.Appointments', 'U') IS NULL
       OR OBJECT_ID('dbo.AppointmentAuditLogs', 'U') IS NULL
       OR OBJECT_ID('dbo.Professionals', 'U') IS NULL
       OR OBJECT_ID('dbo.Users', 'U') IS NULL
        THROW 50001, 'Faltan tablas base en GestionCitasDb. Verifica la base y los scripts anteriores.', 1;

    IF COL_LENGTH('dbo.Appointments', 'Reason') IS NULL
    BEGIN
        ALTER TABLE dbo.Appointments ADD Reason NVARCHAR(500) NULL;
    END;

    IF OBJECT_ID('dbo.MedicalRecords', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MedicalRecords
        (
            Id UNIQUEIDENTIFIER NOT NULL,
            PatientId UNIQUEIDENTIFIER NOT NULL,
            ProfessionalId UNIQUEIDENTIFIER NOT NULL,
            AppointmentId UNIQUEIDENTIFIER NOT NULL,
            Observations NVARCHAR(2000) NULL,
            Diagnosis NVARCHAR(500) NULL,
            Recommendations NVARCHAR(1000) NULL,
            AttentionDate DATETIMEOFFSET NOT NULL,
            CreatedAt DATETIMEOFFSET NOT NULL,
            UpdatedAt DATETIMEOFFSET NULL,
            CONSTRAINT PK_MedicalRecords PRIMARY KEY (Id),
            CONSTRAINT FK_MedicalRecords_Appointments_AppointmentId
                FOREIGN KEY (AppointmentId) REFERENCES dbo.Appointments(Id) ON DELETE CASCADE,
            CONSTRAINT FK_MedicalRecords_Professionals_ProfessionalId
                FOREIGN KEY (ProfessionalId) REFERENCES dbo.Professionals(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_MedicalRecords_Users_PatientId
                FOREIGN KEY (PatientId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION
        );
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.MedicalRecords')
          AND name = 'IX_MedicalRecords_AppointmentId'
    )
    BEGIN
        CREATE INDEX IX_MedicalRecords_AppointmentId ON dbo.MedicalRecords(AppointmentId);
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.MedicalRecords')
          AND name = 'IX_MedicalRecords_PatientId'
    )
    BEGIN
        CREATE INDEX IX_MedicalRecords_PatientId ON dbo.MedicalRecords(PatientId);
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.MedicalRecords')
          AND name = 'IX_MedicalRecords_ProfessionalId'
    )
    BEGIN
        CREATE INDEX IX_MedicalRecords_ProfessionalId ON dbo.MedicalRecords(ProfessionalId);
    END;

    -- Corregir también el índice de la versión anterior, si ya existe.
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.AppointmentAuditLogs')
          AND name = 'IX_AppointmentAuditLogs_AppointmentId'
          AND is_unique = 1
    )
    BEGIN
        DROP INDEX IX_AppointmentAuditLogs_AppointmentId ON dbo.AppointmentAuditLogs;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.AppointmentAuditLogs')
          AND name = 'IX_AppointmentAuditLogs_AppointmentId'
    )
    BEGIN
        CREATE INDEX IX_AppointmentAuditLogs_AppointmentId ON dbo.AppointmentAuditLogs(AppointmentId);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    DB_NAME() AS BaseActual,
    CASE WHEN COL_LENGTH('dbo.Appointments', 'Reason') IS NOT NULL THEN 'OK' ELSE 'FALTA' END AS Reason,
    CASE WHEN OBJECT_ID('dbo.MedicalRecords', 'U') IS NOT NULL THEN 'OK' ELSE 'FALTA' END AS MedicalRecords,
    (SELECT is_unique FROM sys.indexes
     WHERE object_id = OBJECT_ID('dbo.AppointmentAuditLogs')
       AND name = 'IX_AppointmentAuditLogs_AppointmentId') AS AuditIndexIsUnique;
-- Resultado esperado: GestionCitasDb | OK | OK | 0.
GO

