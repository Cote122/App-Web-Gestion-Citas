/*
    HU-47 - Solicitudes de cancelación y reprogramación sujetas a aprobación.
    SQL Server. Script idempotente: puede ejecutarse más de una vez.

    Tablas existentes esperadas (esquema dbo):
      Users, Professionals, Appointments, AvailabilitySlots
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Validar primero las tablas referenciadas para evitar crear una estructura incompleta.
    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
        THROW 51000, 'No existe dbo.Users. Verifica el esquema de la base de datos.', 1;

    IF OBJECT_ID(N'dbo.Professionals', N'U') IS NULL
        THROW 51001, 'No existe dbo.Professionals. Verifica el esquema de la base de datos.', 1;

    IF OBJECT_ID(N'dbo.Appointments', N'U') IS NULL
        THROW 51002, 'No existe dbo.Appointments. Verifica el esquema de la base de datos.', 1;

    IF OBJECT_ID(N'dbo.AvailabilitySlots', N'U') IS NULL
        THROW 51003, 'No existe dbo.AvailabilitySlots. Verifica el esquema de la base de datos.', 1;

    IF OBJECT_ID(N'dbo.AppointmentChangeRequests', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AppointmentChangeRequests
        (
            Id                       uniqueidentifier NOT NULL
                CONSTRAINT DF_AppointmentChangeRequests_Id DEFAULT (NEWID()),
            AppointmentId            uniqueidentifier NOT NULL,
            RequestedByProfessionalId uniqueidentifier NOT NULL,
            [Type]                   nvarchar(20) NOT NULL,
            Reason                   nvarchar(500) NULL,
            TargetSlotId             uniqueidentifier NULL,
            [Status]                 nvarchar(24) NOT NULL
                CONSTRAINT DF_AppointmentChangeRequests_Status DEFAULT (N'PendienteAprobacion'),
            CreatedAt                datetimeoffset(7) NOT NULL
                CONSTRAINT DF_AppointmentChangeRequests_CreatedAt
                DEFAULT (TODATETIMEOFFSET(SYSUTCDATETIME(), '-05:00')),
            ReviewedByAdminId        uniqueidentifier NULL,
            ReviewedAt               datetimeoffset(7) NULL,
            AdminComment             nvarchar(500) NULL,

            CONSTRAINT PK_AppointmentChangeRequests
                PRIMARY KEY CLUSTERED (Id),

            CONSTRAINT FK_AppointmentChangeRequests_Appointments
                FOREIGN KEY (AppointmentId)
                REFERENCES dbo.Appointments (Id)
                ON DELETE NO ACTION,

            CONSTRAINT FK_AppointmentChangeRequests_Professionals
                FOREIGN KEY (RequestedByProfessionalId)
                REFERENCES dbo.Professionals (Id)
                ON DELETE NO ACTION,

            CONSTRAINT FK_AppointmentChangeRequests_AvailabilitySlots
                FOREIGN KEY (TargetSlotId)
                REFERENCES dbo.AvailabilitySlots (Id)
                ON DELETE NO ACTION,

            CONSTRAINT FK_AppointmentChangeRequests_ReviewedByAdmin
                FOREIGN KEY (ReviewedByAdminId)
                REFERENCES dbo.Users (Id)
                ON DELETE NO ACTION,

            CONSTRAINT CK_AppointmentChangeRequests_Type_TargetSlot
                CHECK
                (
                    ([Type] = N'Cancelacion' AND TargetSlotId IS NULL)
                    OR
                    ([Type] = N'Reprogramacion' AND TargetSlotId IS NOT NULL)
                ),

            CONSTRAINT CK_AppointmentChangeRequests_Status_Review
                CHECK
                (
                    ([Status] = N'PendienteAprobacion'
                        AND ReviewedByAdminId IS NULL
                        AND ReviewedAt IS NULL)
                    OR
                    ([Status] IN (N'Aprobada', N'Rechazada')
                        AND ReviewedByAdminId IS NOT NULL
                        AND ReviewedAt IS NOT NULL)
                )
        );
    END;

    -- Historial completo de solicitudes de una cita.
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AppointmentChangeRequests')
          AND name = N'IX_AppointmentChangeRequests_AppointmentId_CreatedAt'
    )
    BEGIN
        CREATE INDEX IX_AppointmentChangeRequests_AppointmentId_CreatedAt
            ON dbo.AppointmentChangeRequests (AppointmentId, CreatedAt DESC);
    END;

    -- Bandeja administrativa: primero las pendientes más antiguas.
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AppointmentChangeRequests')
          AND name = N'IX_AppointmentChangeRequests_Status_CreatedAt'
    )
    BEGIN
        CREATE INDEX IX_AppointmentChangeRequests_Status_CreatedAt
            ON dbo.AppointmentChangeRequests ([Status], CreatedAt);
    END;

    -- Consulta de solicitudes pertenecientes a la agenda de un profesional.
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AppointmentChangeRequests')
          AND name = N'IX_AppointmentChangeRequests_Professional_CreatedAt'
    )
    BEGIN
        CREATE INDEX IX_AppointmentChangeRequests_Professional_CreatedAt
            ON dbo.AppointmentChangeRequests (RequestedByProfessionalId, CreatedAt DESC);
    END;

    -- Un mismo appointment no puede tener más de una solicitud pendiente.
    -- Una solicitud rechazada no impide registrar una nueva posteriormente.
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AppointmentChangeRequests')
          AND name = N'UX_AppointmentChangeRequests_OnePendingPerAppointment'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_AppointmentChangeRequests_OnePendingPerAppointment
            ON dbo.AppointmentChangeRequests (AppointmentId)
            WHERE [Status] = N'PendienteAprobacion';
    END;

    -- Índices para las FK opcionales consultadas o verificadas independientemente.
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AppointmentChangeRequests')
          AND name = N'IX_AppointmentChangeRequests_TargetSlotId'
    )
    BEGIN
        CREATE INDEX IX_AppointmentChangeRequests_TargetSlotId
            ON dbo.AppointmentChangeRequests (TargetSlotId)
            WHERE TargetSlotId IS NOT NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AppointmentChangeRequests')
          AND name = N'IX_AppointmentChangeRequests_ReviewedByAdminId'
    )
    BEGIN
        CREATE INDEX IX_AppointmentChangeRequests_ReviewedByAdminId
            ON dbo.AppointmentChangeRequests (ReviewedByAdminId)
            WHERE ReviewedByAdminId IS NOT NULL;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
