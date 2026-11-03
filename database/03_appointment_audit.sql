/*
  HU-08 / HU-09
  Trazabilidad de cancelaciones y reprogramaciones.

  Ejecutar una sola vez sobre GestionCitasDb antes de probar esta rama.
  El script es idempotente.
*/

USE [GestionCitasDb];
GO

IF OBJECT_ID('dbo.AppointmentAuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppointmentAuditLogs
    (
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_AppointmentAuditLogs PRIMARY KEY,

        AppointmentId UNIQUEIDENTIFIER NOT NULL,
        PatientId UNIQUEIDENTIFIER NOT NULL,

        EventType NVARCHAR(50) NOT NULL,

        PreviousSlotId UNIQUEIDENTIFIER NULL,
        NewSlotId UNIQUEIDENTIFIER NULL,

        Details NVARCHAR(1000) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,

        CONSTRAINT FK_AppointmentAuditLogs_Appointments_AppointmentId
            FOREIGN KEY (AppointmentId)
            REFERENCES dbo.Appointments(Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_AppointmentAuditLogs_AppointmentId_CreatedAt'
      AND object_id = OBJECT_ID('dbo.AppointmentAuditLogs')
)
BEGIN
    CREATE INDEX IX_AppointmentAuditLogs_AppointmentId_CreatedAt
        ON dbo.AppointmentAuditLogs(AppointmentId, CreatedAt);
END;
GO

SELECT
    CASE WHEN OBJECT_ID('dbo.AppointmentAuditLogs', 'U') IS NOT NULL
         THEN 'OK'
         ELSE 'FALTA'
    END AS AppointmentAuditLogs;
GO
