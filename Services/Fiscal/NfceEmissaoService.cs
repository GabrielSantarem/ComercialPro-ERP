using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GetStartedApp.Models.Fiscal;
using NFe.Classes;
using NFe.Classes.Informacoes;
using NFe.Classes.Informacoes.Identificacao;
using NFe.Classes.Informacoes.Identificacao.Tipos;
using NFe.Classes.Informacoes.Emitente;
using NFe.Classes.Informacoes.Destinatario;
using NFe.Classes.Informacoes.Detalhe;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal.Tipos;
using NFe.Classes.Informacoes.Total;
using NFe.Classes.Informacoes.Pagamento;
using NFe.Utils.Assinatura;
using NFe.Utils.NFe;
using DFe.Classes.Flags;
using DFe.Classes.Entidades;

namespace GetStartedApp.Services.Fiscal;

public class NfceEmissaoService
{
    private readonly DanfeNfceTermicaService _danfeService;
    private readonly ILogger<NfceEmissaoService> _logger;

    public NfceEmissaoService(DanfeNfceTermicaService danfeService, ILogger<NfceEmissaoService> logger)
    {
        _danfeService = danfeService;
        _logger = logger;
    }

    public RetornoEmissaoNfce EmitirNfce(DadosEmissaoNfce dados, ConfiguracaoFiscalEmpresa empresa)
    {
        // 1. Validações prévias
        if (dados.Itens == null || dados.Itens.Count == 0)
        {
            return new RetornoEmissaoNfce { Sucesso = false, Mensagem = "A NFC-e deve conter ao menos 1 produto vendido." };
        }

        foreach (var it in dados.Itens)
        {
            var ncmLimpo = (it.Ncm ?? string.Empty).Trim().Replace(".", "");
            if (ncmLimpo.Length != 8)
            {
                return new RetornoEmissaoNfce
                {
                    Sucesso = false,
                    Mensagem = $"NCM inválido no item '{it.DescricaoProduto}': '{it.Ncm}'. O NCM deve possuir exatamente 8 dígitos."
                };
            }
        }

        var totalProdutos = dados.Itens.Sum(i => i.ValorTotal);
        var totalPagamentos = dados.Pagamentos.Sum(p => p.Valor);

        if (totalPagamentos < totalProdutos && (totalProdutos - totalPagamentos) > 0.01m)
        {
            return new RetornoEmissaoNfce
            {
                Sucesso = false,
                Mensagem = $"Total pago (R$ {totalPagamentos:N2}) é menor que o total da venda (R$ {totalProdutos:N2})."
            };
        }

        long numeroNota = empresa.UltimoNumeroNfce + 1;
        DateTime dataEmissao = DateTime.Now;

        // 2. Gerar Chave de Acesso (44 dígitos)
        // cUF(2) + AAMM(4) + CNPJ(14) + mod(2) + serie(3) + nNF(9) + tpEmis(1) + cNF(8) + cDV(1)
        var codigoAleatorio = (new Random().Next(10000000, 99999999)).ToString();
        var estado = Enum.Parse<Estado>(empresa.Uf.ToUpperInvariant());
        int cUF = (int)estado;
        string aamm = dataEmissao.ToString("yyMM");
        string cnpjLimpo = empresa.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Trim().PadLeft(14, '0');
        int modelo = 65; // NFC-e
        int serie = empresa.SerieNfce;
        int tpEmis = dados.ModoContingenciaOffline ? 9 : 1; // 1 - Normal, 9 - Contingência Offline

        string chaveSemDV = $"{cUF:D2}{aamm}{cnpjLimpo}{modelo:D2}{serie:D3}{numeroNota:D9}{tpEmis}{codigoAleatorio}";
        int cDV = CalcularDigitoVerificadorModulo11(chaveSemDV);
        string chaveAcesso = $"{chaveSemDV}{cDV}";
        string idNFe = $"NFe{chaveAcesso}";

        // 3. Montar Árvore de Objetos do Zeus NFe
        var nfe = new NFe.Classes.NFe
        {
            infNFe = new infNFe
            {
                versao = "4.00",
                Id = idNFe,
                ide = new ide
                {
                    cUF = estado,
                    cNF = codigoAleatorio,
                    natOp = "VENDA AO CONSUMIDOR",
                    mod = ModeloDocumento.NFCe,
                    serie = serie,
                    nNF = numeroNota,
                    dhEmi = dataEmissao,
                    tpNF = TipoNFe.tnSaida,
                    idDest = DestinoOperacao.doInterna,
                    cMunFG = empresa.CodigoMunicipioIbge,
                    tpImp = TipoImpressao.tiNFCe,
                    tpEmis = dados.ModoContingenciaOffline ? TipoEmissao.teOffLine : TipoEmissao.teNormal,
                    cDV = cDV,
                    tpAmb = empresa.Ambiente == 1 ? TipoAmbiente.Producao : TipoAmbiente.Homologacao,
                    finNFe = FinalidadeNFe.fnNormal,
                    indFinal = ConsumidorFinal.cfConsumidorFinal,
                    indPres = PresencaComprador.pcPresencial,
                    procEmi = ProcessoEmissao.peAplicativoContribuinte,
                    verProc = "1.0"
                },
                emit = new emit
                {
                    CNPJ = cnpjLimpo,
                    xNome = empresa.RazaoSocial,
                    xFant = empresa.NomeFantasia,
                    enderEmit = new enderEmit
                    {
                        xLgr = empresa.Logradouro,
                        nro = empresa.Numero,
                        xBairro = empresa.Bairro,
                        cMun = empresa.CodigoMunicipioIbge,
                        xMun = empresa.Municipio,
                        UF = estado,
                        CEP = empresa.Cep.Replace("-", "").Trim()
                    },
                    IE = empresa.InscricaoEstadual.Replace(".", "").Replace("-", "").Trim(),
                    CRT = empresa.Crt == "1" ? CRT.SimplesNacional : CRT.RegimeNormal
                },
                det = [],
                total = new total
                {
                    ICMSTot = new ICMSTot
                    {
                        vProd = totalProdutos,
                        vNF = totalProdutos,
                        vTotTrib = dados.Itens.Sum(i => i.ValorTotal * (i.AliquotaTributosAproximadosPercentual / 100m))
                    }
                },
                pag = []
            }
        };

        // Tratamento de contingência
        if (dados.ModoContingenciaOffline)
        {
            nfe.infNFe.ide.dhCont = dataEmissao;
            nfe.infNFe.ide.xJust = string.IsNullOrWhiteSpace(dados.JustificativaContingencia)
                ? "Falha de comunicacao com o servidor da SEFAZ autorizadora"
                : dados.JustificativaContingencia;
        }

        // Destinatário (Consumidor avulso ou com CPF)
        if (!string.IsNullOrWhiteSpace(dados.CpfConsumidor))
        {
            var cpfLimpo = dados.CpfConsumidor.Replace(".", "").Replace("-", "").Trim();
            nfe.infNFe.dest = new dest(VersaoServico.Versao400)
            {
                CPF = cpfLimpo,
                xNome = string.IsNullOrWhiteSpace(dados.NomeConsumidor) ? "CONSUMIDOR" : dados.NomeConsumidor,
                indIEDest = indIEDest.NaoContribuinte
            };
        }

        // Itens
        int numItem = 1;
        foreach (var it in dados.Itens)
        {
            var ncmLimpo = it.Ncm.Replace(".", "").Trim();
            var det = new det
            {
                nItem = numItem++,
                prod = new prod
                {
                    cProd = string.IsNullOrWhiteSpace(it.CodigoProduto) ? $"{numItem:D3}" : it.CodigoProduto,
                    cEAN = string.IsNullOrWhiteSpace(it.CodigoBarrasEan) ? "SEM GTIN" : it.CodigoBarrasEan,
                    xProd = it.DescricaoProduto,
                    NCM = ncmLimpo,
                    CFOP = it.Cfop > 0 ? it.Cfop : 5102,
                    uCom = string.IsNullOrWhiteSpace(it.UnidadeComercial) ? "UN" : it.UnidadeComercial,
                    qCom = it.Quantidade,
                    vUnCom = it.ValorUnitario,
                    vProd = it.ValorTotal,
                    cEANTrib = string.IsNullOrWhiteSpace(it.CodigoBarrasEan) ? "SEM GTIN" : it.CodigoBarrasEan,
                    uTrib = string.IsNullOrWhiteSpace(it.UnidadeComercial) ? "UN" : it.UnidadeComercial,
                    qTrib = it.Quantidade,
                    vUnTrib = it.ValorUnitario,
                    indTot = IndicadorTotal.ValorDoItemCompoeTotalNF
                },
                imposto = new imposto
                {
                    vTotTrib = Math.Round(it.ValorTotal * (it.AliquotaTributosAproximadosPercentual / 100m), 2),
                    ICMS = new ICMS
                    {
                        TipoICMS = it.Csosn == "500"
                            ? new ICMSSN500 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn500 }
                            : new ICMSSN102 { orig = OrigemMercadoria.OmNacional, CSOSN = Csosnicms.Csosn102 }
                    },
                    PIS = new PIS
                    {
                        TipoPIS = new PISNT { CST = CSTPIS.pis07 }
                    },
                    COFINS = new COFINS
                    {
                        TipoCOFINS = new COFINSNT { CST = CSTCOFINS.cofins07 }
                    }
                }
            };
            nfe.infNFe.det.Add(det);
        }

