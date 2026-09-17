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
        var dto = new NfeParsedDto();

        // O XML da NF-e padrão SEFAZ usa o namespace http://www.portalfiscal.inf.br/nfe
        // Procuramos os elementos de forma agnóstica a namespace para tolerar variações
        var infNfe = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "infNFe");
        if (infNfe == null)
        {
            throw new InvalidOperationException("O arquivo XML fornecido não é um documento fiscal NF-e válido (tag 'infNFe' ausente).");
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
        var dups = infNfe.Descendants().Where(e => e.Name.LocalName == "dup");
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

        _logger.LogInformation("NF-e {Numero} emitida por '{Fornecedor}' parseada com sucesso. {QtdItens} itens, {QtdDups} duplicatas.",
            dto.NumeroNota, dto.EmitenteRazaoSocial, dto.Itens.Count, dto.Duplicatas.Count);

        return dto;
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
