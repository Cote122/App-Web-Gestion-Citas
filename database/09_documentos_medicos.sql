-- HU-18: compatible con el catálogo clínico de 13 columnas existente en GestionCitasDb.
-- Solo crea la tabla cuando falta y agrega índices. No modifica columnas ni registros existentes.
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
    IF OBJECT_ID('dbo.Appointments', 'U') IS NULL OR OBJECT_ID('dbo.Users', 'U') IS NULL
        THROW 50001, 'Faltan tablas base. Verifica la base de datos y los scripts anteriores.', 1;

    IF OBJECT_ID('dbo.MedicalDocuments', 'U') IS NULL
    BEGIN
        -- DDL en otro nivel de ejecución permite capturar sus errores con TRY/CATCH.
        EXEC sys.sp_executesql N'
        CREATE TABLE dbo.MedicalDocuments
        (
            Id UNIQUEIDENTIFIER NOT NULL,
            PatientId UNIQUEIDENTIFIER NOT NULL,
            EncounterId UNIQUEIDENTIFIER NULL,
            AppointmentId UNIQUEIDENTIFIER NULL,
            UploadedBy UNIQUEIDENTIFIER NOT NULL,
            DocumentType NVARCHAR(80) NOT NULL,
            Title NVARCHAR(200) NOT NULL,
            FileName NVARCHAR(260) NOT NULL,
            ContentType NVARCHAR(120) NULL,
            FileSizeBytes BIGINT NULL,
            StoragePath NVARCHAR(1000) NOT NULL,
            IsVisibleToPatient BIT NOT NULL CONSTRAINT DF_MedicalDocuments_IsVisibleToPatient DEFAULT (0),
            UploadedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_MedicalDocuments_UploadedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_MedicalDocuments PRIMARY KEY (Id),
            CONSTRAINT FK_MedicalDocuments_Users_PatientId
                FOREIGN KEY (PatientId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_MedicalDocuments_Appointments_AppointmentId
                FOREIGN KEY (AppointmentId) REFERENCES dbo.Appointments(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_MedicalDocuments_Users_UploadedBy
                FOREIGN KEY (UploadedBy) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
            CONSTRAINT CK_MedicalDocuments_FileSizeBytes CHECK (FileSizeBytes IS NULL OR FileSizeBytes > 0)
        );';
    END;

    DECLARE @Required TABLE
    (
        Name SYSNAME NOT NULL,
        TypeName SYSNAME NOT NULL,
        MinLengthBytes INT NULL,
        IsNullable BIT NOT NULL
    );
    INSERT INTO @Required (Name, TypeName, MinLengthBytes, IsNullable) VALUES
        ('Id', 'uniqueidentifier', NULL, 0),
        ('PatientId', 'uniqueidentifier', NULL, 0),
        ('EncounterId', 'uniqueidentifier', NULL, 1),
        ('AppointmentId', 'uniqueidentifier', NULL, 1),
        ('UploadedBy', 'uniqueidentifier', NULL, 0),
        ('DocumentType', 'nvarchar', 160, 0),
        ('Title', 'nvarchar', 400, 0),
        ('FileName', 'nvarchar', 520, 0),
        ('ContentType', 'nvarchar', 240, 1),
        ('FileSizeBytes', 'bigint', NULL, 1),
        ('StoragePath', 'nvarchar', 2000, 0),
        ('IsVisibleToPatient', 'bit', NULL, 0),
        ('UploadedAt', 'datetimeoffset', NULL, 0);

    DECLARE @Incompatible NVARCHAR(1800);
    SELECT @Incompatible = STRING_AGG(CONVERT(NVARCHAR(MAX), QUOTENAME(r.Name)), ', ')
    FROM @Required AS r
    LEFT JOIN sys.columns AS c
      ON c.object_id = OBJECT_ID('dbo.MedicalDocuments', 'U') AND c.name = r.Name
    WHERE c.column_id IS NULL OR c.system_type_id <> TYPE_ID(r.TypeName)
       OR c.is_nullable <> r.IsNullable OR c.is_computed = 1 OR c.is_identity = 1
       OR (r.MinLengthBytes IS NOT NULL AND c.max_length <> -1 AND c.max_length < r.MinLengthBytes);

    IF @Incompatible IS NOT NULL
    BEGIN
        DECLARE @Message NVARCHAR(2048) = N'MedicalDocuments tiene un esquema incompatible. Revisar estas columnas antes de continuar: ' + @Incompatible;
        THROW 50002, @Message, 1;
    END;

    -- Índices no únicos: no cambian ni exigen deduplicar documentos existentes.
    -- Compilación diferida hasta después de validar las columnas.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MedicalDocuments')
                   AND name = 'IX_MedicalDocuments_AppointmentId_UploadedAt')
        EXEC sys.sp_executesql N'CREATE INDEX IX_MedicalDocuments_AppointmentId_UploadedAt ON dbo.MedicalDocuments(AppointmentId, UploadedAt);';

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MedicalDocuments')
                   AND name = 'IX_MedicalDocuments_PatientId_UploadedAt')
        EXEC sys.sp_executesql N'CREATE INDEX IX_MedicalDocuments_PatientId_UploadedAt ON dbo.MedicalDocuments(PatientId, UploadedAt);';

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MedicalDocuments')
                   AND name = 'IX_MedicalDocuments_UploadedBy')
        EXEC sys.sp_executesql N'CREATE INDEX IX_MedicalDocuments_UploadedBy ON dbo.MedicalDocuments(UploadedBy);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT DB_NAME() AS BaseActual, 'OK' AS MedicalDocuments;
GO
