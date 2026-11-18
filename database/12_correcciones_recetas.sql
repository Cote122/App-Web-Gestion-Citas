-- HU-16: historial de correcciones de recetas realizadas por un administrador.
-- Crea una tabla de auditoría independiente; conserva las tablas y los datos de recetas existentes.
USE [GestionCitasDb];
GO

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR('Cierra la transacción abierta antes de ejecutar este script.', 16, 1);
    RETURN;
END;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID('dbo.Prescriptions', 'U') IS NULL OR OBJECT_ID('dbo.Users', 'U') IS NULL
        THROW 50041, 'Faltan Prescriptions o Users. Verifica la base y los scripts anteriores.', 1;
    IF OBJECT_ID('dbo.PrescriptionCorrections','U') IS NULL
        EXEC sys.sp_executesql N'
        CREATE TABLE dbo.PrescriptionCorrections (
            Id uniqueidentifier NOT NULL PRIMARY KEY,
            PrescriptionId uniqueidentifier NOT NULL,
            CorrectedBy uniqueidentifier NOT NULL,
            CorrectedAt datetimeoffset NOT NULL,
            Reason nvarchar(1000) NOT NULL,
            PreviousContent nvarchar(max) NOT NULL,
            UpdatedContent nvarchar(max) NOT NULL,
            PreviousVersion nvarchar(64) NOT NULL,
            UpdatedVersion nvarchar(64) NOT NULL,
            CONSTRAINT FK_PrescriptionCorrections_Prescriptions FOREIGN KEY (PrescriptionId) REFERENCES dbo.Prescriptions(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_PrescriptionCorrections_Users FOREIGN KEY (CorrectedBy) REFERENCES dbo.Users(Id) ON DELETE NO ACTION
        );';

    DECLARE @Required TABLE
    (
        Name SYSNAME COLLATE CATALOG_DEFAULT NOT NULL,
        TypeName SYSNAME COLLATE CATALOG_DEFAULT NOT NULL,
        MinLengthBytes INT NULL,
        IsNullable BIT NOT NULL
    );
    INSERT INTO @Required (Name, TypeName, MinLengthBytes, IsNullable) VALUES
        ('Id','uniqueidentifier',NULL,0),
        ('PrescriptionId','uniqueidentifier',NULL,0),
        ('CorrectedBy','uniqueidentifier',NULL,0),
        ('CorrectedAt','datetimeoffset',NULL,0),
        ('Reason','nvarchar',2000,0),
        ('PreviousContent','nvarchar',-1,0),
        ('UpdatedContent','nvarchar',-1,0),
        ('PreviousVersion','nvarchar',128,0),
        ('UpdatedVersion','nvarchar',128,0);

    DECLARE @Incompatible NVARCHAR(1800);
    SELECT @Incompatible = STRING_AGG(CONVERT(NVARCHAR(MAX), QUOTENAME(r.Name)), ', ')
    FROM @Required AS r
    LEFT JOIN sys.columns AS c
      ON c.object_id = OBJECT_ID('dbo.PrescriptionCorrections', 'U')
     AND c.name COLLATE CATALOG_DEFAULT = r.Name COLLATE CATALOG_DEFAULT
    WHERE c.column_id IS NULL OR c.system_type_id <> TYPE_ID(r.TypeName)
       OR c.is_nullable <> r.IsNullable OR c.is_computed = 1 OR c.is_identity = 1
       OR (r.MinLengthBytes = -1 AND c.max_length <> -1)
       OR (r.MinLengthBytes > 0 AND c.max_length <> -1 AND c.max_length < r.MinLengthBytes);

    IF @Incompatible IS NOT NULL
    BEGIN
        DECLARE @Message NVARCHAR(2048) = N'PrescriptionCorrections tiene un esquema incompatible. Revisar estas columnas antes de continuar: ' + @Incompatible;
        THROW 50042, @Message, 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PrescriptionCorrections') AND name='IX_PrescriptionCorrections_PrescriptionId_CorrectedAt')
        EXEC sys.sp_executesql N'CREATE INDEX IX_PrescriptionCorrections_PrescriptionId_CorrectedAt ON dbo.PrescriptionCorrections(PrescriptionId,CorrectedAt);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT DB_NAME() AS BaseActual, 'OK' AS PrescriptionCorrections;
GO
