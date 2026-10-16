using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TalentAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyntheticAttendanceDataset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyntheticAttendanceDatasets",
                columns: table => new
                {
                    DatasetId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    PayloadVersion = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyntheticAttendanceDatasets", x => x.DatasetId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyntheticAttendanceDatasets");
        }
    }
}
