using System;
using System.Collections.Generic;

namespace GetStartedApp.Models;

public class PedidoBalcao
{
    public int Id { get; set; }
    public string NumeroComanda { get; set; } = string.Empty;
    public int VendedorId { get; set; }
    public Vendedor Vendedor { get; set; } = null!;
    
    public string ClienteNome { get; set; } = "Cliente Balcão";
    public string ClienteCpf { get; set; } = string.Empty;
    
    public DateTime DataHora { get; set; } = DateTime.Now;
    public decimal ValorTotal { get; set; }
    
    // Status: "AGUARDANDO_PAGAMENTO", "FATURADO", "CANCELADO"
    public string Status { get; set; } = "AGUARDANDO_PAGAMENTO";

    // VendaId vinculado quando for faturado no Caixa Central
    public int? VendaId { get; set; }
    public Venda? Venda { get; set; }

    public List<ItemPedidoBalcao> Itens { get; set; } = [];
}

public class ItemPedidoBalcao
{
    public int Id { get; set; }
    public int PedidoBalcaoId { get; set; }
    public PedidoBalcao PedidoBalcao { get; set; } = null!;

    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;

    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Total => Quantidade * PrecoUnitario;
}
