/*
  HU-14 Vacaciones y licencias del profesional.
  Ejecutar una sola vez sobre GestionCitasDb ANTES de levantar el backend de esta rama.
  Sin esta tabla, la consulta de disponibilidad y la reserva de citas fallan.

  El script es idempotente: si la tabla o el índice ya existen, no hace nada.
  No modifica ni elimina datos existentes.
*/

USE [GestionCitasDb];
GO

IF OBJECT_ID('dbo.ProfessionalAbsences', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfessionalAbsences
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProfessionalAbsences PRIMARY KEY,
        ProfessionalId UNIQUEIDENTIFIER NOT NULL,
        Type NVARCHAR(20) NOT NULL,
        StartAt DATETIMEOFFSET NOT NULL,
        EndAt DATETIMEOFFSET NOT NULL,
        Reason NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CancelledAt DATETIMEOFFSET NULL,
        CONSTRAINT FK_ProfessionalAbsences_Professionals_ProfessionalId
            FOREIGN KEY (ProfessionalId) REFERENCES dbo.Professionals (Id) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ProfessionalAbsences_ProfessionalId_StartAt_EndAt'
      AND object_id = OBJECT_ID('dbo.ProfessionalAbsences')
)
BEGIN
    CREATE INDEX IX_ProfessionalAbsences_ProfessionalId_StartAt_EndAt
        ON dbo.ProfessionalAbsences (ProfessionalId, StartAt, EndAt);
END;
GO

SELECT CASE WHEN OBJECT_ID('dbo.ProfessionalAbsences', 'U') IS NOT NULL
            THEN 'ProfessionalAbsences = OK' ELSE 'ProfessionalAbsences = FALTA' END AS Resultado;
GO
