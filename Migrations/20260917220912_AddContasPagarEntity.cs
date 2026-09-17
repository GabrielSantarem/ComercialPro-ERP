using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GetStartedApp.Migrations
{
    /// <inheritdoc />
    public partial class AddContasPagarEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContasPagar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntradaMercadoriaId = table.Column<int>(type: "INTEGER", nullable: true),
                    FornecedorNome = table.Column<string>(type: "TEXT", nullable: false),
                    FornecedorCnpj = table.Column<string>(type: "TEXT", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "TEXT", nullable: false),
                    NumeroParcela = table.Column<string>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    DataEmissao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataVencimento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValorPago = table.Column<decimal>(type: "TEXT", nullable: true),
                    FormaPagamento = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasPagar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContasPagar_EntradasMercadoria_EntradaMercadoriaId",
                        column: x => x.EntradaMercadoriaId,
                        principalTable: "EntradasMercadoria",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContasPagar_EntradaMercadoriaId",
                table: "ContasPagar",
                column: "EntradaMercadoriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContasPagar");
        }
    }
}
