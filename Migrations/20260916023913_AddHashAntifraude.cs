using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GetStartedApp.Migrations
{
    /// <inheritdoc />
    public partial class AddHashAntifraude : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HashSeguranca",
                table: "Vendas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HashSeguranca",
                table: "Vendas");
        }
    }
}
