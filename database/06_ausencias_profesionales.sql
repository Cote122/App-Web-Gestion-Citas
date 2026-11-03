/*
  HU-14 Vacaciones y licencias del profesional.
  Ejecutar sobre GestionCitasDb ANTES de levantar el backend de esta rama.

  El script es idempotente y compatible con un esquema previo:
  - crea ProfessionalAbsences si no existe;
  - conserva datos existentes;
  - normaliza la columna de tipo a [Type];
  - agrega IsActive y CancelledAt si faltan;
  - elimina la columna heredada AbsenceType una vez migrada;
  - crea el índice si no existe.
*/

USE [GestionCitasDb];
GO

IF OBJECT_ID('dbo.ProfessionalAbsences', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfessionalAbsences
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProfessionalAbsences PRIMARY KEY,
        ProfessionalId UNIQUEIDENTIFIER NOT NULL,
        [Type] NVARCHAR(20) NOT NULL,
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
  Compatibilidad con una versión anterior que usaba AbsenceType.
  Si [Type] aún no existe, se crea nullable, se copian los valores heredados
  y luego se vuelve NOT NULL. Si ya fue creada por una ejecución anterior,
  se preserva el valor real de AbsenceType para filas existentes.
*/
IF COL_LENGTH('dbo.ProfessionalAbsences', 'Type') IS NULL
BEGIN
    ALTER TABLE dbo.ProfessionalAbsences
    ADD [Type] NVARCHAR(20) NULL;
END;
GO

IF COL_LENGTH('dbo.ProfessionalAbsences', 'AbsenceType') IS NOT NULL
BEGIN
    UPDATE dbo.ProfessionalAbsences
    SET [Type] = AbsenceType
    WHERE AbsenceType IS NOT NULL
      AND (
            [Type] IS NULL
            OR LTRIM(RTRIM([Type])) = ''
            OR [Type] = 'Vacaciones'
          );
END;
GO

UPDATE dbo.ProfessionalAbsences
SET [Type] = 'Vacaciones'
WHERE [Type] IS NULL OR LTRIM(RTRIM([Type])) = '';
GO

ALTER TABLE dbo.ProfessionalAbsences
ALTER COLUMN [Type] NVARCHAR(20) NOT NULL;
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

/*
  Una columna heredada NOT NULL que EF ya no conoce bloquea los INSERT.
  Después de copiar sus datos a [Type], se elimina de forma segura.
  Primero se remueve cualquier DEFAULT ligado a AbsenceType.
*/
IF COL_LENGTH('dbo.ProfessionalAbsences', 'AbsenceType') IS NOT NULL
BEGIN
    DECLARE @DefaultConstraint sysname;

    SELECT @DefaultConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.ProfessionalAbsences')
      AND c.name = 'AbsenceType';

    IF @DefaultConstraint IS NOT NULL
        EXEC('ALTER TABLE dbo.ProfessionalAbsences DROP CONSTRAINT [' + @DefaultConstraint + ']');

    ALTER TABLE dbo.ProfessionalAbsences
    DROP COLUMN AbsenceType;
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
    CASE WHEN COL_LENGTH('dbo.ProfessionalAbsences', 'AbsenceType') IS NULL
         THEN 'OK' ELSE 'FALTA ELIMINAR' END AS AbsenceTypeHeredado,
    CASE WHEN COL_LENGTH('dbo.ProfessionalAbsences', 'IsActive') IS NOT NULL
         THEN 'OK' ELSE 'FALTA' END AS IsActive,
    CASE WHEN COL_LENGTH('dbo.ProfessionalAbsences', 'CancelledAt') IS NOT NULL
         THEN 'OK' ELSE 'FALTA' END AS CancelledAt;
GO
