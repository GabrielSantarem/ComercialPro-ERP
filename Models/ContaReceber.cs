using System;

namespace GetStartedApp.Models;

public class ContaReceber
{
    public int Id { get; set; }
    
    public int? VendaId { get; set; }
    public Venda? Venda { get; set; }

    public int? PedidoBalcaoId { get; set; }
    public PedidoBalcao? PedidoBalcao { get; set; }

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteCpfCnpj { get; set; } = string.Empty;
    public string ClienteTelefone { get; set; } = string.Empty;

    public string NumeroDocumento { get; set; } = string.Empty;
    public string NumeroParcela { get; set; } = "1/1";
    
    public decimal ValorOriginal { get; set; }
    public decimal JurosMulta { get; set; } = 0m;
    public decimal Desconto { get; set; } = 0m;
    public decimal ValorFinal => Math.Max(0, ValorOriginal + JurosMulta - Desconto);

    public DateTime DataEmissao { get; set; } = DateTime.Today;
    public DateTime DataVencimento { get; set; }
    
    public DateTime? DataRecebimento { get; set; }
    public decimal? ValorRecebido { get; set; }
    public string FormaRecebimento { get; set; } = string.Empty;
    
    public string Status { get; set; } = "PENDENTE"; // PENDENTE, RECEBIDO, CANCELADO
    public string Observacao { get; set; } = string.Empty;

    public bool IsVencido => Status == "PENDENTE" && DataVencimento.Date < DateTime.Today;
    public int DiasAtraso => IsVencido ? (int)(DateTime.Today - DataVencimento.Date).TotalDays : 0;
}
