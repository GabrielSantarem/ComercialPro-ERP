using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;

namespace GetStartedApp.ViewModels;

public partial class ConfiguracoesViewModel : ViewModelBase
{
    private readonly PdvService _service;
    
    public ObservableCollection<Vendedor> VendedoresLista { get; } = new();

    [ObservableProperty] 
    private string _novoVendedorNome = string.Empty;
    
    [ObservableProperty] 
    private string _mensagemAviso = string.Empty;

    public ConfiguracoesViewModel(PdvService service) 
    {
        _service = service;
        _ = CarregarVendedoresAsync();
    }

    [RelayCommand]
    private async Task CarregarVendedoresAsync() 
    {
        VendedoresLista.Clear();
        var lista = await _service.ObterVendedoresAsync();
        foreach (var v in lista) VendedoresLista.Add(v);
    }

    [RelayCommand]
    private async Task AdicionarVendedorAsync() 
    {
        if (string.IsNullOrWhiteSpace(NovoVendedorNome)) return;
        await _service.AdicionarVendedorAsync(NovoVendedorNome);
        
        NovoVendedorNome = string.Empty;
        MensagemAviso = "Funcionário habilitado com sucesso!";
        
        await CarregarVendedoresAsync();
        _ = LimparAvisoAsync();
    }

    private async Task LimparAvisoAsync() 
    {
        await Task.Delay(3000);
        MensagemAviso = string.Empty;
    }
}
