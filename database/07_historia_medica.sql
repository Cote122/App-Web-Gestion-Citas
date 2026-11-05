BEGIN TRANSACTION;
ALTER TABLE [AppointmentAuditLogs] DROP CONSTRAINT [FK_AppointmentAuditLogs_Appointments_AppointmentId];

ALTER TABLE [Appointments] ADD [Reason] nvarchar(500) NULL;

CREATE TABLE [MedicalRecords] (
    [Id] uniqueidentifier NOT NULL,
    [PatientId] uniqueidentifier NOT NULL,
    [ProfessionalId] uniqueidentifier NOT NULL,
    [AppointmentId] uniqueidentifier NOT NULL,
    [Observations] nvarchar(2000) NULL,
    [Diagnosis] nvarchar(500) NULL,
    [Recommendations] nvarchar(1000) NULL,
    [AttentionDate] datetimeoffset NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    CONSTRAINT [PK_MedicalRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MedicalRecords_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MedicalRecords_Professionals_ProfessionalId] FOREIGN KEY ([ProfessionalId]) REFERENCES [Professionals] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_MedicalRecords_Users_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_AppointmentAuditLogs_AppointmentId] ON [AppointmentAuditLogs] ([AppointmentId]);

CREATE INDEX [IX_MedicalRecords_AppointmentId] ON [MedicalRecords] ([AppointmentId]);

CREATE INDEX [IX_MedicalRecords_PatientId] ON [MedicalRecords] ([PatientId]);

CREATE INDEX [IX_MedicalRecords_ProfessionalId] ON [MedicalRecords] ([ProfessionalId]);

ALTER TABLE [AppointmentAuditLogs] ADD CONSTRAINT [FK_AppointmentAuditLogs_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [Appointments] ([Id]) ON DELETE NO ACTION;

COMMIT;
GO

