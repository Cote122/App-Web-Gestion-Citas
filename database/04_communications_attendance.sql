/*
  HU-20 Recordatorios automáticos
  HU-23 Confirmación de asistencia

  Ejecutar una sola vez sobre GestionCitasDb antes de probar esta rama.
  El script es idempotente.
*/

USE [GestionCitasDb];
GO

/* ============================================================
   1. Appointments: confirmación de asistencia
   ============================================================ */
IF COL_LENGTH('dbo.Appointments', 'AttendanceStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Appointments
    ADD AttendanceStatus NVARCHAR(30) NOT NULL
        CONSTRAINT DF_Appointments_AttendanceStatus DEFAULT N'Pendiente';
END;
GO

IF COL_LENGTH('dbo.Appointments', 'AttendanceRequestedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Appointments
    ADD AttendanceRequestedAt DATETIMEOFFSET NULL;
END;
GO

IF COL_LENGTH('dbo.Appointments', 'AttendanceRespondedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Appointments
    ADD AttendanceRespondedAt DATETIMEOFFSET NULL;
END;
GO

/* ============================================================
   2. NotificationOutbox: relación con cita para evitar duplicados
   ============================================================ */
IF COL_LENGTH('dbo.NotificationOutbox', 'RelatedAppointmentId') IS NULL
BEGIN
    ALTER TABLE dbo.NotificationOutbox
    ADD RelatedAppointmentId UNIQUEIDENTIFIER NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_NotificationOutbox_Appointments_RelatedAppointmentId'
)
BEGIN
    ALTER TABLE dbo.NotificationOutbox
    ADD CONSTRAINT FK_NotificationOutbox_Appointments_RelatedAppointmentId
        FOREIGN KEY (RelatedAppointmentId)
        REFERENCES dbo.Appointments(Id);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_NotificationOutbox_Type_RelatedAppointmentId'
      AND object_id = OBJECT_ID('dbo.NotificationOutbox')
)
BEGIN
    CREATE UNIQUE INDEX UX_NotificationOutbox_Type_RelatedAppointmentId
        ON dbo.NotificationOutbox([Type], RelatedAppointmentId)
        WHERE RelatedAppointmentId IS NOT NULL;
END;
GO

/* ============================================================
   3. Verificación
   ============================================================ */
SELECT
    c.name AS Columna,
    t.name AS Tipo,
    c.is_nullable
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.Appointments')
  AND c.name IN (
      'AttendanceStatus',
      'AttendanceRequestedAt',
      'AttendanceRespondedAt'
  )
ORDER BY c.column_id;
GO

SELECT
    CASE
        WHEN COL_LENGTH('dbo.NotificationOutbox', 'RelatedAppointmentId') IS NOT NULL
        THEN 'OK'
        ELSE 'FALTA'
    END AS RelatedAppointmentId;
GO
