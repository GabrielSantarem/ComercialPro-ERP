namespace GetStartedApp.Services;

public class RelatorioInventarioDto
{
    public int ProdutoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeVendida { get; set; }
    public int EstoqueAtualSistema { get; set; }
}
