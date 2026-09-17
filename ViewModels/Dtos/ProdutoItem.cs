using GetStartedApp.Models;

namespace GetStartedApp.ViewModels;

public class ProdutoItem
{
    public Produto Produto { get; set; } = null!;
    public int Quantidade { get; set; }
    public decimal Total => Quantidade * (Produto?.Preco ?? 0m);
}
