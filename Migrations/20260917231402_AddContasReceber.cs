using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GetStartedApp.Migrations
{
    /// <inheritdoc />
    public partial class AddContasReceber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContasReceber",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VendaId = table.Column<int>(type: "INTEGER", nullable: true),
                    PedidoBalcaoId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClienteNome = table.Column<string>(type: "TEXT", nullable: false),
                    ClienteCpfCnpj = table.Column<string>(type: "TEXT", nullable: false),
                    ClienteTelefone = table.Column<string>(type: "TEXT", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "TEXT", nullable: false),
                    NumeroParcela = table.Column<string>(type: "TEXT", nullable: false),
                    ValorOriginal = table.Column<decimal>(type: "TEXT", nullable: false),
                    JurosMulta = table.Column<decimal>(type: "TEXT", nullable: false),
                    Desconto = table.Column<decimal>(type: "TEXT", nullable: false),
                    DataEmissao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataVencimento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataRecebimento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ValorRecebido = table.Column<decimal>(type: "TEXT", nullable: true),
                    FormaRecebimento = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Observacao = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContasReceber", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContasReceber_PedidosBalcao_PedidoBalcaoId",
                        column: x => x.PedidoBalcaoId,
                        principalTable: "PedidosBalcao",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContasReceber_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_PedidoBalcaoId",
                table: "ContasReceber",
                column: "PedidoBalcaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_VendaId",
                table: "ContasReceber",
                column: "VendaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContasReceber");
        }
    }
}
