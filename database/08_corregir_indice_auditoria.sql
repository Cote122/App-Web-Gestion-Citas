/*
  Reparación para bases donde ya se ejecutó 07_historia_medica.sql.
  Permite varios eventos de auditoría por cita sin eliminar registros.
  Es idempotente: se puede volver a ejecutar.
*/

USE [GestionCitasDb];
GO

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID('dbo.AppointmentAuditLogs', 'U') IS NULL
        THROW 50001, 'Falta dbo.AppointmentAuditLogs. Ejecuta primero 03_appointment_audit.sql.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.AppointmentAuditLogs')
          AND name = 'IX_AppointmentAuditLogs_AppointmentId'
          AND is_unique = 1
    )
    BEGIN
        DROP INDEX IX_AppointmentAuditLogs_AppointmentId ON dbo.AppointmentAuditLogs;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.AppointmentAuditLogs')
          AND name = 'IX_AppointmentAuditLogs_AppointmentId'
    )
    BEGIN
        CREATE INDEX IX_AppointmentAuditLogs_AppointmentId
            ON dbo.AppointmentAuditLogs(AppointmentId);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT name AS IndexName, is_unique AS IsUnique
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.AppointmentAuditLogs')
  AND name = 'IX_AppointmentAuditLogs_AppointmentId';
-- Resultado esperado: IsUnique = 0.
GO