        // Pagamentos
        var pagList = new List<detPag>();
        foreach (var p in dados.Pagamentos)
        {
            var forma = p.MeioPagamento switch
            {
                "01" => FormaPagamento.fpDinheiro,
                "03" => FormaPagamento.fpCartaoCredito,
                "04" => FormaPagamento.fpCartaoDebito,
                "17" => FormaPagamento.fpPagamentoInstantaneoPIXDinamico,
                "20" => FormaPagamento.fpPagamentoInstantaneoPIXEstatico,
                _ => FormaPagamento.fpOutro
            };

            pagList.Add(new detPag
            {
                tPag = forma,
                vPag = p.Valor
            });
        }

        nfe.infNFe.pag =
        [
            new pag
            {
                detPag = pagList,
                vTroco = dados.ValorTroco > 0 ? dados.ValorTroco : null
            }
        ];

        // 4. Montar URL Oficial do QR-Code Versão 2.0
        string urlQrCode = GerarUrlQrCodeVersao2(chaveAcesso, empresa);

        // 5. Assinatura Digital do XML
        try
        {
            var cert = CertificadoA1Helper.ObterOuGerarCertificado(empresa);

            nfe.Signature = Assinador.ObterAssinatura(
                nfe.infNFe,
                nfe.infNFe.Id,
                cert,
                manterDadosEmCache: false,
                signatureMethod: SignedXml.XmlDsigRSASHA256Url,
                digestMethod: SignedXml.XmlDsigSHA256Url,
                cfgServicoRemoverAcentos: true);

            var xmlString = nfe.ObterXmlString();

            // Atualiza número da empresa após emissão bem-sucedida
            empresa.UltimoNumeroNfce = numeroNota;

            // 6. Gerar Impressão DANFE Térmica
            var danfeTexto = _danfeService.GerarDanfeTermica(empresa, dados, numeroNota, chaveAcesso, urlQrCode, dataEmissao);

            return new RetornoEmissaoNfce
            {
                Sucesso = true,
                Mensagem = dados.ModoContingenciaOffline 
                    ? "NFC-e gerada e assinada com sucesso em CONTINGÊNCIA OFFLINE!"
                    : "NFC-e gerada e assinada com sucesso!",
                ChaveAcesso = chaveAcesso,
                NumeroNota = numeroNota,
                Serie = serie,
                XmlAssinado = xmlString,
                UrlQrCode = urlQrCode,
                DanfeTextoTermica = danfeTexto,
                EmitidaEmContingencia = dados.ModoContingenciaOffline
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao assinar digitalmente a NFC-e {Chave}", chaveAcesso);
            return new RetornoEmissaoNfce
            {
                Sucesso = false,
                Mensagem = $"Falha na assinatura digital da NFC-e: {ex.Message}"
            };
        }
    }

