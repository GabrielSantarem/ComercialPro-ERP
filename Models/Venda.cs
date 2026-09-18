using System;
using System.Collections.Generic;

namespace GetStartedApp.Models;

public class Venda
{
    public int Id { get; set; }
    public int VendedorId { get; set; }
    public Vendedor Vendedor { get; set; } = null!;
    public DateTime DataHora { get; set; }
    public decimal ValorTotal { get; set; }
    
    public string Status { get; set; } = "FINALIZADA"; // "FINALIZADA", "CANCELADA"
    public string FormaPagamento { get; set; } = "Dinheiro";

    // Dados Fiscais NFC-e (SEFAZ)
    public string? ChaveAcessoNfce { get; set; }
    public long? NumeroNfce { get; set; }
    public int? SerieNfce { get; set; }
    public string? ProtocoloAutorizacaoNfce { get; set; }
    public string? XmlNfce { get; set; }

    // Dados de Cancelamento Oficial (Evento 110111)
    public DateTime? DataHoraCancelamento { get; set; }
    public string? ProtocoloCancelamento { get; set; }
    public string? JustificativaCancelamento { get; set; }
    public string? XmlCancelamento { get; set; }

    // Assinatura de cadeia antifraude (Blockchain local SHA-256)
    public string HashSeguranca { get; set; } = string.Empty;

    public List<ItemVenda> Itens { get; set; } = [];
}

public class ItemVenda
{
    public int Id { get; set; }
    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}
