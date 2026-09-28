using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceProcessor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractionModelChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionModelChanges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionModelChanges_ChangedAt",
                table: "ExtractionModelChanges",
                column: "ChangedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtractionModelChanges");
        }
    }
}
