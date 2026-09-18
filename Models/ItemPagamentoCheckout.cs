namespace GetStartedApp.Models;

public class ItemPagamentoCheckout
{
    public string Forma { get; set; } = "Dinheiro";
    public decimal Valor { get; set; }
    public string MeioPagamentoCodigo { get; set; } = "01";
}
