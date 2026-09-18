using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Data;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GetStartedApp.Tests;

public class BackupDatabaseTests : IDisposable
{
    private readonly string _dbName;
    private readonly string _backupDir;
    private readonly AppDbContext _db;
    private readonly BackupDatabaseService _service;

    public BackupDatabaseTests()
    {
        _dbName = $"test_backup_{Guid.NewGuid():N}.db";
        _backupDir = Path.Combine(Path.GetTempPath(), $"backups_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_backupDir);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbName}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _service = new BackupDatabaseService(_db, NullLogger<BackupDatabaseService>.Instance, _backupDir);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_dbName)) File.Delete(_dbName);
        if (Directory.Exists(_backupDir)) Directory.Delete(_backupDir, true);
    }

    [Fact]
    public async Task ExecutarBackupAsync_DeveCriarArquivoZipContendoPdvDb()
    {
        // Act
        var backup = await _service.ExecutarBackupAsync("TesteAutomatizado");

        // Assert
        Assert.NotNull(backup);
        Assert.True(File.Exists(backup.CaminhoCompleto));
        Assert.True(backup.TamanhoBytes > 0);
        Assert.Contains("TesteAutomatizado", backup.NomeArquivo);

        // Valida que o zip contém o arquivo pdv.db
        using var archive = ZipFile.OpenRead(backup.CaminhoCompleto);
        var entry = archive.GetEntry("pdv.db");
        Assert.NotNull(entry);
    }

    [Fact]
    public async Task ListarBackupsExistentesAsync_DeveRetornarBackupsOrdenadosPorData()
    {
        // Arrange (cria dois backups em momentos ligeiramente distintos)
        await _service.ExecutarBackupAsync("Primeiro");
        await Task.Delay(50);
        await _service.ExecutarBackupAsync("Segundo");

        // Act
        var lista = await _service.ListarBackupsExistentesAsync();

        // Assert
        Assert.True(lista.Count >= 2);
        Assert.All(lista, b => Assert.True(File.Exists(b.CaminhoCompleto)));
        // Deve estar ordenado decrescente por data/criação
        Assert.True(lista[0].DataHoraCriacao >= lista[1].DataHoraCriacao);
    }

    [Fact]
    public async Task LimparBackupsAntigosAsync_DeveRemoverApenasBackupsAcimaDoLimite()
    {
        // Arrange: cria um backup recente e um simulando data antiga (> 30 dias)
        var recente = await _service.ExecutarBackupAsync("Recente");
        
        var caminhoAntigo = Path.Combine(_backupDir, "pdv_backup_20260101_000000_Antigo.zip");
        File.Copy(recente.CaminhoCompleto, caminhoAntigo);
        File.SetCreationTime(caminhoAntigo, DateTime.Now.AddDays(-40));

        // Act (expurgo com retenção de 30 dias)
        var apagados = await _service.LimparBackupsAntigosAsync(30);

        // Assert
        Assert.Equal(1, apagados);
        Assert.False(File.Exists(caminhoAntigo));
        Assert.True(File.Exists(recente.CaminhoCompleto));
    }

    [Fact]
    public async Task RestaurarBackupAsync_ArquivoInexistente_DeveLancarFileNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.RestaurarBackupAsync("caminho_inexistente_123.zip"));
    }

    [Fact]
    public async Task RestaurarBackupAsync_ZipSemPdvDb_DeveLancarInvalidOperationException()
    {
        // Arrange (cria um zip corrompido sem o arquivo pdv.db dentro)
        var zipInvalido = Path.Combine(_backupDir, "zip_sem_banco.zip");
        using (var archive = ZipFile.Open(zipInvalido, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("outro_arquivo.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.WriteLine("conteudo");
        }

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RestaurarBackupAsync(zipInvalido));

        Assert.Contains("não contém a base de dados pdv.db", ex.Message);
    }

    [Fact]
    public async Task RestaurarBackupAsync_ZipValido_DeveExtrairPdvDbCorretamente()
    {
        // Arrange
        var backup = await _service.ExecutarBackupAsync("ParaRestauracao");

        // Act
        var sucesso = await _service.RestaurarBackupAsync(backup.CaminhoCompleto);

        // Assert
        Assert.True(sucesso);
        Assert.True(File.Exists("pdv.db"));
    }
}
