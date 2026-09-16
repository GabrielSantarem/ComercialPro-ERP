using Microsoft.EntityFrameworkCore;
using GetStartedApp.Models;

namespace GetStartedApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<Vendedor> Vendedores { get; set; } = null!;
    public DbSet<Produto> Produtos { get; set; } = null!;
    public DbSet<Venda> Vendas { get; set; } = null!;
    public DbSet<ItemVenda> ItensVenda { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=pdv.db");
    }
}
