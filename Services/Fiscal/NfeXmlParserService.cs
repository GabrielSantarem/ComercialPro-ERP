using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.Services.Fiscal;

public class NfeXmlParserService
{
    private readonly ILogger<NfeXmlParserService> _logger;

    public NfeXmlParserService(ILogger<NfeXmlParserService> logger)
    {
        _logger = logger;
    }

    public NfeParsedDto ParseFromStream(Stream stream)
    {
        var doc = XDocument.Load(stream);
        return ParseXmlDocument(doc);
    }

    public NfeParsedDto ParseFromString(string xmlContent)
    {
        var doc = XDocument.Parse(xmlContent);
        return ParseXmlDocument(doc);
    }

    private NfeParsedDto ParseXmlDocument(XDocument doc)
    {
        // 0. Detecção inteligente de outros padrões fiscais brasileiros não-mercantis
        ValidarTipoDocumentoXml(doc);

        var dto = new NfeParsedDto();

        // O XML da NF-e padrão SEFAZ usa o namespace http://www.portalfiscal.inf.br/nfe
        // Procuramos os elementos de forma agnóstica a namespace para tolerar variações
        var infNfe = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "infNFe");
        if (infNfe == null)
        {
            throw new InvalidOperationException("O arquivo XML fornecido não é um documento fiscal NF-e ou NFC-e válido (tag 'infNFe' ausente).");
        }

        // 1. Chave de Acesso (atributo Id do infNFe)
        var idAttr = infNfe.Attribute("Id")?.Value ?? string.Empty;
        dto.ChaveAcesso = idAttr.StartsWith("NFe", StringComparison.OrdinalIgnoreCase) 
            ? idAttr[3..] 
            : idAttr;

        // 2. Identificação (ide)
        var ide = infNfe.Elements().FirstOrDefault(e => e.Name.LocalName == "ide");
        if (ide != null)
        {
            dto.NumeroNota = GetElementValue(ide, "nNF");
            dto.Serie = GetElementValue(ide, "serie");
            dto.NaturezaOperacao = GetElementValue(ide, "natOp");

            var modelo = GetElementValue(ide, "mod");
            if (!string.IsNullOrWhiteSpace(modelo))
            {
                dto.ModeloDocumento = modelo;
                dto.TipoDocumentoDescricao = modelo switch
                {
                    "65" => "NFC-e (Consumidor Eletrônica)",
                    "55" => "NF-e (Mercantil Eletrônica)",
                    _ => $"NF mod. {modelo}"
                };
            }

            var dhEmi = GetElementValue(ide, "dhEmi");
            if (string.IsNullOrWhiteSpace(dhEmi)) dhEmi = GetElementValue(ide, "dEmi");

            if (DateTime.TryParse(dhEmi, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt) ||
                DateTime.TryParse(dhEmi, out dt))
            {
                dto.DataEmissao = dt;
            }
        }

        // 3. Emitente / Fornecedor (emit)
        var emit = infNfe.Elements().FirstOrDefault(e => e.Name.LocalName == "emit");
        if (emit != null)
        {
            dto.EmitenteCnpj = GetElementValue(emit, "CNPJ");
            if (string.IsNullOrWhiteSpace(dto.EmitenteCnpj)) dto.EmitenteCnpj = GetElementValue(emit, "CPF");

            dto.EmitenteRazaoSocial = GetElementValue(emit, "xNome");
            dto.EmitenteNomeFantasia = GetElementValue(emit, "xFant");
            dto.EmitenteInscricaoEstadual = GetElementValue(emit, "IE");

            var enderEmit = emit.Elements().FirstOrDefault(e => e.Name.LocalName == "enderEmit");
            if (enderEmit != null)
            {
                dto.EmitenteUf = GetElementValue(enderEmit, "UF");
            }
        }

        // 4. Totais da Nota (total/ICMSTot)
        var icmsTot = infNfe.Descendants().FirstOrDefault(e => e.Name.LocalName == "ICMSTot");
        if (icmsTot != null)
        {
            dto.ValorProdutos = ParseDecimal(GetElementValue(icmsTot, "vProd"));
            dto.ValorFrete = ParseDecimal(GetElementValue(icmsTot, "vFrete"));
            dto.ValorSeguro = ParseDecimal(GetElementValue(icmsTot, "vSeg"));
            dto.OutrasDespesas = ParseDecimal(GetElementValue(icmsTot, "vOutro"));
            dto.ValorDesconto = ParseDecimal(GetElementValue(icmsTot, "vDesc"));
            dto.ValorTotalNfe = ParseDecimal(GetElementValue(icmsTot, "vNF"));
        }

