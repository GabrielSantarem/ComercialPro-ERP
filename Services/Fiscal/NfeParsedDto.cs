using System;
using System.Collections.Generic;

namespace GetStartedApp.Services.Fiscal;

public class NfeItemParsedDto
{
    public int NumeroItem { get; set; }
    public string CodigoProduto { get; set; } = string.Empty;
    public string CodigoEan { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Ncm { get; set; } = string.Empty;
    public string Cfop { get; set; } = string.Empty;
    public string UnidadeComercial { get; set; } = "UN";
    public decimal QuantidadeComercial { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotalBruto { get; set; }
    public decimal ValorFreteRateado { get; set; }
    public decimal OutrasDespesasRateadas { get; set; }
    public decimal Desconto { get; set; }
}

public class NfeDuplicataParsedDto
{
    public string Numero { get; set; } = string.Empty;
    public DateTime? Vencimento { get; set; }
    public decimal Valor { get; set; }
}

public class NfeParsedDto
{
    public string ChaveAcesso { get; set; } = string.Empty;
    public string NumeroNota { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public DateTime? DataEmissao { get; set; }
    public string NaturezaOperacao { get; set; } = string.Empty;

    // Emitente / Fornecedor
    public string EmitenteCnpj { get; set; } = string.Empty;
    public string EmitenteRazaoSocial { get; set; } = string.Empty;
    public string EmitenteNomeFantasia { get; set; } = string.Empty;
    public string EmitenteUf { get; set; } = string.Empty;
    public string EmitenteInscricaoEstadual { get; set; } = string.Empty;

    // Totais
    public decimal ValorProdutos { get; set; }
    public decimal ValorFrete { get; set; }
    public decimal ValorSeguro { get; set; }
    public decimal OutrasDespesas { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorTotalNfe { get; set; }

    // Listas
    public List<NfeItemParsedDto> Itens { get; set; } = [];
    public List<NfeDuplicataParsedDto> Duplicatas { get; set; } = [];
}
