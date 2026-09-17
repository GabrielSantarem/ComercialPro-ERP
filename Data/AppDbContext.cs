using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using GetStartedApp.Models;

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

    // === INTERCEPTADOR ANTIFRAUDE (BLOCKCHAIN FISCAL) ===
    public override async Task<int> SaveChangesAsync(CancellationToken cancel = default)
    {
        var vendasNovas = ChangeTracker.Entries<Venda>()
            .Where(e => e.State == EntityState.Added)
            .ToList();

        if (vendasNovas.Count != 0)
        {
            var ultimaVenda = await Vendas.OrderByDescending(v => v.Id).FirstOrDefaultAsync(cancel);
            string ultimoHash = ultimaVenda?.HashSeguranca ?? "BLOCO_GENESIS_00000000";

            foreach (var entry in vendasNovas)
            {
                var v = entry.Entity;
                string textoCru = $"{ultimoHash}|{v.DataHora:O}|{v.ValorTotal}|{v.VendedorId}";

                byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(textoCru));
                v.HashSeguranca = Convert.ToHexStringLower(hashBytes);

                ultimoHash = v.HashSeguranca;
            }
        }

        return await base.SaveChangesAsync(cancel);
    }
}
