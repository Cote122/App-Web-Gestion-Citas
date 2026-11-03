/*
  HU-14 Vacaciones y licencias del profesional.
  Ejecutar sobre GestionCitasDb ANTES de levantar el backend de esta rama.

  El script es idempotente:
  - crea ProfessionalAbsences si no existe;
  - si existe con un esquema anterior, agrega las columnas faltantes;
  - crea el índice si no existe;
  - no elimina datos existentes.
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

/*
  Compatibilidad con una versión previa de ProfessionalAbsences.
  WITH VALUES permite agregar columnas NOT NULL sin romper filas existentes.
*/
IF COL_LENGTH('dbo.ProfessionalAbsences', 'Type') IS NULL
BEGIN
    ALTER TABLE dbo.ProfessionalAbsences
    ADD Type NVARCHAR(20) NOT NULL
        CONSTRAINT DF_ProfessionalAbsences_Type DEFAULT ('Vacaciones') WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.ProfessionalAbsences', 'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.ProfessionalAbsences
    ADD IsActive BIT NOT NULL
        CONSTRAINT DF_ProfessionalAbsences_IsActive DEFAULT (1) WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.ProfessionalAbsences', 'CancelledAt') IS NULL
BEGIN
    ALTER TABLE dbo.ProfessionalAbsences
    ADD CancelledAt DATETIMEOFFSET NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ProfessionalAbsences_ProfessionalId_StartAt_EndAt'
      AND object_id = OBJECT_ID('dbo.ProfessionalAbsences')
)
BEGIN
    CREATE INDEX IX_ProfessionalAbsences_ProfessionalId_StartAt_EndAt
        ON dbo.ProfessionalAbsences (ProfessionalId, StartAt, EndAt);
END;
GO

SELECT
    CASE WHEN OBJECT_ID('dbo.ProfessionalAbsences', 'U') IS NOT NULL
         THEN 'OK' ELSE 'FALTA' END AS Tabla,
    CASE WHEN COL_LENGTH('dbo.ProfessionalAbsences', 'Type') IS NOT NULL
         THEN 'OK' ELSE 'FALTA' END AS [Type],
    CASE WHEN COL_LENGTH('dbo.ProfessionalAbsences', 'IsActive') IS NOT NULL
         THEN 'OK' ELSE 'FALTA' END AS IsActive,
    CASE WHEN COL_LENGTH('dbo.ProfessionalAbsences', 'CancelledAt') IS NOT NULL
         THEN 'OK' ELSE 'FALTA' END AS CancelledAt;
GO
