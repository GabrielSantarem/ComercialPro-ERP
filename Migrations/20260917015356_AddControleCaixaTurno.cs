using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GetStartedApp.Migrations
{
    /// <inheritdoc />
    public partial class AddControleCaixaTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaixasTurno",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VendedorId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataAbertura = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataFechamento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SaldoInicial = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalVendasDinheiro = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalVendasOutros = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalSuprimentos = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalSangrias = table.Column<decimal>(type: "TEXT", nullable: false),
                    SaldoInformado = table.Column<decimal>(type: "TEXT", nullable: true),
                    DiferencaQuebra = table.Column<decimal>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaixasTurno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaixasTurno_Vendedores_VendedorId",
                        column: x => x.VendedorId,
                        principalTable: "Vendedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesCaixa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaixaTurnoId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentacoesCaixa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentacoesCaixa_CaixasTurno_CaixaTurnoId",
                        column: x => x.CaixaTurnoId,
                        principalTable: "CaixasTurno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaixasTurno_VendedorId",
                table: "CaixasTurno",
                column: "VendedorId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesCaixa_CaixaTurnoId",
                table: "MovimentacoesCaixa",
                column: "CaixaTurnoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentacoesCaixa");

            migrationBuilder.DropTable(
                name: "CaixasTurno");
        }
    }
}
