using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using GetStartedApp.Models.Fiscal;
using GetStartedApp.Services.Fiscal;
using Xunit;

namespace GetStartedApp.Tests;

public class NfceZeusEngineTests
{
    private readonly DanfeNfceTermicaService _danfeService;
    private readonly NfceEmissaoService _nfceService;
    private readonly ConfiguracaoFiscalEmpresa _empresaPadrao;

    public NfceZeusEngineTests()
    {
        _danfeService = new DanfeNfceTermicaService();
        _nfceService = new NfceEmissaoService(_danfeService, NullLogger<NfceEmissaoService>.Instance);

        _empresaPadrao = new ConfiguracaoFiscalEmpresa
        {
            Cnpj = "12.345.678/0001-90",
            RazaoSocial = "SUPERMERCADO TESTE FISCAL LTDA",
            NomeFantasia = "SUPERMERCADO TESTE",
            InscricaoEstadual = "123456789111",
            Crt = "1", // Simples Nacional
            Logradouro = "AVENIDA BRASIL",
            Numero = "500",
            Bairro = "CENTRO",
            CodigoMunicipioIbge = 3550308,
            Municipio = "SAO PAULO",
            Uf = "SP",
            Cep = "01001-000",
            SerieNfce = 1,
            UltimoNumeroNfce = 100,
            Ambiente = 2, // Homologação
            IdCsc = "000001",
            CodigoCsc = "TESTECSC12345678",
            UsarCertificadoTesteAutoassinado = true
        };
    }

    [Fact]
    public void CalcularDigitoVerificadorModulo11_ComChaveConhecida_DeveRetornarDigitoExato()
    {
        string chave43 = "3526091234567800019065001000000101112345678";
        int dv = NfceEmissaoService.CalcularDigitoVerificadorModulo11(chave43);

        Assert.InRange(dv, 0, 9);
        string chave44 = chave43 + dv;
        Assert.Equal(44, chave44.Length);
    }

    [Fact]
    public void GerarUrlQrCodeVersao2_DeveGerarEstruturaCorreta_E_HashSha1Valido()
    {
        string chave44 = "35260912345678000190650010000001011123456788";
        var url = NfceEmissaoService.GerarUrlQrCodeVersao2(chave44, _empresaPadrao);

        Assert.StartsWith("https://www.homologacao.nfce.fazenda.sp.gov.br/qrcode?p=", url);
        Assert.Contains(chave44, url);
        Assert.Contains("|2|2|1|", url); // Versao 2, tpAmb 2, IdToken 1
    }

    [Fact]
    public void EmitirNfce_ComSucesso_DeveGerarXmlAssinadoValido_E_Chave44Digitos()
    {
        var dados = new DadosEmissaoNfce
        {
            VendaId = 1,
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    ItemNumero = 1,
                    CodigoProduto = "001",
                    CodigoBarrasEan = "7891000100103",
                    DescricaoProduto = "REFRIGERANTE COLA 2L",
                    Ncm = "22021000",
                    Cfop = 5102,
                    UnidadeComercial = "UN",
                    Quantidade = 2,
                    ValorUnitario = 9.50m,
                    Csosn = "102"
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 19.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Equal(44, retorno.ChaveAcesso.Length);
        Assert.Equal(101, retorno.NumeroNota);
        Assert.False(retorno.EmitidaEmContingencia);
        Assert.Contains("<infNFe versao=\"4.00\"", retorno.XmlAssinado);
        Assert.Contains("REFRIGERANTE COLA 2L", retorno.XmlAssinado);
        Assert.Contains("<vNF>19.00</vNF>", retorno.XmlAssinado);
    }

    [Fact]
    public void EmitirNfce_DeveConterTagsAssinatura_Signature_SignedInfo_DigestValue()
    {
        var dados = new DadosEmissaoNfce
        {
            VendaId = 2,
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "ARROZ TIPO 1 5KG",
                    Ncm = "10063021",
                    Quantidade = 1,
                    ValorUnitario = 28.90m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "17", Valor = 28.90m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\">", retorno.XmlAssinado);
        Assert.Contains("<SignedInfo>", retorno.XmlAssinado);
        Assert.Contains("<DigestValue>", retorno.XmlAssinado);
        Assert.Contains("<SignatureValue>", retorno.XmlAssinado);
        Assert.Contains("<X509Certificate>", retorno.XmlAssinado);
    }

    [Fact]
    public void EmitirNfce_ComCSOSN102_SimplesNacional_DeveConfigurarImpostosCorretamente()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "BISCOITO RECHEADO 140G",
                    Ncm = "19053100",
                    Quantidade = 3,
                    ValorUnitario = 3.50m,
                    Csosn = "102"
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 10.50m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<ICMSSN102>", retorno.XmlAssinado);
        Assert.Contains("<CSOSN>102</CSOSN>", retorno.XmlAssinado);
    }

