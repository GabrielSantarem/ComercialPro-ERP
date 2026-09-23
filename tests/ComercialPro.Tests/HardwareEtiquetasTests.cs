using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Services.Etiquetas;
using GetStartedApp.Services.Hardware;
using GetStartedApp.Services.Impressao;
using Xunit;

namespace GetStartedApp.Tests;

public class HardwareEtiquetasTests
{
    [Fact]
    public void DecodificarCodigo_ModoValorTotal_DeveCalcularPesoCorretoParaProduto()
    {
        var parser = new BalancaEtiquetaParserService();
        // Prefixo 2, código 1234, valor R$ 15,00 (01500 centavos), preço unitário R$ 30,00/kg
        // Código 13 dígitos: 2123400150008 (prefixo 2, prod 1234, filler 0, payload 01500, checksum)
        var resultado = parser.DecodificarCodigo(
            codigoBarras: "2123400150008",
            precoUnitarioCadastro: 30.00m,
            modo: ModoCodigoBalanca.ValorTotal,
            tamanhoCodigoProduto: 4);

        Assert.True(resultado.IsCodigoBalanca);
        Assert.Equal("1234", resultado.CodigoProduto);
        Assert.Equal(15.00m, resultado.ValorTotalCalculado);
        Assert.Equal(0.500m, resultado.QuantidadeOuPeso); // 15.00 / 30.00 = 0.500 kg
        Assert.Empty(resultado.MensagemErro);
    }

    [Fact]
    public void DecodificarCodigo_ModoPesoLiquido_DeveExtrairQuantidadeEPrecoTotalCorretos()
    {
        var parser = new BalancaEtiquetaParserService();
        // Prefixo 2, código 1234, peso 1.500 kg (01500 gramas), preço unitário R$ 20,00/kg
        var resultado = parser.DecodificarCodigo(
            codigoBarras: "2123400150008",
            precoUnitarioCadastro: 20.00m,
            modo: ModoCodigoBalanca.PesoLiquido,
            tamanhoCodigoProduto: 4);

        Assert.True(resultado.IsCodigoBalanca);
        Assert.Equal("1234", resultado.CodigoProduto);
        Assert.Equal(1.500m, resultado.QuantidadeOuPeso);
        Assert.Equal(30.00m, resultado.ValorTotalCalculado); // 1.500 * 20.00 = 30.00
        Assert.Empty(resultado.MensagemErro);
    }

