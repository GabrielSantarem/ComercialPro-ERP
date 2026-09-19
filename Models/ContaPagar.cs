using System;

namespace GetStartedApp.Models;

public class ContaPagar
{
    public int Id { get; set; }
    
    public int? EntradaMercadoriaId { get; set; }
    public EntradaMercadoria? EntradaMercadoria { get; set; }

    public string FornecedorNome { get; set; } = string.Empty;
    public string FornecedorCnpj { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string NumeroParcela { get; set; } = string.Empty;
    
    public decimal Valor { get; set; }
    public decimal ValorOriginal => Valor;
    public DateTime DataEmissao { get; set; } = DateTime.Today;
    public DateTime DataVencimento { get; set; }
    
    public DateTime? DataPagamento { get; set; }
    public decimal? ValorPago { get; set; }
    public string FormaPagamento { get; set; } = string.Empty;
    
    public string Status { get; set; } = "PENDENTE"; // PENDENTE, PAGO, CANCELADO
    public string Observacao { get; set; } = string.Empty;

    public bool IsVencido => Status == "PENDENTE" && DataVencimento.Date < DateTime.Today;
    public int DiasAtraso => IsVencido ? (int)(DateTime.Today - DataVencimento.Date).TotalDays : 0;
}