    public static int CalcularDigitoVerificadorModulo11(string chaveSemDv)
    {
        int[] multiplicadores = [2, 3, 4, 5, 6, 7, 8, 9];
        int soma = 0;
        int multIdx = 0;

        for (int i = chaveSemDv.Length - 1; i >= 0; i--)
        {
            int digito = chaveSemDv[i] - '0';
            soma += digito * multiplicadores[multIdx];
            multIdx = (multIdx + 1) % multiplicadores.Length;
        }

        int resto = soma % 11;
        int dv = 11 - resto;
        return (dv == 0 || dv >= 10) ? 0 : dv;
    }

    public static string GerarUrlQrCodeVersao2(string chaveAcesso, ConfiguracaoFiscalEmpresa empresa)
    {
        // Padrão SEFAZ QR-Code versão 2.0:
        // p=chave|2|tpAmb|cIdToken|cHashQRCode
        int tpAmb = empresa.Ambiente;
        string idTokenFormatado = int.Parse(empresa.IdCsc).ToString(); // sem zeros a esquerda no hash da sefaz
        string dadosParaHash = $"{chaveAcesso}|2|{tpAmb}|{idTokenFormatado}{empresa.CodigoCsc}";

        using var sha1 = SHA1.Create();
        var hashBytes = sha1.ComputeHash(Encoding.ASCII.GetBytes(dadosParaHash));
        var cHashQRCode = Convert.ToHexString(hashBytes).ToUpperInvariant();

        string baseUrl = empresa.Ambiente == 1
            ? $"https://www.nfce.fazenda.{empresa.Uf.ToLowerInvariant()}.gov.br/qrcode"
            : $"https://www.homologacao.nfce.fazenda.{empresa.Uf.ToLowerInvariant()}.gov.br/qrcode";

        return $"{baseUrl}?p={chaveAcesso}|2|{tpAmb}|{idTokenFormatado}|{cHashQRCode}";
    }
}
