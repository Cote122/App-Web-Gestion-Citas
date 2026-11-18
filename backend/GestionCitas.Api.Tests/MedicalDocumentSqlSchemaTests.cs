using System.Text.RegularExpressions;
using GestionCitas.Api.Data;
using GestionCitas.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionCitas.Api.Tests;

// Se ejecutan en SQL Server real en CI. Cada caso crea y elimina su propia base temporal.
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI_SQLSERVER_CONNECTION")))
            Skip = "Requiere CI_SQLSERVER_CONNECTION a una instancia de SQL Server de pruebas.";
    }
}

public class MedicalDocumentSqlSchemaTests
{
    private const string BaseTables = """
        CREATE TABLE dbo.Users (Id uniqueidentifier NOT NULL PRIMARY KEY);
        CREATE TABLE dbo.Appointments (Id uniqueidentifier NOT NULL PRIMARY KEY, PatientId uniqueidentifier NOT NULL);
        INSERT INTO dbo.Users(Id) VALUES ('00000000-0000-0000-0000-000000000001'), ('00000000-0000-0000-0000-000000000002');
        INSERT INTO dbo.Appointments(Id, PatientId) VALUES ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001');
        """;

    // Contrato reportado por sys.columns en GestionCitasDb: independiente de EnsureCreated y del modelo EF.
    private const string ExistingTable = """
        CREATE TABLE dbo.MedicalDocuments (
            Id uniqueidentifier NOT NULL PRIMARY KEY,
            PatientId uniqueidentifier NOT NULL,
            EncounterId uniqueidentifier NULL,
            AppointmentId uniqueidentifier NULL,
            UploadedBy uniqueidentifier NOT NULL,
            DocumentType nvarchar(80) NOT NULL,
            Title nvarchar(200) NOT NULL,
            FileName nvarchar(260) NOT NULL,
            ContentType nvarchar(120) NULL,
            FileSizeBytes bigint NULL,
            StoragePath nvarchar(1000) NOT NULL,
            IsVisibleToPatient bit NOT NULL,
            UploadedAt datetimeoffset NOT NULL
        );
        """;