    [Fact]
    public void EmitirNfce_ComCSOSN500_SubstituicaoTributaria_DeveConfigurarTagCorreta()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "CERVEJA PILSEN LATA 350ML",
                    Ncm = "22030000",
                    Quantidade = 6,
                    ValorUnitario = 4.00m,
                    Csosn = "500"
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "04", Valor = 24.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<ICMSSN500>", retorno.XmlAssinado);
        Assert.Contains("<CSOSN>500</CSOSN>", retorno.XmlAssinado);
    }

    [Fact]
    public void EmitirNfce_ComConsumidorIdentificadoPorCPF_DevePreencherDestinatario()
    {
        var dados = new DadosEmissaoNfce
        {
            CpfConsumidor = "123.456.789-09",
            NomeConsumidor = "CARLOS ALBERTO SILVA",
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "LEITE INTEGRAL 1L",
                    Ncm = "04012010",
                    Quantidade = 4,
                    ValorUnitario = 5.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "17", Valor = 20.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<dest>", retorno.XmlAssinado);
        Assert.Contains("<CPF>12345678909</CPF>", retorno.XmlAssinado);
        Assert.Contains("<xNome>CARLOS ALBERTO SILVA</xNome>", retorno.XmlAssinado);
        Assert.Contains("CONSUMIDOR: CPF 123.456.789-09", retorno.DanfeTextoTermica);
    }

    [Fact]
    public void EmitirNfce_ParaConsumidorAvulsoSemCPF_NaoDeveGerarTagDest()
    {
        var dados = new DadosEmissaoNfce
        {
            CpfConsumidor = null,
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "PAO FRANCES KG",
                    Ncm = "19059090",
                    Quantidade = 1,
                    ValorUnitario = 15.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 15.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.DoesNotContain("<dest>", retorno.XmlAssinado);
        Assert.Contains("CONSUMIDOR NÃO IDENTIFICADO", retorno.DanfeTextoTermica);
    }

    [Fact]
    public void EmitirNfce_ComMultiplosPagamentos_DinheiroEPix_ComTroco()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "CARNE BOVINA KG",
                    Ncm = "02013000",
                    Quantidade = 2,
                    ValorUnitario = 40.00m // Total = 80.00
                }
            ],
            Pagamentos =
            [
                new PagamentoEmissaoNfceDto { MeioPagamento = "17", Valor = 50.00m }, // PIX 50
                new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 50.00m }  // Dinheiro 50 (Total pago = 100)
            ],
            ValorTroco = 20.00m
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<tPag>17</tPag>", retorno.XmlAssinado);
        Assert.Contains("<tPag>01</tPag>", retorno.XmlAssinado);
        Assert.Contains("<vTroco>20.00</vTroco>", retorno.XmlAssinado);
        Assert.Contains("Troco", retorno.DanfeTextoTermica);
        Assert.Contains("20,00", retorno.DanfeTextoTermica);
    }

    [Fact]
    public void EmitirNfce_EmModoContingenciaOffline_DeveGerar_tpEmis_9_E_TagsJustificativa()
    {
        var dados = new DadosEmissaoNfce
        {
            ModoContingenciaOffline = true,
            JustificativaContingencia = "Queda de conexao com a internet da loja",
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "SABONETE 90G",
                    Ncm = "34011190",
                    Quantidade = 5,
                    ValorUnitario = 2.50m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 12.50m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.True(retorno.EmitidaEmContingencia);
        Assert.Contains("<tpEmis>9</tpEmis>", retorno.XmlAssinado);
        Assert.Contains("<xJust>Queda de conexao com a internet da loja</xJust>", retorno.XmlAssinado);
        Assert.Contains("EMITIDA EM CONTINGÊNCIA OFFLINE", retorno.DanfeTextoTermica);
    }

    [Fact]
    public void EmitirNfce_DanfeTermica_DeveConterChaveFormatadaEm11Blocos_E_TributosLei12741()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "DETERGENTE 500ML",
                    Ncm = "34022000",
                    Quantidade = 2,
                    ValorUnitario = 3.00m,
                    AliquotaTributosAproximadosPercentual = 18.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 6.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("DANFE NFC-e", retorno.DanfeTextoTermica);
        Assert.Contains("Não permite aproveitamento de crédito de ICMS", retorno.DanfeTextoTermica);
        Assert.Contains("Lei 12.741", retorno.DanfeTextoTermica);
        Assert.Contains("CHAVE DE ACESSO:", retorno.DanfeTextoTermica);

        var partes = retorno.DanfeTextoTermica.Split('\n');
        var linhaChave = partes.First(p => p.Trim().StartsWith(retorno.ChaveAcesso.Substring(0, 4)));
        Assert.Equal(11, linhaChave.Trim().Split(' ').Length);
    }

    [Fact]
    public void EmitirNfce_SemItens_DeveRetornarErroDeValidacao()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens = [],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 10.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.False(retorno.Sucesso);
        Assert.Contains("deve conter ao menos 1 produto", retorno.Mensagem);
    }

    [Fact]
    public void EmitirNfce_ComNcmInvalido_DiferenteDe8Digitos_DeveRejeitar()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "ITEM COM NCM CURTO",
                    Ncm = "1234", // Apenas 4 dígitos
                    Quantidade = 1,
                    ValorUnitario = 10.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 10.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.False(retorno.Sucesso);
        Assert.Contains("NCM inválido", retorno.Mensagem);
    }

    [Fact]
    public void EmitirNfce_ComPagamentoMenorQueTotal_DeveRejeitar()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "ITEM VALOR 50",
                    Ncm = "22021000",
                    Quantidade = 1,
                    ValorUnitario = 50.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 30.00m }] // Faltam 20
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.False(retorno.Sucesso);
        Assert.Contains("é menor que o total da venda", retorno.Mensagem);
    }

    [Fact]
    public void CertificadoA1Helper_GerarCertificadoTesteEmMemoria_DeveConterChavePrivadaExportavel()
    {
        using var cert = CertificadoA1Helper.GerarCertificadoTesteEmMemoria("EMPRESA TESTE LTDA", "12345678000190");

        Assert.NotNull(cert);
        Assert.True(cert.HasPrivateKey);
        Assert.Contains("12345678000190", cert.Subject);
        Assert.True(cert.NotAfter > DateTime.Now);
    }

    [Fact]
    public void EmitirNfce_DeveIncrementarUltimoNumeroDaNotaAposSucesso()
    {
        long numeroOriginal = _empresaPadrao.UltimoNumeroNfce;

        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "PRODUTO TESTE",
                    Ncm = "22021000",
                    Quantidade = 1,
                    ValorUnitario = 10.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 10.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Equal(numeroOriginal + 1, _empresaPadrao.UltimoNumeroNfce);
        Assert.Equal(numeroOriginal + 1, retorno.NumeroNota);
    }

    [Fact]
    public void EmitirNfce_ComCRT3_RegimeNormal_DeveGerarCrt3()
    {
        var empresaNormal = new ConfiguracaoFiscalEmpresa
        {
            Crt = "3", // Regime Normal
            RazaoSocial = "EMPRESA LUCRO REAL S/A",
            Cnpj = "99.888.777/0001-66",
            InscricaoEstadual = "987654321000",
            Uf = "SP"
        };

        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "PRODUTO TRIBUTADO NORMAL",
                    Ncm = "22021000",
                    Quantidade = 1,
                    ValorUnitario = 12.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 12.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, empresaNormal);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<CRT>3</CRT>", retorno.XmlAssinado);
    }

    [Fact]
    public void EmitirNfce_AmbienteProducao_DeveGerar_tpAmb_1_E_UrlQrCodeProducao()
    {
        var empresaProducao = new ConfiguracaoFiscalEmpresa
        {
            Ambiente = 1, // Produção Real
            Uf = "SP",
            Cnpj = "12.345.678/0001-90",
            IdCsc = "000001",
            CodigoCsc = "CSCPRODUCAO1234"
        };

        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "AGUA MINERAL SEM GAS 500ML",
                    Ncm = "22011000",
                    Quantidade = 2,
                    ValorUnitario = 2.50m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 5.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, empresaProducao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<tpAmb>1</tpAmb>", retorno.XmlAssinado);
        Assert.StartsWith("https://www.nfce.fazenda.sp.gov.br/qrcode?p=", retorno.UrlQrCode);
        Assert.Contains("|2|1|1|", retorno.UrlQrCode); // tpAmb 1 no QR code
        Assert.DoesNotContain("EMITIDA EM AMBIENTE DE HOMOLOGAÇÃO", retorno.DanfeTextoTermica);
    }

    [Fact]
    public void EmitirNfce_MultiplosItens_CalculoTributosAcumulado()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "PRODUTO A",
                    Ncm = "22021000",
                    Quantidade = 10,
                    ValorUnitario = 10.00m, // 100.00 * 10% = 10.00 trib
                    AliquotaTributosAproximadosPercentual = 10.00m
                },
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "PRODUTO B",
                    Ncm = "22021000",
                    Quantidade = 5,
                    ValorUnitario = 20.00m, // 100.00 * 20% = 20.00 trib
                    AliquotaTributosAproximadosPercentual = 20.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 200.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        // Total tributos acumulado = 10.00 + 20.00 = 30.00
        Assert.Contains("<vTotTrib>30.00</vTotTrib>", retorno.XmlAssinado);
        Assert.Contains("Trib Aprox R$: 30,00", retorno.DanfeTextoTermica);
    }

    [Fact]
    public void EmitirNfce_ContingenciaOffline_SemJustificativaInformada_DeveAdotarJustificativaPadrao()
    {
        var dados = new DadosEmissaoNfce
        {
            ModoContingenciaOffline = true,
            JustificativaContingencia = null, // Deixando nulo para testar fallback
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "CAFE TORRADO E MOIDO 500G",
                    Ncm = "09012100",
                    Quantidade = 1,
                    ValorUnitario = 18.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 18.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<tpEmis>9</tpEmis>", retorno.XmlAssinado);
        Assert.Contains("<xJust>Falha de comunicacao com o servidor da SEFAZ autorizadora</xJust>", retorno.XmlAssinado);
    }

    [Fact]
    public void DanfeNfceTermicaService_ComItensLongos_DeveTruncarDescricaoComReticencias()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "PRODUTO COM UMA DESCRICAO EXTREMAMENTE LONGA QUE NAO CABE EM 80MM INTEIRA",
                    Ncm = "22021000",
                    Quantidade = 1,
                    ValorUnitario = 5.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 5.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("PRODUTO COM UMA DESCRICAO...", retorno.DanfeTextoTermica);
    }

    [Fact]
    public void EmitirNfce_ComProdutoSemCodigoBarrasEan_DevePreencherSEM_GTIN()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    CodigoBarrasEan = "", // Sem EAN (ex: produto a granel hortifruti)
                    DescricaoProduto = "BANANA PRATA KG",
                    Ncm = "08039000",
                    Quantidade = 2,
                    ValorUnitario = 4.50m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 9.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains("<cEAN>SEM GTIN</cEAN>", retorno.XmlAssinado);
        Assert.Contains("<cEANTrib>SEM GTIN</cEANTrib>", retorno.XmlAssinado);
    }

    [Fact]
    public void EmitirNfce_ComChaveGerada_DeveTerIdentificadorNFePrefixado()
    {
        var dados = new DadosEmissaoNfce
        {
            Itens =
            [
                new ItemEmissaoNfceDto
                {
                    DescricaoProduto = "AGUA SANITARIA 2L",
                    Ncm = "28289011",
                    Quantidade = 1,
                    ValorUnitario = 6.00m
                }
            ],
            Pagamentos = [new PagamentoEmissaoNfceDto { MeioPagamento = "01", Valor = 6.00m }]
        };

        var retorno = _nfceService.EmitirNfce(dados, _empresaPadrao);

        Assert.True(retorno.Sucesso);
        Assert.Contains($"Id=\"NFe{retorno.ChaveAcesso}\"", retorno.XmlAssinado);
    }
}
