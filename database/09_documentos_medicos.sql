-- HU-18: solo metadatos; los archivos se guardan en una carpeta privada del hosting.
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
        CREATE TABLE dbo.MedicalDocuments
        (
            Id UNIQUEIDENTIFIER NOT NULL,
            AppointmentId UNIQUEIDENTIFIER NOT NULL,
            UploadedByUserId UNIQUEIDENTIFIER NOT NULL,
            FileName NVARCHAR(255) NOT NULL,
            StorageKey NVARCHAR(32) NOT NULL,
            ContentType NVARCHAR(100) NOT NULL,
            SizeBytes BIGINT NOT NULL,
            CreatedAt DATETIMEOFFSET NOT NULL,
            CONSTRAINT PK_MedicalDocuments PRIMARY KEY (Id),
            CONSTRAINT FK_MedicalDocuments_Appointments_AppointmentId
                FOREIGN KEY (AppointmentId) REFERENCES dbo.Appointments(Id) ON DELETE NO ACTION,
            CONSTRAINT FK_MedicalDocuments_Users_UploadedByUserId
                FOREIGN KEY (UploadedByUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
            CONSTRAINT CK_MedicalDocuments_SizeBytes CHECK (SizeBytes > 0)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MedicalDocuments')
                   AND name = 'IX_MedicalDocuments_AppointmentId_CreatedAt')
        CREATE INDEX IX_MedicalDocuments_AppointmentId_CreatedAt ON dbo.MedicalDocuments(AppointmentId, CreatedAt);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MedicalDocuments')
                   AND name = 'IX_MedicalDocuments_UploadedByUserId')
        CREATE INDEX IX_MedicalDocuments_UploadedByUserId ON dbo.MedicalDocuments(UploadedByUserId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MedicalDocuments')
                   AND name = 'IX_MedicalDocuments_StorageKey')
        CREATE UNIQUE INDEX IX_MedicalDocuments_StorageKey ON dbo.MedicalDocuments(StorageKey);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT DB_NAME() AS BaseActual,
    CASE WHEN OBJECT_ID('dbo.MedicalDocuments', 'U') IS NOT NULL THEN 'OK' ELSE 'FALTA' END AS MedicalDocuments;
GO
