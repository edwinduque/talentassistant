using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentAssistant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyntheticAttendanceDatasets",
                columns: table => new
                {
                    DatasetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadVersion = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyntheticAttendanceDatasets", x => x.DatasetId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyntheticAttendanceDatasets");
        }
    }
}
