using System.IO;
using GetStartedApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GetStartedApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<Produto> Produtos { get; set; }
    public DbSet<Venda> Vendas { get; set; }
    public DbSet<ItemVenda> ItensVenda { get; set; }
    public DbSet<Vendedor> Vendedores { get; set; }
    public DbSet<EntradaMercadoria> EntradasMercadoria { get; set; }
    public DbSet<ItemEntradaMercadoria> ItensEntradaMercadoria { get; set; }
    public DbSet<CaixaTurno> CaixasTurno { get; set; }
    public DbSet<MovimentacaoCaixa> MovimentacoesCaixa { get; set; }
    public DbSet<PedidoBalcao> PedidosBalcao { get; set; }
    public DbSet<ItemPedidoBalcao> ItensPedidoBalcao { get; set; }
    public DbSet<ContaPagar> ContasPagar { get; set; }
    public DbSet<ContaReceber> ContasReceber { get; set; }
    public DbSet<AjusteEstoque> AjustesEstoque { get; set; }
    public DbSet<ConfiguracaoTerminal> ConfiguracoesTerminal { get; set; }
    public DbSet<ValeCredito> ValesCredito { get; set; }
    public DbSet<Cliente> Clientes { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=pdv.db");
        }
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    public AppDbContext() { }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ValeCredito>()
            .HasIndex(v => v.Codigo)
            .IsUnique();

        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.CpfCnpj);
    }
}
