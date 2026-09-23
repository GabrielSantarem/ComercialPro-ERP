using GetStartedApp.Models;

namespace GetStartedApp.ViewModels;

public class ProdutoItem
{
    public Produto Produto { get; set; } = null!;
    public decimal Quantidade { get; set; }
    public decimal? TotalCustomizado { get; set; }
    public decimal Total => TotalCustomizado ?? (Quantidade * (Produto?.Preco ?? 0m));
}
