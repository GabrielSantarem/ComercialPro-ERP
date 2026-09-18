using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GetStartedApp.Models.Fiscal;
using GetStartedApp.Services.Fiscal;
using Xunit;

namespace GetStartedApp.Tests;

public class DanfeA4PdfTests
{
    private readonly DanfeA4PdfService _danfePdfService = new();

    private ConfiguracaoFiscalEmpresa CriarEmpresaPadrao()
    {
        return new ConfiguracaoFiscalEmpresa
        {
            Cnpj = "12345678000195",
            RazaoSocial = "Supermercado Modelo Ltda",
            NomeFantasia = "Super Modelo",
            InscricaoEstadual = "123456789",
            Crt = "1",
            Logradouro = "Av. Brasil",
            Numero = "1500",
            Bairro = "Centro",
            Municipio = "São Paulo",
            CodigoMunicipioIbge = 3550308,
            Uf = "SP",
            Cep = "01000-000",
            SerieNfce = 1,
            UltimoNumeroNfce = 100
        };
    }

    private (DadosEmissaoNfce Dados, RetornoEmissaoNfce Retorno) CriarVendaPadrao()
    {
        var dados = new DadosEmissaoNfce
        {
            CpfConsumidor = "123.456.789-00",
            NomeConsumidor = "João da Silva",
            Itens = new List<ItemEmissaoNfceDto>
            {
                new()
                {
                    CodigoProduto = "PROD-01",
                    DescricaoProduto = "Arroz Branco Tipo 1 5kg",
                    Ncm = "10063021",
                    Cfop = 5102,
                    Csosn = "102",
                    UnidadeComercial = "UN",
                    Quantidade = 2,
                    ValorUnitario = 25.50m
                },
                new()
                {
                    CodigoProduto = "PROD-02",
                    DescricaoProduto = "Feijão Carioca 1kg",
                    Ncm = "07133399",
                    Cfop = 5102,
                    Csosn = "102",
                    UnidadeComercial = "UN",
                    Quantidade = 3,
                    ValorUnitario = 8.00m
                }
            },
            Pagamentos = new List<PagamentoEmissaoNfceDto>
            {
                new() { MeioPagamento = "01", Valor = 100.00m }
            }
        };

        var retorno = new RetornoEmissaoNfce
        {
            Sucesso = true,
            NumeroNota = 101,
            Serie = 1,
            ChaveAcesso = "35260912345678000195650010000001011876543210",
            EmitidaEmContingencia = false,
            XmlAssinado = "<xml></xml>"
        };

        return (dados, retorno);
    }

    [Fact]
    public void GerarDanfeA4Pdf_DeveGerarBytesValidosComCabecalhoPdf()
    {
        var empresa = CriarEmpresaPadrao();
        var (dados, retorno) = CriarVendaPadrao();

        var pdfBytes = _danfePdfService.GerarDanfeA4Pdf(dados, retorno, empresa);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000, "O PDF gerado deve conter bytes substanciais.");

        // Assinatura mágica de arquivo PDF: %PDF-
        var cabecalho = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        Assert.Equal("%PDF", cabecalho);
    }

    [Fact]
    public void SalvarPdf_DeveSalvarArquivoNoDiscoComNomePadronizado()
    {
        var empresa = CriarEmpresaPadrao();
        var (dados, retorno) = CriarVendaPadrao();

        var pdfBytes = _danfePdfService.GerarDanfeA4Pdf(dados, retorno, empresa);
        var tempDir = Path.Combine(Path.GetTempPath(), "test_danfe_" + Guid.NewGuid().ToString("N"));

        try
        {
            var caminho = _danfePdfService.SalvarPdf(pdfBytes, retorno.ChaveAcesso, tempDir);

            Assert.True(File.Exists(caminho), "O arquivo PDF deve existir fisicamente no disco.");
            Assert.Contains(retorno.ChaveAcesso, Path.GetFileName(caminho));
            Assert.True(new FileInfo(caminho).Length > 1000);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void FormatarChave44_DeveDividirEm11BlocosDeQuatroDigitos()
    {
        var chave = "35260912345678000195650010000001011876543210";
        var formatada = FormatadorDanfe.FormatarChave44(chave);

        var partes = formatada.Split(' ');
        Assert.Equal(11, partes.Length);
        Assert.All(partes, p => Assert.Equal(4, p.Length));
        Assert.Equal("3526 0912 3456 7800 0195 6500 1000 0001 0118 7654 3210", formatada);
    }

    [Fact]
    public void GerarDanfeA4Pdf_EmModoContingencia_DeveGerarPdfSemFalhas()
    {
        var empresa = CriarEmpresaPadrao();
        var (dados, retorno) = CriarVendaPadrao();
        retorno.EmitidaEmContingencia = true; // Contingência Offline

        var pdfBytes = _danfePdfService.GerarDanfeA4Pdf(dados, retorno, empresa);

        Assert.NotNull(pdfBytes);
        var cabecalho = Encoding.ASCII.GetString(pdfBytes, 0, 4);
        Assert.Equal("%PDF", cabecalho);
    }

    [Fact]
    public void GerarDanfeA4Pdf_ComConsumidorAnonimo_DevePreencherConsumidorFinal()
    {
        var empresa = CriarEmpresaPadrao();
        var (dados, retorno) = CriarVendaPadrao();
        dados.CpfConsumidor = null;
        dados.NomeConsumidor = null;

        var pdfBytes = _danfePdfService.GerarDanfeA4Pdf(dados, retorno, empresa);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
    }

    [Fact]
    public void AbrirVisualizacaoEImpressao_ComArquivoInexistente_DeveRetornarFalso()
    {
        var resultado = _danfePdfService.AbrirVisualizacaoEImpressao("/caminho/falso/inexistente.pdf");
        Assert.False(resultado);
    }
}
