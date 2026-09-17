namespace GetStartedApp.Models;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int Estoque { get; set; }
    
    // Identificadores fiscais e comerciais
    public string? CodigoBarras { get; set; }
    public string? Ncm { get; set; }
    public string? UnidadeMedida { get; set; } = "UN";
    public decimal CustoUltimaCompra { get; set; }
}
