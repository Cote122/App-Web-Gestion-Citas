/*
  HU-13 Lista de espera por horario concreto + bandeja de notificaciones.
  Ejecutar una sola vez sobre GestionCitasDb antes de probar esta rama.

  El script es idempotente: valida columnas, índices y FKs antes de crearlos.
  Los registros antiguos de WaitlistEntries no se eliminan. Si existen, SlotId
  quedará NULL hasta que se migren o cierren manualmente.
*/

USE [GestionCitasDb];
GO

/* ============================================================
   1. WaitlistEntries: asociar la espera a un AvailabilitySlot
   ============================================================ */
IF COL_LENGTH('dbo.WaitlistEntries', 'SlotId') IS NULL
BEGIN
    ALTER TABLE dbo.WaitlistEntries
    ADD SlotId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.WaitlistEntries', 'AssignedAppointmentId') IS NULL
BEGIN
    ALTER TABLE dbo.WaitlistEntries
    ADD AssignedAppointmentId UNIQUEIDENTIFIER NULL;
END;
GO

IF COL_LENGTH('dbo.WaitlistEntries', 'AssignedAt') IS NULL
BEGIN
    ALTER TABLE dbo.WaitlistEntries
    ADD AssignedAt DATETIMEOFFSET NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_WaitlistEntries_AvailabilitySlots_SlotId'
)
BEGIN
    ALTER TABLE dbo.WaitlistEntries
    ADD CONSTRAINT FK_WaitlistEntries_AvailabilitySlots_SlotId
        FOREIGN KEY (SlotId)
        REFERENCES dbo.AvailabilitySlots(Id);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_WaitlistEntries_Appointments_AssignedAppointmentId'
)
BEGIN
    ALTER TABLE dbo.WaitlistEntries
    ADD CONSTRAINT FK_WaitlistEntries_Appointments_AssignedAppointmentId
        FOREIGN KEY (AssignedAppointmentId)
        REFERENCES dbo.Appointments(Id);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_WaitlistEntries_SlotId_Status_CreatedAt'
      AND object_id = OBJECT_ID('dbo.WaitlistEntries')
)
BEGIN
    CREATE INDEX IX_WaitlistEntries_SlotId_Status_CreatedAt
        ON dbo.WaitlistEntries(SlotId, Status, CreatedAt);
END;
GO

/* Un paciente solo puede tener una espera ACTIVA para el mismo horario. */
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_WaitlistEntries_ActivePatientSlot'
      AND object_id = OBJECT_ID('dbo.WaitlistEntries')
)
BEGIN
    CREATE UNIQUE INDEX UX_WaitlistEntries_ActivePatientSlot
        ON dbo.WaitlistEntries(PatientId, SlotId)
        WHERE Status = N'Activa' AND SlotId IS NOT NULL;
END;
GO

/* ============================================================
   2. NotificationOutbox
      No depende todavía de Brevo/Resend/Mailjet.
      Guarda notificaciones pendientes para un proveedor futuro.
   ============================================================ */
IF OBJECT_ID('dbo.NotificationOutbox', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotificationOutbox
    (
        Id UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_NotificationOutbox PRIMARY KEY,

        RecipientUserId UNIQUEIDENTIFIER NOT NULL,
        RecipientEmail NVARCHAR(200) NOT NULL,

        [Type] NVARCHAR(80) NOT NULL,
        [Subject] NVARCHAR(200) NOT NULL,
        Body NVARCHAR(2000) NOT NULL,

        [Status] NVARCHAR(30) NOT NULL
            CONSTRAINT DF_NotificationOutbox_Status DEFAULT N'Pendiente',

        CreatedAt DATETIMEOFFSET NOT NULL,
        SentAt DATETIMEOFFSET NULL,
        LastError NVARCHAR(1000) NULL,

        CONSTRAINT FK_NotificationOutbox_Users_RecipientUserId
            FOREIGN KEY (RecipientUserId)
            REFERENCES dbo.Users(Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_NotificationOutbox_Status_CreatedAt'
      AND object_id = OBJECT_ID('dbo.NotificationOutbox')
)
BEGIN
    CREATE INDEX IX_NotificationOutbox_Status_CreatedAt
        ON dbo.NotificationOutbox([Status], CreatedAt);
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
WHERE c.object_id = OBJECT_ID('dbo.WaitlistEntries')
  AND c.name IN ('SlotId', 'AssignedAppointmentId', 'AssignedAt')
ORDER BY c.column_id;
GO

SELECT
    CASE WHEN OBJECT_ID('dbo.NotificationOutbox', 'U') IS NOT NULL
         THEN 'OK'
         ELSE 'FALTA'
    END AS NotificationOutbox;
GO
