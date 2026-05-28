using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformAI.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddChartsJsonToMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChartsJson",
                table: "Message",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChartsJson",
                table: "Message");
        }
    }
}
