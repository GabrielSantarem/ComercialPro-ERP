
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using GetStartedApp.Models;

namespace GetStartedApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<Produto> Produtos { get; set; }
    public DbSet<Venda> Vendas { get; set; }
    public DbSet<ItemVenda> ItensVenda { get; set; }
    public DbSet<Vendedor> Vendedores { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // === DICA DE SEGURANÇA: ENCRYPTION (SQLCIPHER) ===
        // Se você instalar o pacote "SQLitePCLRaw.bundle_e_sqlcipher",
        // você protegeria o banco fisicamente ativando a linha abaixo:
        // optionsBuilder.UseSqlite("Data Source=pdv.db;Password=MinhaSenhaUltraSegura123!");

        optionsBuilder.UseSqlite("Data Source=pdv.db");
    }

    // === INTERCEPTADOR ANTIFRAUDE (BLOCKCHAIN FISCAL) ===
    public override async Task<int> SaveChangesAsync(CancellationToken cancel = default)
    {
        // 1. O Entity Framework detecta tudo que "vai ser" salvo antes de ir pro disco.
        var vendasNovas = ChangeTracker.Entries<Venda>()
            .Where(e => e.State == EntityState.Added)
            .ToList();

        if (vendasNovas.Count != 0)
        {
            // 2. Busca a última venda real que está gravada no banco
            var ultimaVenda = await Vendas.OrderByDescending(v => v.Id).FirstOrDefaultAsync(cancel);
            string ultimoHash = ultimaVenda?.HashSeguranca ?? "BLOCO_GENESIS_00000000";

            // 3. Aplica a assinatura para travar o bloco
            foreach (var entry in vendasNovas)
            {
                var v = entry.Entity;
                string textoCru = $"{ultimoHash}|{v.DataHora:O}|{v.ValorTotal}|{v.VendedorId}";

                byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(textoCru));
                v.HashSeguranca = Convert.ToHexStringLower(hashBytes);

                // Caso haja múltiplas vendas sendo salvas na mesma transação:
                ultimoHash = v.HashSeguranca;
            }
        }

        // 4. Libera o fluxo normal para gravar no SQLite
        return await base.SaveChangesAsync(cancel);
    }
}
