using System;

namespace GetStartedApp.Models;

public class AjusteEstoque
{
    public int Id { get; set; }
    
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;

    public DateTime DataHora { get; set; } = DateTime.Now;
    
    public string TipoAjuste { get; set; } = "AVARIA"; // AVARIA, VENCIMENTO, BALANCO_FISICO, OUTROS
    public int QuantidadeDiferenca { get; set; } // Negativo para perdas, positivo para sobras
    public int EstoqueAnterior { get; set; }
    public int EstoqueNovo { get; set; }
    
    public string Motivo { get; set; } = string.Empty;
    public string Responsavel { get; set; } = string.Empty;
}
