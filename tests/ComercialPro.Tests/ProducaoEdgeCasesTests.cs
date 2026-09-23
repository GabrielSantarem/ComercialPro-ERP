using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Xunit;

namespace GetStartedApp.Tests;

public class ProducaoEdgeCasesTests : IDisposable
{
    private readonly string _dbName;
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly AppDbContext _db;
    private readonly PdvService _service;

    public ProducaoEdgeCasesTests()
    {
        _dbName = $"test_edgecases_{Guid.NewGuid():N}.db";
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(_dbOptions);
        _db.Database.EnsureCreated();

        _service = new PdvService(_db, NullLogger<PdvService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName))
        {
            try { File.Delete(_dbName); } catch { }
        }
    }

    // =========================================================================
    // 1. CONCORRÊNCIA E RACE CONDITIONS (MULTI-TERMINAL)
    // =========================================================================

    [Fact]
    public async Task Concorrencia_DuploFaturamentoDePedidoBalcao_ApenasUmDeveSucessoEOutroRejeitado()
    {
        // Arrange: Criar vendedor, produto, turno de caixa e pedido balcão
        var vendedor = new Vendedor { Nome = "Vendedor Balcão" };
        var produto = new Produto { Nome = "Item Concorrente", Preco = 50.00m, Estoque = 10 };
        _db.Vendedores.Add(vendedor);
        _db.Produtos.Add(produto);
        await _db.SaveChangesAsync();

        var turno = await _service.AbrirCaixaAsync(vendedor.Id, 100.00m);
        var pedido = await _service.CriarPedidoBalcaoAsync(vendedor.Id, "Cliente Stress", "", [(produto, 2)]);

        // Act: Criar duas instâncias de PdvService com contextos distintos (simulando 2 terminais de caixa na rede)
        using var dbTerminal1 = new AppDbContext(_dbOptions);
        var serviceTerminal1 = new PdvService(dbTerminal1, NullLogger<PdvService>.Instance);

        using var dbTerminal2 = new AppDbContext(_dbOptions);
        var serviceTerminal2 = new PdvService(dbTerminal2, NullLogger<PdvService>.Instance);

        // Disparar simultaneamente o faturamento do MESMO pedido em dois caixas
        var task1 = Task.Run(async () =>
        {
            try { await serviceTerminal1.FaturarPedidoBalcaoNoCaixaAsync(pedido.Id, "Dinheiro"); return true; }
            catch { return false; }
        });

        var task2 = Task.Run(async () =>
        {
            try { await serviceTerminal2.FaturarPedidoBalcaoNoCaixaAsync(pedido.Id, "Dinheiro"); return true; }
            catch { return false; }
        });

        var resultados = await Task.WhenAll(task1, task2);

        // Assert: Exatamente um faturamento deve ter sido bem-sucedido e o outro rejeitado
        var sucessos = resultados.Count(r => r);
        var falhas = resultados.Count(r => !r);

        Assert.Equal(1, sucessos);
        Assert.Equal(1, falhas);

        // O estoque só pode ter sido baixado 1 vez (10 - 2 = 8, NUNCA 6)
        using var dbVerificacao = new AppDbContext(_dbOptions);
        var produtoFinal = await dbVerificacao.Produtos.FindAsync(produto.Id);
        Assert.Equal(8, produtoFinal!.Estoque);

        // Apenas 1 venda fiscal deve existir no banco
        var totalVendas = await dbVerificacao.Vendas.CountAsync();
        Assert.Equal(1, totalVendas);
    }

    // =========================================================================
    // 2. CURVA ABC & DRE - MARGEM NEGATIVA E PRODUTOS COM CUSTO ZERO
    // =========================================================================

    [Fact]
    public async Task VendaComMargemNegativa_DeveCalcularDreELucroNegativoSemOverflow()
    {
        // Produto vendido abaixo do preço de custo (promoção de queima com prejuízo)
        var vendedor = new Vendedor { Nome = "Operador Queima" };
        var prodPrejuizo = new Produto { Nome = "Item Queima", Preco = 30.00m, CustoUltimaCompra = 50.00m, Estoque = 100 };
        _db.Vendedores.Add(vendedor);
        _db.Produtos.Add(prodPrejuizo);
        await _db.SaveChangesAsync();

        // Venda de 2 unidades: Receita = R$ 60,00; CMV = R$ 100,00; Lucro Bruto = -R$ 40,00 (-66,7% margem)
        await _service.SalvarPedidoAsync(vendedor.Id, [(prodPrejuizo, 2)]);

        var indicadores = await _service.ObterIndicadoresVendasAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        Assert.Equal(60.00m, indicadores.TotalFaturado);
        Assert.Equal(100.00m, indicadores.Dre.CustoMercadoriasVendidas);
        Assert.Equal(-40.00m, indicadores.Dre.LucroBruto);
        Assert.True(indicadores.Dre.MargemBrutaPercentual < 0, "A margem bruta deve ser negativa.");
        Assert.Equal(-66.67m, Math.Round(indicadores.Dre.MargemBrutaPercentual, 2));

        // Na Curva ABC o item deve constar com lucro negativo
        var itemAbc = Assert.Single(indicadores.CurvaAbc);
        Assert.Equal(-40.00m, itemAbc.LucroBrutoTotal);
        Assert.Equal(-66.67m, Math.Round(itemAbc.MargemPercentual, 2));
    }

    [Fact]
    public async Task CurvaAbc_ProdutoComCustoZero_DeveCalcularLucroTotalSemFalhar()
    {
        // Produto cadastrado sem nota fiscal de entrada inicial (Custo = 0)
        var vendedor = new Vendedor { Nome = "Operador" };
        var prodCustoZero = new Produto { Nome = "Produto Custo Zero", Preco = 80.00m, CustoUltimaCompra = 0m, Estoque = 50 };
        _db.Vendedores.Add(vendedor);
        _db.Produtos.Add(prodCustoZero);
        await _db.SaveChangesAsync();

        await _service.SalvarPedidoAsync(vendedor.Id, [(prodCustoZero, 1)]);

        var indicadores = await _service.ObterIndicadoresVendasAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        var item = Assert.Single(indicadores.CurvaAbc);
        Assert.Equal(80.00m, item.FaturamentoTotal);
        Assert.Equal(0m, item.CustoTotal);
        Assert.Equal(80.00m, item.LucroBrutoTotal);
        Assert.Equal(100m, item.MargemPercentual);
        Assert.Equal(100m, indicadores.Dre.MargemBrutaPercentual);
    }

    [Fact]
    public async Task CurvaAbc_EmpateMassivoDeProdutos_OrdenacaoParetoEstavelEAcumuladoExato()
    {
        var vendedor = new Vendedor { Nome = "Vendedor" };
        _db.Vendedores.Add(vendedor);

        // Cadastra 10 produtos idênticos a R$ 10 cada
        var produtos = new List<Produto>();
        for (int i = 1; i <= 10; i++)
        {
            produtos.Add(new Produto { Nome = $"Item Empate #{i:D2}", Preco = 10m, CustoUltimaCompra = 5m, Estoque = 100 });
        }
        _db.Produtos.AddRange(produtos);
        await _db.SaveChangesAsync();

        // Vende 1 unidade de cada produto (Total R$ 100)
        foreach (var p in produtos)
        {
            await _service.SalvarPedidoAsync(vendedor.Id, [(p, 1)]);
        }

        var indicadores = await _service.ObterIndicadoresVendasAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        Assert.Equal(10, indicadores.CurvaAbc.Count);
        Assert.Equal(100m, indicadores.TotalFaturado);

        // Cada produto é 10% do total
        Assert.All(indicadores.CurvaAbc, item => Assert.Equal(10m, item.PercentualDoTotal));

        // O último item acumulado deve atingir exatamente 100%
        Assert.Equal(100m, Math.Round(indicadores.CurvaAbc.Last().PercentualAcumulado, 2));

        // Os primeiros 8 produtos devem ser Classe A (somam 80%)
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal("A", indicadores.CurvaAbc[i].ClasseAbc);
        }
        // O 9º produto deve ser Classe B (90%)
        Assert.Equal("B", indicadores.CurvaAbc[8].ClasseAbc);
        // O 10º produto deve ser Classe C (100%)
        Assert.Equal("C", indicadores.CurvaAbc[9].ClasseAbc);
    }

    // =========================================================================
    // 3. FINANCEIRO & CREDIÁRIO - CORNER CASES DE JUROS E DESCONTOS
    // =========================================================================

    [Fact]
    public async Task ContasReceber_DescontoSuperiorAoTotalDevido_DeveLancarExcecao()
    {
        var conta = await _service.RegistrarContaReceberManualAsync(
            clienteNome: "Cliente Fiado", 
            clienteCpfCnpj: "123.456.789-00", 
            clienteTelefone: "(11) 98888-7777", 
            numeroDocumento: "DOC-001", 
            numeroParcela: "1/1", 
            valor: 100.00m, 
            dataVencimento: DateTime.Today.AddDays(15)
        );

        // Tentar dar R$ 150,00 de desconto em uma dívida de R$ 100,00
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.LiquidarContaReceberAsync(conta.Id, 10.00m, juros: 0m, desconto: 150.00m)
        );

        Assert.Contains("não pode exceder o valor total da dívida", ex.Message);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -5)]
    public async Task ContasReceber_JurosOuDescontoNegativos_DeveLancarExcecao(decimal jurosInvalido, decimal descontoInvalido)
    {
        var conta = await _service.RegistrarContaReceberManualAsync(
            clienteNome: "Cliente Fiado", 
            clienteCpfCnpj: "123.456.789-00", 
            clienteTelefone: "(11) 98888-7777", 
            numeroDocumento: "DOC-002", 
            numeroParcela: "1/1", 
            valor: 100.00m, 
            dataVencimento: DateTime.Today.AddDays(15)
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.LiquidarContaReceberAsync(conta.Id, 100.00m, juros: jurosInvalido, desconto: descontoInvalido)
        );
    }

    [Fact]
    public async Task ContasReceber_LiquidacaoComJurosEDescontoSimultaneos_CalculaValorFinalExato()
    {
        // Dívida R$ 200,00. Atraso gera juros de R$ 35,50 e gerente dá R$ 15,50 de desconto
        // Valor Final esperado: 200 + 35.50 - 15.50 = R$ 220,00
        var conta = await _service.RegistrarContaReceberManualAsync(
            clienteNome: "Cliente Atrasado", 
            clienteCpfCnpj: "123.456.789-00", 
            clienteTelefone: "(11) 98888-7777", 
            numeroDocumento: "DOC-003", 
            numeroParcela: "1/1", 
            valor: 200.00m, 
            dataVencimento: DateTime.Today.AddDays(-10)
        );

        await _service.LiquidarContaReceberAsync(conta.Id, valorRecebido: 220.00m, juros: 35.50m, desconto: 15.50m, formaRecebimento: "PIX");

        var contaDb = await _db.ContasReceber.FindAsync(conta.Id);
        Assert.Equal("RECEBIDO", contaDb!.Status);
        Assert.Equal(200.00m, contaDb.ValorOriginal);
        Assert.Equal(35.50m, contaDb.JurosMulta);
        Assert.Equal(15.50m, contaDb.Desconto);
        Assert.Equal(220.00m, contaDb.ValorFinal);
        Assert.Equal(220.00m, contaDb.ValorRecebido);
        Assert.Equal("PIX", contaDb.FormaRecebimento);
    }

    [Fact]
    public async Task ContasPagar_DuplicataComVencimentoNoPassado_CalculaDiasAtrasoCorretamente()
    {
        // Criar duplicata vencida há 5 dias
        var dataVencimento = DateTime.Today.AddDays(-5);
        var conta = await _service.RegistrarContaPagarManualAsync("Fornecedor Antigo", "", "DOC-ATRASO", "1/1", 500m, dataVencimento);

        Assert.True(conta.IsVencido);
        Assert.True(conta.DiasAtraso >= 5);
    }

    // =========================================================================
    // 4. CAIXA & TURNOS - LIMITES DE SANGRIA E QUEBRA
    // =========================================================================

    [Fact]
    public async Task CaixaTurno_SangriaExatamenteIgualAoSaldo_ZeraGavetaSemErro()
    {
        var vendedor = new Vendedor { Nome = "Caixa 1" };
        _db.Vendedores.Add(vendedor);
        await _db.SaveChangesAsync();

        var turno = await _service.AbrirCaixaAsync(vendedor.Id, 150.00m);

        // Sangria exata de todo o dinheiro da gaveta (R$ 150,00)
        var mov = await _service.RegistrarSangriaAsync(turno.Id, 150.00m, "Retirada total para cofre");

        Assert.Equal(150.00m, mov.Valor);
        var turnoDb = await _db.CaixasTurno.FindAsync(turno.Id);
        Assert.Equal(0m, turnoDb!.SaldoEsperadoEmDinheiro);
    }

    [Fact]
    public async Task CaixaTurno_SangriaUmCentavoAcimaDoSaldo_DeveFalharComMensagemClara()
    {
        var vendedor = new Vendedor { Nome = "Caixa 1" };
        _db.Vendedores.Add(vendedor);
        await _db.SaveChangesAsync();

        var turno = await _service.AbrirCaixaAsync(vendedor.Id, 100.00m);

        // Tentar sangria de R$ 100,01
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RegistrarSangriaAsync(turno.Id, 100.01m, "Tentativa excedente")
        );

        Assert.Contains("maior do que o saldo físico disponível", ex.Message);
    }

    [Fact]
    public async Task CaixaTurno_FechamentoComFaltaEComSobra_CalculaDiferencaQuebraExata()
    {
        var vendedor = new Vendedor { Nome = "Caixa 1" };
        _db.Vendedores.Add(vendedor);
        await _db.SaveChangesAsync();

        // Cenário 1: Falta de Caixa (Esperado 100, Informado 80 -> Quebra = -20)
        var t1 = await _service.AbrirCaixaAsync(vendedor.Id, 100.00m);
        await _service.FecharCaixaAsync(t1.Id, 80.00m, "Faltou 20 reais");
        var t1Db = await _db.CaixasTurno.FindAsync(t1.Id);
        Assert.Equal(-20.00m, t1Db!.DiferencaQuebra);

        // Cenário 2: Sobra de Caixa (Esperado 50, Informado 55 -> Quebra = +5)
        var t2 = await _service.AbrirCaixaAsync(vendedor.Id, 50.00m);
        await _service.FecharCaixaAsync(t2.Id, 55.00m, "Sobrou 5 reais");
        var t2Db = await _db.CaixasTurno.FindAsync(t2.Id);
        Assert.Equal(5.00m, t2Db!.DiferencaQuebra);
    }

    // =========================================================================
    // 5. DRE & PERÍODOS - PREJUÍZO LÍQUIDO E VALIDAÇÃO DE DATAS
    // =========================================================================

    [Fact]
    public async Task DRE_DespesasSuperioresAoLucroBruto_GeraPrejuizoLiquidoCorretamente()
    {
        var vendedor = new Vendedor { Nome = "Vendedor" };
        var prod = new Produto { Nome = "Item Normal", Preco = 100m, CustoUltimaCompra = 60m, Estoque = 10 };
        _db.Vendedores.Add(vendedor);
        _db.Produtos.Add(prod);
        await _db.SaveChangesAsync();

        // Venda 1: R$ 100 receita, CMV R$ 60 -> Lucro Bruto = R$ 40
        await _service.SalvarPedidoAsync(vendedor.Id, [(prod, 1)]);

        // Despesa Fixa Paga no período: R$ 200 de aluguel
        var conta = await _service.RegistrarContaPagarManualAsync("Imobiliária", "", "ALUGUEL-01", "1/1", 200.00m, DateTime.Today);
        await _service.LiquidarContaPagarAsync(conta.Id, 200.00m, "TED");

        var indicadores = await _service.ObterIndicadoresVendasAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        var dre = indicadores.Dre;
        Assert.Equal(100.00m, dre.ReceitaBruta);
        Assert.Equal(60.00m, dre.CustoMercadoriasVendidas);
        Assert.Equal(40.00m, dre.LucroBruto);
        Assert.Equal(200.00m, dre.DespesasOperacionaisPagas);

        // Lucro Líquido = 40 - 200 = -160 (Prejuízo Líquido)
        Assert.Equal(-160.00m, dre.LucroLiquido);
        Assert.Equal(-160.00m, dre.MargemLiquidaPercentual); // -160 / 100 = -160%
    }

    [Fact]
    public async Task PeriodoDatasInvertidas_DeveLancarArgumentException()
    {
        var dataInicio = DateTime.Today.AddDays(10);
        var dataFim = DateTime.Today;

        // Início posterior ao Fim
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ObterIndicadoresVendasAsync(dataInicio, dataFim)
        );
    }
}
