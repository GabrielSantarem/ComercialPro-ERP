using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GetStartedApp.Migrations
{
    /// <inheritdoc />
    public partial class AddHardwareGavetaBalancaConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcionarGavetaAutomaticamente",
                table: "ConfiguracoesTerminal",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModoBalancaEtiqueta",
                table: "ConfiguracoesTerminal",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TamanhoCodigoBalanca",
                table: "ConfiguracoesTerminal",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UsarEmuladorBalanca",
                table: "ConfiguracoesTerminal",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcionarGavetaAutomaticamente",
                table: "ConfiguracoesTerminal");

            migrationBuilder.DropColumn(
                name: "ModoBalancaEtiqueta",
                table: "ConfiguracoesTerminal");

            migrationBuilder.DropColumn(
                name: "TamanhoCodigoBalanca",
                table: "ConfiguracoesTerminal");

            migrationBuilder.DropColumn(
                name: "UsarEmuladorBalanca",
                table: "ConfiguracoesTerminal");
        }
    }
}
