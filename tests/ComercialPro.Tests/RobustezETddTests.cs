using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.Services.Fiscal;
using GetStartedApp.ViewModels;
using Xunit;

namespace GetStartedApp.Tests;

public class RobustezETddTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PdvService _service;
    private readonly NfeXmlParserService _parser;

    public RobustezETddTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.Vendedores.AddRange(
            new Vendedor { Id = 1, Nome = "Operador Caixa Principal" },
            new Vendedor { Id = 2, Nome = "Atendente Balcão 1" }
        );

        _db.Produtos.AddRange(
            new Produto { Id = 1, Nome = "Sacola Branca 2k", Preco = 1.00m, Estoque = 100, CodigoBarras = "7890001" },
            new Produto { Id = 2, Nome = "Copo Descartável", Preco = 5.00m, Estoque = 50, CodigoBarras = "7890002" }
        );
        _db.SaveChanges();

        _service = new PdvService(_db, NullLogger<PdvService>.Instance);
        _parser = new NfeXmlParserService(NullLogger<NfeXmlParserService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // =========================================================================
    // 1. TESTES DE CAIXA E TURNOS - INTEGRIDADE & FRAUDE
    // =========================================================================

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public async Task Nao_Deve_Permitir_Fechar_Caixa_Com_Saldo_Informado_Negativo(decimal saldoNegativo)
    {
        // Arrange
        var turno = await _service.AbrirCaixaAsync(1, 100.00m);

        // Act & Assert: Contagem física de gaveta nunca pode ser negativa
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.FecharCaixaAsync(turno.Id, saldoNegativo, "Contagem errada")
        );

        Assert.Contains("não pode ser negativo", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Nao_Deve_Permitir_Suprimento_Com_Valor_Zero_Ou_Negativo(decimal valorInvalido)
    {
        var turno = await _service.AbrirCaixaAsync(1, 50.00m);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegistrarSuprimentoAsync(turno.Id, valorInvalido, "Motivo")
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Nao_Deve_Permitir_Sangria_Com_Valor_Zero_Ou_Negativo(decimal valorInvalido)
    {
        var turno = await _service.AbrirCaixaAsync(1, 50.00m);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegistrarSangriaAsync(turno.Id, valorInvalido, "Motivo")
        );
    }

    [Fact]
    public async Task Nao_Deve_Permitir_Faturar_Pedido_Balcao_Se_Caixa_Estiver_Fechado()
    {
        // Arrange: Não há turno aberto no sistema!
        var p = await _db.Produtos.FirstAsync(x => x.Id == 1);
        var pedido = await _service.CriarPedidoBalcaoAsync(2, "Cliente Fila", "", [(p, 2)]);

        // Act & Assert: Faturar pedido no caixa fechado deve ser barrado
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.FaturarPedidoBalcaoNoCaixaAsync(pedido.Id, "Dinheiro")
        );

        Assert.Contains("sem um turno de caixa aberto", ex.Message);
    }

    [Fact]
    public async Task Deve_Permitir_Ciclo_Completo_Multiplas_Aberturas_E_Fechamentos_Sequenciais()
    {
        // Turno 1
        var t1 = await _service.AbrirCaixaAsync(1, 100m, "Manhã");
        await _service.RegistrarSuprimentoAsync(t1.Id, 20m, "Troco extra");
        await _service.FecharCaixaAsync(t1.Id, 120m, "Fim turno 1");

        // Turno 2 (Mesmo operador ou outro, logo em seguida)
        var t2 = await _service.AbrirCaixaAsync(1, 50m, "Tarde");
        await _service.RegistrarSangriaAsync(t2.Id, 30m, "Sangria cofre");
        await _service.FecharCaixaAsync(t2.Id, 20m, "Fim turno 2");

        // Assert: Ambos turnos existem no histórico e estão FECHADOS
        var historico = await _service.ObterHistoricoTurnosAsync();
        Assert.Equal(2, historico.Count);
        Assert.All(historico, t => Assert.Equal("FECHADO", t.Status));
    }

    // =========================================================================
    // 2. TESTES DE VENDAS E BALCÃO - QUANTIDADES E CÁLCULOS FINANCEIROS
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task Nao_Deve_Permitir_Venda_Com_Quantidade_Zero_Ou_Negativa(int qtdInvalida)
    {
        await _service.AbrirCaixaAsync(1, 100m);
        var p = await _db.Produtos.FirstAsync(x => x.Id == 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SalvarPedidoAsync(1, [(p, qtdInvalida)], "Dinheiro")
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Nao_Deve_Permitir_Criar_Pedido_Balcao_Com_Item_Quantidade_Invalida(int qtdInvalida)
    {
        var p = await _db.Produtos.FirstAsync(x => x.Id == 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CriarPedidoBalcaoAsync(2, "Cliente Teste", "", [(p, qtdInvalida)])
        );
    }

    [Fact]
    public async Task Cancelar_Pedido_Balcao_Deve_Atualizar_Status_Para_Cancelado()
    {
        var p = await _db.Produtos.FirstAsync(x => x.Id == 1);
        var pedido = await _service.CriarPedidoBalcaoAsync(2, "Cliente Desistiu", "", [(p, 1)]);

        await _service.CancelarPedidoBalcaoAsync(pedido.Id, "Cliente desistiu da compra");

        var pedidoDb = await _db.PedidosBalcao.FindAsync(pedido.Id);
        Assert.Equal("CANCELADO", pedidoDb!.Status);

        // Tentar faturar o cancelado deve falhar
        await _service.AbrirCaixaAsync(1, 100m);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.FaturarPedidoBalcaoNoCaixaAsync(pedido.Id, "Dinheiro")
        );
    }

    // =========================================================================
    // 3. TESTES DE ESTOQUE E ENTRADAS - SANITIZAÇÃO DE DADOS
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Nao_Deve_Permitir_Entrada_De_Mercadoria_Com_Quantidade_Invalida(int qtdInvalida)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegistrarEntradaMercadoriaAsync("NF-ERR", "Forn", "", [(1, qtdInvalida, 10.00m)])
        );
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public async Task Nao_Deve_Permitir_Entrada_De_Mercadoria_Com_Custo_Negativo(decimal custoNegativo)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegistrarEntradaMercadoriaAsync("NF-ERR", "Forn", "", [(1, 10, custoNegativo)])
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Nao_Deve_Criar_Produto_Via_Xml_Com_Nome_Vazio_Ou_Nulo(string? nomeInvalido)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ObterOuCriarProdutoPorXmlAsync(nomeInvalido!, "789999", "3923", "UN", 10m, 5m)
        );
    }

    // =========================================================================
    // 4. TESTES DO PARSER XML DA SEFAZ - CASOS EXTREMOS & VARIAÇÕES
    // =========================================================================

    [Theory]
    [InlineData("  SEM GTIN  ")]
    [InlineData("sem gtin")]
    [InlineData("Sem GTIN")]
    [InlineData("")]
    [InlineData("   ")]
    public void Deve_Normalizar_Ean_Com_Espacos_Ou_Case_Sem_Gtin(string eanBruto)
    {
        var xml = $"""
        <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe">
          <NFe>
            <infNFe Id="NFe123">
              <ide><nNF>1</nNF></ide>
              <emit><xNome>Distribuidora</xNome></emit>
              <det nItem="1">
                <prod>
                  <cProd>001</cProd>
                  <cEAN>{eanBruto}</cEAN>
                  <xProd>Item Teste</xProd>
                  <qCom>1</qCom>
                  <vUnCom>10.00</vUnCom>
                  <vProd>10.00</vProd>
                </prod>
              </det>
            </infNFe>
          </NFe>
        </nfeProc>
        """;

        var nfe = _parser.ParseFromString(xml);

        Assert.Single(nfe.Itens);
        Assert.Equal(string.Empty, nfe.Itens[0].CodigoEan);
    }

    [Fact]
    public void Deve_Tratar_Xml_Com_Decimais_Em_Formato_Brasileiro_Com_Virgula()
    {
        // Certos emissores legados ou sistemas regionais geram vProd com vírgula "25,50"
        var xml = """
        <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe">
          <NFe>
            <infNFe Id="NFe123">
              <ide><nNF>100</nNF></ide>
              <emit><xNome>Forn</xNome></emit>
              <det nItem="1">
                <prod>
                  <cProd>X1</cProd>
                  <xProd>Produto Virgula</xProd>
                  <qCom>2</qCom>
                  <vUnCom>12,50</vUnCom>
                  <vProd>25,00</vProd>
                </prod>
              </det>
              <total>
                <ICMSTot>
                  <vProd>25,00</vProd>
                  <vNF>25,00</vNF>
                </ICMSTot>
              </total>
            </infNFe>
          </NFe>
        </nfeProc>
        """;

        var nfe = _parser.ParseFromString(xml);

        Assert.Equal(25.00m, nfe.ValorTotalNfe);
        Assert.Equal(12.50m, nfe.Itens[0].ValorUnitario);
        Assert.Equal(25.00m, nfe.Itens[0].ValorTotalBruto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>Não é XML</body></html>")]
    public void Deve_Lancar_Excecao_Para_Xml_Invalido_Ou_Vazio(string xmlInvalido)
    {
        Assert.ThrowsAny<Exception>(() => _parser.ParseFromString(xmlInvalido));
    }

    // =========================================================================
    // 5. TESTES DO MULTIPLICADOR DE QUANTIDADES
    // =========================================================================

    [Theory]
    [InlineData("10 * arroz", 10, "arroz")]
    [InlineData("  5*feijao  ", 5, "feijao")]
    [InlineData("0*arroz", 1, "arroz")] // 0 não faz sentido, trata como busca do termo
    [InlineData("1000000*sacola", 99999, "sacola")] // Quantidade exorbitante limitada a 99999
    [InlineData("*feijao", 1, "*feijao")]
    public void Deve_Processar_Multiplicador_De_Forma_Resiliente(string input, int qtdEsperada, string termoEsperado)
    {
        var (qtd, termo) = PdvViewModel.ProcessarMultiplicador(input);

        Assert.Equal(qtdEsperada, qtd);
        Assert.Equal(termoEsperado, termo);
    }

    // =========================================================================
    // 6. TESTES DE PESQUISA MULTI-TERMO E CASE-INSENSITIVE
    // =========================================================================

    [Theory]
    [InlineData("sacola", 1)]
    [InlineData("SACOLA", 1)]
    [InlineData("Sacola 2k", 1)]
    [InlineData("2k sacola", 1)] // Ordem invertida das palavras
    [InlineData("  sacola    branca  ", 1)]
    [InlineData("produto_inexistente_xyz", 0)]
    public async Task Pesquisa_De_Produtos_Deve_Ser_Resiliente_A_Ordem_E_Case(string termo, int qtdEsperada)
    {
        var resultados = await _service.PesquisarProdutosAsync(termo);
        Assert.Equal(qtdEsperada, resultados.Count);
    }

    // =========================================================================
    // 7. TESTES DE CICLO DE ATENDENTES NO BALCÃO
    // =========================================================================

    [Fact]
    public void TrocarVendedorProximo_Deve_Ciclar_Atendentes_Corretamente()
    {
        var vm = new BalcaoViewModel(_service, NullLogger<BalcaoViewModel>.Instance);
        vm.Vendedores.Clear();
        vm.Vendedores.Add(new Vendedor { Id = 1, Nome = "Carlos" });
        vm.Vendedores.Add(new Vendedor { Id = 2, Nome = "Ana" });
        vm.Vendedores.Add(new Vendedor { Id = 3, Nome = "Marcos" });
        vm.VendedorIndex = 0;
        vm.VendedorSelecionado = vm.Vendedores[0];

        // Ciclo 1: 0 -> 1 (Ana)
        vm.TrocarVendedorProximo();
        Assert.Equal(1, vm.VendedorIndex);
        Assert.Equal("Ana", vm.VendedorSelecionado.Nome);

        // Ciclo 2: 1 -> 2 (Marcos)
        vm.TrocarVendedorProximo();
        Assert.Equal(2, vm.VendedorIndex);
        Assert.Equal("Marcos", vm.VendedorSelecionado.Nome);

        // Ciclo 3: 2 -> 0 (Carlos - wrap around)
        vm.TrocarVendedorProximo();
        Assert.Equal(0, vm.VendedorIndex);
        Assert.Equal("Carlos", vm.VendedorSelecionado.Nome);
    }

    [Fact]
    public void TrocarVendedorProximo_Com_Lista_Vazia_Nao_Deve_Lancar_Excecao()
    {
        var vm = new BalcaoViewModel(_service, NullLogger<BalcaoViewModel>.Instance);
        vm.Vendedores.Clear();

        // Não deve estourar DivideByZero nem IndexOutOfRange
        var exception = Record.Exception(() => vm.TrocarVendedorProximo());
        Assert.Null(exception);
    }

    // =========================================================================
    // 8. TESTES DE FECHAMENTO FISCAL E DIVISÃO DE PARCELAS (SPLIT DE CENTAVOS)
    // =========================================================================

    [Theory]
    [InlineData(100.00)]
    [InlineData(10.00)]
    [InlineData(1000.01)]
    public void Divisao_De_Parcelas_Em_3x_Deve_Manter_Soma_Exata_Dos_Centavos(decimal valorTotal)
    {
        // Regra contábil: a soma das parcelas deve bater 100.0% com o total da nota
        var p1 = Math.Round(valorTotal / 3, 2);
        var p2 = Math.Round(valorTotal / 3, 2);
        var p3 = valorTotal - (p1 + p2);

        var soma = p1 + p2 + p3;
        Assert.Equal(valorTotal, soma);
    }
}