    [Fact]
    public void DecodificarCodigo_ComPrecoCadastroZerado_DeveRetornarErro()
    {
        var parser = new BalancaEtiquetaParserService();
        var resultado = parser.DecodificarCodigo(
            codigoBarras: "2123400150008",
            precoUnitarioCadastro: 0.00m,
            modo: ModoCodigoBalanca.ValorTotal,
            tamanhoCodigoProduto: 4);

        Assert.NotEmpty(resultado.MensagemErro);
        Assert.Contains("zero", resultado.MensagemErro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecodificarCodigo_CodigoSemPrefixo2_DeveIdentificarComoNaoBalanca()
    {
        var parser = new BalancaEtiquetaParserService();
        var resultado = parser.DecodificarCodigo(
            codigoBarras: "7891000100103",
            precoUnitarioCadastro: 10.00m);

        Assert.False(resultado.IsCodigoBalanca);
        Assert.NotEmpty(resultado.MensagemErro);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("2123")]
    [InlineData("212345678901234567")]
    public void DecodificarCodigo_TamanhoInvalido_NaoDeveDispararExcecao(string? codigo)
    {
        var parser = new BalancaEtiquetaParserService();
        var exception = Record.Exception(() => parser.DecodificarCodigo(codigo!, 10.00m));

        Assert.Null(exception);
    }

    [Fact]
    public async Task BalancaEmulada_DeveRetornarPesoSimuladoEstavel()
    {
        var mockService = new BalancaMockService { PesoConfigurado = 1.450m };
        var peso = await mockService.LerPesoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1.450m, peso);
        Assert.True(mockService.IsConectada);
    }

    [Fact]
    public void GerarFolhaEtiquetasPdf_ComListaProdutos_DeveRetornarBytesValidos()
    {
        var service = new EtiquetaGondolaService();
        var modelo = service.ObterModelosSuportados().First(m => m.Nome.Contains("6180"));
        var produtos = new List<ItemEtiquetaGondolaDto>
        {
            new(1, "Queijo Muçarela Fatiado", "7891000100103", "PRD-001", 39.90m, "KG", 39.90m),
            new(2, "Presunto Cozido Sadia", "7891000200202", "PRD-002", 28.50m, "KG", 28.50m)
        };

        var pdfBytes = service.GerarFolhaEtiquetasPdf(produtos, modelo);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
        // Assinatura de PDF (%PDF)
        Assert.Equal((byte)'%', pdfBytes[0]);
        Assert.Equal((byte)'P', pdfBytes[1]);
        Assert.Equal((byte)'D', pdfBytes[2]);
        Assert.Equal((byte)'F', pdfBytes[3]);
    }

    [Fact]
    public void GerarFolhaEtiquetasPdf_MaisProdutosQueCapacidadeFolha_DeveCriarMultiplasPaginas()
    {
        var service = new EtiquetaGondolaService();
        var modelo = service.ObterModelosSuportados().First(m => m.Nome.Contains("6180")); // 3x10 = 30 etiquetas
        var produtos = Enumerable.Range(1, 45)
            .Select(i => new ItemEtiquetaGondolaDto(i, $"Produto Teste {i:D3}", $"789100000{i:D4}", $"COD-{i}", 9.99m * i, "UN"))
            .ToList();

        var pdfBytes = service.GerarFolhaEtiquetasPdf(produtos, modelo);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
    }

    [Fact]
    public void GerarFolhaEtiquetasPdf_ProdutoSemEan_DeveImprimirSemQuebra()
    {
        var service = new EtiquetaGondolaService();
        var modelo = service.ObterModelosSuportados().First();
        var produtos = new List<ItemEtiquetaGondolaDto>
        {
            new(10, "Pão Francês Granel", "", "PAO-01", 14.90m, "KG"),
            new(11, "Bolo de Fubá Caseiro", null!, "BOL-02", 18.00m, "UN")
        };

        var pdfBytes = service.GerarFolhaEtiquetasPdf(produtos, modelo);

        Assert.NotNull(pdfBytes);
        Assert.NotEmpty(pdfBytes);
    }

    [Fact]
    public void ObterModelosSuportados_DeveConterModelosPimacoPadrao()
    {
        var service = new EtiquetaGondolaService();
        var modelos = service.ObterModelosSuportados();

        Assert.Contains(modelos, m => m.Nome.Contains("6180"));
        Assert.Contains(modelos, m => m.Nome.Contains("6182"));
    }

    [Fact]
    public void ObterComandoAberturaGaveta_DeveConterBytesPadraoEscPos()
    {
        var service = new GavetaDinheiroService();
        var comando = service.ObterComandoAberturaGaveta();

        Assert.NotNull(comando);
        Assert.True(comando.Length >= 3);
        Assert.Equal(0x1B, comando[0]); // ESC
        Assert.Equal(0x70, comando[1]); // p
        Assert.Equal(0x00, comando[2]); // Pino 2
    }

    [Fact]
    public void CupomTermico_ComAberturaGavetaHabilitada_DeveAnexarPulsoNaFinalizacaoEmDinheiro()
    {
        var cupomService = new CupomTermicoService();
        var gavetaService = new GavetaDinheiroService();

        var textoCupom = "TESTE DE CUPOM EM DINHEIRO";
        var bytesComprovante = cupomService.GerarBytesComprovante(
            textoCupom: textoCupom,
            acionarGaveta: true,
            formaPagamento: "Dinheiro",
            gavetaService: gavetaService);

        Assert.NotNull(bytesComprovante);
        // Verifica a presença da sequência ESC p (0x1B, 0x70) no buffer
        bool contemPulsoGaveta = false;
        for (int i = 0; i < bytesComprovante.Length - 1; i++)
        {
            if (bytesComprovante[i] == 0x1B && bytesComprovante[i + 1] == 0x70)
            {
                contemPulsoGaveta = true;
                break;
            }
        }
        Assert.True(contemPulsoGaveta);

        // Se forma de pagamento for Cartão, não deve acionar gaveta
        var bytesCartao = cupomService.GerarBytesComprovante(
            textoCupom: textoCupom,
            acionarGaveta: true,
            formaPagamento: "Cartão de Crédito",
            gavetaService: gavetaService);

        bool contemPulsoCartao = false;
        for (int i = 0; i < bytesCartao.Length - 1; i++)
        {
            if (bytesCartao[i] == 0x1B && bytesCartao[i + 1] == 0x70)
            {
                contemPulsoCartao = true;
                break;
            }
        }
        Assert.False(contemPulsoCartao);
    }

    [Fact]
    public async Task BalancaMockService_DeveFornecerPesoEstavelSemDependerDePortaSerialFisica()
    {
        var service = new BalancaMockService { PesoConfigurado = 2.345m };
        var peso1 = await service.LerPesoAsync(TestContext.Current.CancellationToken);
        var peso2 = await service.LerPesoAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2.345m, peso1);
        Assert.Equal(2.345m, peso2);
        Assert.True(service.IsConectada);
    }
}
