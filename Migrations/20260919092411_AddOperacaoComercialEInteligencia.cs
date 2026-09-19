using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GetStartedApp.Migrations
{
    /// <inheritdoc />
    public partial class AddOperacaoComercialEInteligencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PercentualComissao",
                table: "Vendedores",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ChaveAcessoNfce",
                table: "Vendas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataHoraCancelamento",
                table: "Vendas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaPagamento",
                table: "Vendas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "JustificativaCancelamento",
                table: "Vendas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NumeroNfce",
                table: "Vendas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtocoloAutorizacaoNfce",
                table: "Vendas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtocoloCancelamento",
                table: "Vendas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SerieNfce",
                table: "Vendas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Vendas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "XmlCancelamento",
                table: "Vendas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "XmlNfce",
                table: "Vendas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataCadastro",
                table: "Produtos",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "ContasReceber",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    CpfCnpj = table.Column<string>(type: "TEXT", nullable: true),
                    Telefone = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Endereco = table.Column<string>(type: "TEXT", nullable: true),
                    LimiteCredito = table.Column<decimal>(type: "TEXT", nullable: false),
                    BloqueadoManualmente = table.Column<bool>(type: "INTEGER", nullable: false),
                    Bloqueado = table.Column<bool>(type: "INTEGER", nullable: false),
                    MotivoBloqueio = table.Column<string>(type: "TEXT", nullable: true),
                    DataCadastro = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesTerminal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NomeEstacao = table.Column<string>(type: "TEXT", nullable: false),
                    ModeloImpressora = table.Column<string>(type: "TEXT", nullable: false),
                    LarguraBobina = table.Column<string>(type: "TEXT", nullable: false),
                    PortaComunicacao = table.Column<string>(type: "TEXT", nullable: false),
                    CortarPapelAutomatico = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModoImpressaoPadrao = table.Column<string>(type: "TEXT", nullable: false),
                    ImprimirComandaBalcaoAutomatico = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntegracaoBalancaHabilitada = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModeloBalanca = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesTerminal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValesCredito",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", nullable: false),
                    ValorOriginal = table.Column<decimal>(type: "TEXT", nullable: false),
                    SaldoDisponivel = table.Column<decimal>(type: "TEXT", nullable: false),
                    VendaOrigemId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClienteNome = table.Column<string>(type: "TEXT", nullable: true),
                    ClienteCpf = table.Column<string>(type: "TEXT", nullable: true),
                    DataEmissao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataValidade = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataUtilizacaoTotal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    MotivoDevolucao = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValesCredito", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContasReceber_ClienteId",
                table: "ContasReceber",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_CpfCnpj",
                table: "Clientes",
                column: "CpfCnpj");

            migrationBuilder.CreateIndex(
                name: "IX_ValesCredito_Codigo",
                table: "ValesCredito",
                column: "Codigo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContasReceber_Clientes_ClienteId",
                table: "ContasReceber",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContasReceber_Clientes_ClienteId",
                table: "ContasReceber");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "ConfiguracoesTerminal");

            migrationBuilder.DropTable(
                name: "ValesCredito");

            migrationBuilder.DropIndex(
                name: "IX_ContasReceber_ClienteId",
                table: "ContasReceber");

            migrationBuilder.DropColumn(
                name: "PercentualComissao",
                table: "Vendedores");

            migrationBuilder.DropColumn(
                name: "ChaveAcessoNfce",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "DataHoraCancelamento",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "FormaPagamento",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "JustificativaCancelamento",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "NumeroNfce",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "ProtocoloAutorizacaoNfce",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "ProtocoloCancelamento",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "SerieNfce",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "XmlCancelamento",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "XmlNfce",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "DataCadastro",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "ContasReceber");
        }
    }
}
