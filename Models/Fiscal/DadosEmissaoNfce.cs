using System;
using System.Collections.Generic;

namespace GetStartedApp.Models.Fiscal;

public class ItemEmissaoNfceDto
{
    public int ItemNumero { get; set; }
    public string CodigoProduto { get; set; } = string.Empty;
    public string CodigoBarrasEan { get; set; } = string.Empty;
    public string DescricaoProduto { get; set; } = string.Empty;
    public string Ncm { get; set; } = "22021000";
    public int Cfop { get; set; } = 5102;
    public string UnidadeComercial { get; set; } = "UN";
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal => Quantidade * ValorUnitario;
    public string Csosn { get; set; } = "102"; // 102 - Simples Nacional sem crédito, 500 - ST
    public decimal AliquotaTributosAproximadosPercentual { get; set; } = 15.00m;
}

public class PagamentoEmissaoNfceDto
{
    public string MeioPagamento { get; set; } = "01"; // 01 - Dinheiro, 03 - Cartao Credito, 04 - Cartao Debito, 17 - PIX
    public decimal Valor { get; set; }
}

public class DadosEmissaoNfce
{
    public int VendaId { get; set; }
    public string? CpfConsumidor { get; set; }
    public string? NomeConsumidor { get; set; }
    public List<ItemEmissaoNfceDto> Itens { get; set; } = [];
    public List<PagamentoEmissaoNfceDto> Pagamentos { get; set; } = [];
    public decimal ValorTroco { get; set; }
    public bool ModoContingenciaOffline { get; set; } = false;
    public string? JustificativaContingencia { get; set; }
}

public class RetornoEmissaoNfce
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public string ChaveAcesso { get; set; } = string.Empty;
    public long NumeroNota { get; set; }
    public int Serie { get; set; }
    public string XmlAssinado { get; set; } = string.Empty;
    public string UrlQrCode { get; set; } = string.Empty;
    public string DanfeTextoTermica { get; set; } = string.Empty;
    public bool EmitidaEmContingencia { get; set; }
}
