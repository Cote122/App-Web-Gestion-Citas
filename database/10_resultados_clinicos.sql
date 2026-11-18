-- HU-15: valida el catálogo clínico existente y agrega índices no únicos.
-- No modifica columnas, defaults, estados ni registros. Requiere las tablas clínicas del proyecto.
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
    IF OBJECT_ID('dbo.ClinicalResults', 'U') IS NULL OR OBJECT_ID('dbo.ClinicalResultFiles', 'U') IS NULL
       OR OBJECT_ID('dbo.ClinicalEncounters', 'U') IS NULL
        THROW 50011, 'Faltan ClinicalResults, ClinicalResultFiles o ClinicalEncounters. Instala el catálogo clínico antes de HU-15.', 1;

    DECLARE @Required TABLE (
        TableName SYSNAME COLLATE CATALOG_DEFAULT NOT NULL,
        Name SYSNAME COLLATE CATALOG_DEFAULT NOT NULL,
        TypeName SYSNAME COLLATE CATALOG_DEFAULT NOT NULL,
        MinLengthBytes INT NULL, IsNullable BIT NOT NULL
    );
    INSERT INTO @Required VALUES
        ('ClinicalResults','Id','uniqueidentifier',NULL,0),
        ('ClinicalResults','PatientId','uniqueidentifier',NULL,0),
        ('ClinicalResults','EncounterId','uniqueidentifier',NULL,1),
        ('ClinicalResults','AppointmentId','uniqueidentifier',NULL,1),
        ('ClinicalResults','ResultType','nvarchar',200,0),
        ('ClinicalResults','Title','nvarchar',400,0),
        ('ClinicalResults','Summary','nvarchar',-1,1),
        ('ClinicalResults','Status','nvarchar',60,0),
        ('ClinicalResults','IsVisibleToPatient','bit',NULL,0),
        ('ClinicalResults','ResultDate','datetimeoffset',NULL,1),
        ('ClinicalResults','PublishedAt','datetimeoffset',NULL,1),
        ('ClinicalResults','CreatedBy','uniqueidentifier',NULL,1),
        ('ClinicalResults','CreatedAt','datetimeoffset',NULL,0),
        ('ClinicalResultFiles','Id','uniqueidentifier',NULL,0),
        ('ClinicalResultFiles','ClinicalResultId','uniqueidentifier',NULL,0),
        ('ClinicalResultFiles','FileName','nvarchar',520,0),
        ('ClinicalResultFiles','ContentType','nvarchar',240,1),
        ('ClinicalResultFiles','FileSizeBytes','bigint',NULL,1),
        ('ClinicalResultFiles','StoragePath','nvarchar',2000,0),
        ('ClinicalResultFiles','FileHash','nvarchar',256,1),
        ('ClinicalResultFiles','UploadedAt','datetimeoffset',NULL,0),
        ('ClinicalEncounters','Id','uniqueidentifier',NULL,0),
        ('ClinicalEncounters','AppointmentId','uniqueidentifier',NULL,0),
        ('ClinicalEncounters','PatientId','uniqueidentifier',NULL,0),
        ('ClinicalEncounters','ProfessionalId','uniqueidentifier',NULL,0),
        ('ClinicalEncounters','StartedAt','datetimeoffset',NULL,1),
        ('ClinicalEncounters','FinishedAt','datetimeoffset',NULL,1),
        ('ClinicalEncounters','Notes','nvarchar',-1,1),
        ('ClinicalEncounters','DiagnosisSummary','nvarchar',-1,1),
        ('ClinicalEncounters','Indications','nvarchar',-1,1),
        ('ClinicalEncounters','Status','nvarchar',60,0),
        ('ClinicalEncounters','CreatedAt','datetimeoffset',NULL,0),
        ('ClinicalEncounters','UpdatedAt','datetimeoffset',NULL,1);
    DECLARE @Incompatible NVARCHAR(1800);
    SELECT @Incompatible = STRING_AGG(CONVERT(NVARCHAR(MAX), QUOTENAME(r.TableName) + '.' + QUOTENAME(r.Name)), ', ')
    FROM @Required r LEFT JOIN sys.columns c
      ON c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(r.TableName), 'U')
     AND c.name COLLATE CATALOG_DEFAULT = r.Name COLLATE CATALOG_DEFAULT
    WHERE c.column_id IS NULL OR c.system_type_id <> TYPE_ID(r.TypeName)
       OR c.is_nullable <> r.IsNullable OR c.is_computed = 1 OR c.is_identity = 1
       OR (r.MinLengthBytes = -1 AND c.max_length <> -1)
       OR (r.MinLengthBytes > 0 AND c.max_length <> -1 AND c.max_length < r.MinLengthBytes);
    IF @Incompatible IS NOT NULL
    BEGIN
        DECLARE @Message NVARCHAR(2048) = N'Catálogo clínico incompatible. Revisa estas columnas: ' + @Incompatible;
        THROW 50012, @Message, 1;
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ClinicalResults') AND name='IX_ClinicalResults_PatientId_PublishedAt')
        EXEC sys.sp_executesql N'CREATE INDEX IX_ClinicalResults_PatientId_PublishedAt ON dbo.ClinicalResults(PatientId, PublishedAt);';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ClinicalResults') AND name='IX_ClinicalResults_AppointmentId')
        EXEC sys.sp_executesql N'CREATE INDEX IX_ClinicalResults_AppointmentId ON dbo.ClinicalResults(AppointmentId);';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ClinicalResultFiles') AND name='IX_ClinicalResultFiles_ClinicalResultId')
        EXEC sys.sp_executesql N'CREATE INDEX IX_ClinicalResultFiles_ClinicalResultId ON dbo.ClinicalResultFiles(ClinicalResultId);';
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SELECT DB_NAME() AS BaseActual, 'OK' AS ClinicalResults, 'OK' AS ClinicalResultFiles, 'OK' AS ClinicalEncounters;
GO