        // 5. Itens da Nota (det)
        var dets = infNfe.Elements().Where(e => e.Name.LocalName == "det");
        int seq = 1;
        foreach (var det in dets)
        {
            var prod = det.Elements().FirstOrDefault(e => e.Name.LocalName == "prod");
            if (prod == null) continue;

            var item = new NfeItemParsedDto
            {
                NumeroItem = int.TryParse(det.Attribute("nItem")?.Value, out int nItem) ? nItem : seq++,
                CodigoProduto = GetElementValue(prod, "cProd"),
                CodigoEan = NormalizeEan(GetElementValue(prod, "cEAN")),
                Descricao = GetElementValue(prod, "xProd"),
                Ncm = GetElementValue(prod, "NCM"),
                Cfop = GetElementValue(prod, "CFOP"),
                UnidadeComercial = GetElementValue(prod, "uCom"),
                QuantidadeComercial = ParseDecimal(GetElementValue(prod, "qCom")),
                ValorUnitario = ParseDecimal(GetElementValue(prod, "vUnCom")),
                ValorTotalBruto = ParseDecimal(GetElementValue(prod, "vProd")),
                ValorFreteRateado = ParseDecimal(GetElementValue(prod, "vFrete")),
                OutrasDespesasRateadas = ParseDecimal(GetElementValue(prod, "vOutro")),
                Desconto = ParseDecimal(GetElementValue(prod, "vDesc"))
            };

            dto.Itens.Add(item);
        }

        // 6. Cobrança e Duplicatas (cobr/dup)
        var dups = infNfe.Descendants().Where(e => e.Name.LocalName == "dup").ToList();
        foreach (var dup in dups)
        {
            var parcela = new NfeDuplicataParsedDto
            {
                Numero = GetElementValue(dup, "nDup"),
                Valor = ParseDecimal(GetElementValue(dup, "vDup"))
            };

            var dVenc = GetElementValue(dup, "dVenc");
            if (DateTime.TryParse(dVenc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtVenc) ||
                DateTime.TryParse(dVenc, out dtVenc))
            {
                parcela.Vencimento = dtVenc;
            }

            dto.Duplicatas.Add(parcela);
        }

        // 6.1 Fallback para pagamento à vista ou NFC-e via <pag><detPag>
        if (dto.Duplicatas.Count == 0)
        {
            var detsPag = infNfe.Descendants().Where(e => e.Name.LocalName == "detPag").ToList();
            int seqPag = 1;
            foreach (var detPag in detsPag)
            {
                var valorPag = ParseDecimal(GetElementValue(detPag, "vPag"));
                if (valorPag > 0)
                {
                    dto.Duplicatas.Add(new NfeDuplicataParsedDto
                    {
                        Numero = $"AVISTA-{seqPag++:D2}",
                        Vencimento = dto.DataEmissao ?? DateTime.Today,
                        Valor = valorPag
                    });
                }
            }
        }

        _logger.LogInformation("{Tipo} {Numero} emitida por '{Fornecedor}' parseada com sucesso. {QtdItens} itens, {QtdDups} duplicatas.",
            dto.TipoDocumentoDescricao, dto.NumeroNota, dto.EmitenteRazaoSocial, dto.Itens.Count, dto.Duplicatas.Count);

        return dto;
    }

    private static void ValidarTipoDocumentoXml(XDocument doc)
    {
        // 1. Bilhete de Passagem Eletrônico (BP-e Mod. 63)
        if (doc.Descendants().Any(e => e.Name.LocalName is "infBPe" or "bpeProc" or "BPe"))
        {
            throw new InvalidOperationException(
                "O arquivo selecionado é um BP-e (Bilhete de Passagem Eletrônico - Modelo 63). " +
                "Este módulo aceita apenas notas de mercadorias para estoque (NF-e modelo 55 e NFC-e modelo 65).");
        }

        // 2. Conhecimento de Transporte Eletrônico (CT-e / CT-e OS Mod. 67 / 57)
        if (doc.Descendants().Any(e => e.Name.LocalName is "infCte" or "cteOSProc" or "CTeOS" or "cteProc" or "CTe"))
        {
            throw new InvalidOperationException(
                "O arquivo selecionado é um CT-e / CT-e OS (Conhecimento de Transporte Eletrônico). " +
                "Documentos de frete/transporte não contêm itens para entrada de estoque físico.");
        }

        // 3. NFS-e Municipal Padrão ABRASF (Ex: Belo Horizonte, SP, etc.)
        if (doc.Descendants().Any(e => e.Name.LocalName is "CompNfse" or "InfNfse" or "ConsultarNfseResposta"))
        {
            throw new InvalidOperationException(
                "O arquivo selecionado é uma NFS-e Municipal de Prestação de Serviços (Padrão ABRASF/Prefeituras). " +
                "Para entrada de produtos em estoque, utilize uma NF-e de Mercadorias (Modelo 55).");
        }

        // 4. NFS-e Padrão Nacional (Receita Federal / MEI)
        if (doc.Descendants().Any(e => e.Name.LocalName is "infNFSe" or "NFSe" or "DPS"))
        {
            throw new InvalidOperationException(
                "O arquivo selecionado é uma NFS-e Nacional de Serviços (Padrão SPED / Receita Federal). " +
                "Para entrada e formação de estoque de produtos, utilize a NF-e Mercantil (Modelo 55).");
        }
    }

    private static string GetElementValue(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim() ?? string.Empty;
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        value = value.Trim();

        // Se contiver vírgula e não contiver ponto (formato brasileiro legado "25,00"), substitui por ponto
        if (value.Contains(',') && !value.Contains('.'))
        {
            value = value.Replace(',', '.');
        }

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec) ? dec : 0m;
    }

    private static string NormalizeEan(string ean)
    {
        if (string.IsNullOrWhiteSpace(ean)) return string.Empty;
        var trimmed = ean.Trim();
        if (trimmed.Equals("SEM GTIN", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }
        return trimmed;
    }
}
