-- HU-16: valida las tablas existentes de recetas/indicaciones y agrega índices no únicos.
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
    IF OBJECT_ID('dbo.Prescriptions','U') IS NULL OR OBJECT_ID('dbo.PrescriptionItems','U') IS NULL
       OR OBJECT_ID('dbo.ClinicalEncounters','U') IS NULL OR OBJECT_ID('dbo.MedicalRecords','U') IS NULL
        THROW 50021, 'Faltan tablas del catálogo de recetas o atenciones. Revisa la base antes de continuar.', 1;

    DECLARE @Required TABLE (
        TableName SYSNAME COLLATE CATALOG_DEFAULT NOT NULL,
        Name SYSNAME COLLATE CATALOG_DEFAULT NOT NULL,
        TypeName SYSNAME COLLATE CATALOG_DEFAULT NOT NULL,
        MinLengthBytes INT NULL, IsNullable BIT NOT NULL
    );
    INSERT INTO @Required VALUES
        ('Prescriptions','Id','uniqueidentifier',NULL,0),
        ('Prescriptions','EncounterId','uniqueidentifier',NULL,0),
        ('Prescriptions','PatientId','uniqueidentifier',NULL,0),
        ('Prescriptions','ProfessionalId','uniqueidentifier',NULL,0),
        ('Prescriptions','GeneralIndications','nvarchar',-1,1),
        ('Prescriptions','IssuedAt','datetimeoffset',NULL,0),
        ('Prescriptions','IsActive','bit',NULL,0),
        ('PrescriptionItems','Id','uniqueidentifier',NULL,0),
        ('PrescriptionItems','PrescriptionId','uniqueidentifier',NULL,0),
        ('PrescriptionItems','Medication','nvarchar',400,0),
        ('PrescriptionItems','Dose','nvarchar',200,1),
        ('PrescriptionItems','Frequency','nvarchar',200,1),
        ('PrescriptionItems','Duration','nvarchar',200,1),
        ('PrescriptionItems','Instructions','nvarchar',1000,1),
        ('PrescriptionItems','SortOrder','int',NULL,0),
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
        ('ClinicalEncounters','UpdatedAt','datetimeoffset',NULL,1),
        ('MedicalRecords','Id','uniqueidentifier',NULL,0),
        ('MedicalRecords','PatientId','uniqueidentifier',NULL,0),
        ('MedicalRecords','ProfessionalId','uniqueidentifier',NULL,0),
        ('MedicalRecords','AppointmentId','uniqueidentifier',NULL,0),
        ('MedicalRecords','Observations','nvarchar',4000,1),
        ('MedicalRecords','Diagnosis','nvarchar',1000,1),
        ('MedicalRecords','Recommendations','nvarchar',2000,1),
        ('MedicalRecords','AttentionDate','datetimeoffset',NULL,0),
        ('MedicalRecords','CreatedAt','datetimeoffset',NULL,0),
        ('MedicalRecords','UpdatedAt','datetimeoffset',NULL,1);
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
        THROW 50022, @Message, 1;
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Prescriptions') AND name='IX_Prescriptions_PatientId_IssuedAt')
        EXEC sys.sp_executesql N'CREATE INDEX IX_Prescriptions_PatientId_IssuedAt ON dbo.Prescriptions(PatientId, IssuedAt);';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Prescriptions') AND name='IX_Prescriptions_EncounterId')
        EXEC sys.sp_executesql N'CREATE INDEX IX_Prescriptions_EncounterId ON dbo.Prescriptions(EncounterId);';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PrescriptionItems') AND name='IX_PrescriptionItems_PrescriptionId_SortOrder')
        EXEC sys.sp_executesql N'CREATE INDEX IX_PrescriptionItems_PrescriptionId_SortOrder ON dbo.PrescriptionItems(PrescriptionId, SortOrder);';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.MedicalRecords') AND name='IX_MedicalRecords_PatientId_AttentionDate')
        EXEC sys.sp_executesql N'CREATE INDEX IX_MedicalRecords_PatientId_AttentionDate ON dbo.MedicalRecords(PatientId, AttentionDate);';
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SELECT DB_NAME() AS BaseActual, 'OK' AS Prescriptions, 'OK' AS PrescriptionItems, 'OK' AS ClinicalEncounters, 'OK' AS MedicalRecords;
GO