    [SqlServerFact]
    public async Task Script_CreatesNativeSchema_AndCanBeRepeated()
    {
        await using var database = await TestDatabase.Create();
        await database.Execute(BaseTables);
        await database.RunScript();
        await database.RunScript();
        Assert.Equal(13, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.MedicalDocuments')"));
        Assert.Equal(3, await database.Scalar("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.MedicalDocuments') AND name LIKE 'IX_MedicalDocuments_%'"));
        Assert.Equal(0, database.LastScriptTransactionCount);
    }

    [SqlServerFact]
    public async Task Existing13ColumnSchema_PreservesDataAndIndexes_AndSupportsEfReadAndWrite()
    {
        await using var database = await TestDatabase.Create();
        await database.Execute(BaseTables + ExistingTable + """
            CREATE INDEX ExistingMedicalDocumentTitle ON dbo.MedicalDocuments(Title);
            INSERT INTO dbo.MedicalDocuments (
                Id, PatientId, EncounterId, AppointmentId, UploadedBy, DocumentType, Title,
                FileName, ContentType, FileSizeBytes, StoragePath, IsVisibleToPatient, UploadedAt)
            VALUES ('00000000-0000-0000-0000-000000000005',
                '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000004',
                NULL, '00000000-0000-0000-0000-000000000002', 'Resultado', 'Título anterior',
                'anterior.pdf', NULL, NULL, 'C:\previous\anterior.pdf', 0, '2026-01-02T03:04:05+00:00');
            """);
        await database.RunScript();
        await database.RunScript();

        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(database.ConnectionString).Options);
        var previous = await context.MedicalDocuments.SingleAsync();
        Assert.Equal("Título anterior", previous.Title);
        Assert.Equal("Resultado", previous.DocumentType);
        Assert.Equal("C:\\previous\\anterior.pdf", previous.StorageKey);
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T03:04:05+00:00"), previous.CreatedAt);
        Assert.Null(previous.AppointmentId);
        Assert.NotNull(previous.EncounterId);
        Assert.Null(previous.ContentType);
        Assert.Null(previous.SizeBytes);
        Assert.False(previous.IsVisibleToPatient);

        var added = new MedicalDocument
        {
            PatientId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            AppointmentId = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            UploadedByUserId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Title = "Documento nuevo", FileName = "nuevo.pdf", ContentType = "application/pdf",
            SizeBytes = 25, StorageKey = Guid.NewGuid().ToString("N"), IsVisibleToPatient = true
        };
        context.MedicalDocuments.Add(added);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(added.StorageKey, (await context.MedicalDocuments.SingleAsync(x => x.Id == added.Id)).StorageKey);
        Assert.Equal(13, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.MedicalDocuments')"));
        Assert.Equal(1, await database.Scalar("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.MedicalDocuments') AND name='ExistingMedicalDocumentTitle'"));
    }

    [SqlServerFact]
    public async Task IncompatibleSchema_ProducesControlledError_WithoutChangingIt()
    {
        await using var database = await TestDatabase.Create();
        await database.Execute(BaseTables + ExistingTable.Replace("UploadedAt", "PreviousUploadedAt"));
        var error = await Assert.ThrowsAsync<SqlException>(() => database.RunScript());
        Assert.Equal(50002, error.Number);
        Assert.Contains("UploadedAt", error.Message);
        Assert.Equal(0, database.LastScriptTransactionCount);
        Assert.Equal(1, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.MedicalDocuments') AND name='PreviousUploadedAt'"));
        Assert.Equal(0, await database.Scalar("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.MedicalDocuments') AND name LIKE 'IX_MedicalDocuments_%'"));
    }

    [SqlServerFact]
    public async Task ContainedDatabase_WithDifferentDataAndCatalogCollations_CanRunScriptTwice()
    {
        await using var database = await TestDatabase.Create(contained: true);
        await database.Execute(BaseTables + ExistingTable);
        var dataCollation = await database.TextScalar("SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'))");
        var catalogCollation = await database.TextScalar("SELECT CONVERT(nvarchar(128), SQL_VARIANT_PROPERTY(name, 'Collation')) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Users') AND name=N'Id'");
        Assert.Equal("SQL_Latin1_General_CP1_CI_AS", dataCollation);
        Assert.NotEqual(dataCollation, catalogCollation);

        await database.RunScript();
        await database.RunScript();

        Assert.Equal(13, await database.Scalar("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.MedicalDocuments')"));
        Assert.Equal(3, await database.Scalar("SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.MedicalDocuments') AND name LIKE 'IX_MedicalDocuments_%'"));
        Assert.Equal(0, database.LastScriptTransactionCount);
    }

    internal sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string name = "GestionCitas_HU18_Test_" + Guid.NewGuid().ToString("N");
        private readonly string masterConnection;
        public string ConnectionString { get; }
        public int LastScriptTransactionCount { get; private set; } = -1;

        private TestDatabase(string connection)
        {
            var builder = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master", Pooling = false };
            masterConnection = builder.ConnectionString;
            builder.InitialCatalog = name;
            ConnectionString = builder.ConnectionString;
        }

        public static async Task<TestDatabase> Create(bool contained = false)
        {
            var database = new TestDatabase(Environment.GetEnvironmentVariable("CI_SQLSERVER_CONNECTION")!);
            await using var connection = new SqlConnection(database.masterConnection);
            await connection.OpenAsync();
            var containment = contained ? " CONTAINMENT = PARTIAL COLLATE SQL_Latin1_General_CP1_CI_AS" : "";
            await using var command = new SqlCommand($"CREATE DATABASE [{database.name}]" + containment, connection);
            await command.ExecuteNonQueryAsync();
            return database;
        }

        public async Task Execute(string sql)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<int> Scalar(string sql)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task<string> TextScalar(string sql)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
        }

        public async Task RunScript(string fileName = "09_documentos_medicos.sql")
        {
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SqlScripts", fileName));
            script = script.Replace("USE [GestionCitasDb];", $"USE [{name}];");
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            try
            {
                foreach (var batch in Regex.Split(script, @"^[\t ]*GO[\t ]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    await using var command = new SqlCommand(batch, connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                await using var check = new SqlCommand("SELECT @@TRANCOUNT", connection);
                LastScriptTransactionCount = Convert.ToInt32(await check.ExecuteScalarAsync());
            }
        }

        public async ValueTask DisposeAsync()
        {
            // El nombre se genera internamente: nunca se elimina GestionCitasDb ni otra base compartida.
            await using var connection = new SqlConnection(masterConnection);
            await connection.OpenAsync();
            await using var command = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
