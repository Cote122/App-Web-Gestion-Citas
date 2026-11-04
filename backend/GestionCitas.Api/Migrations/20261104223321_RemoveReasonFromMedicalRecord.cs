using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionCitas.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReasonFromMedicalRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "MedicalRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "MedicalRecords",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
