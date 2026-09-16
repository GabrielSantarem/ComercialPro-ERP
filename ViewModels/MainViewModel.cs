using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GetStartedApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; } = null!;

    [ObservableProperty]
    private bool _isHome; // Propriedade para gerenciar o estado da navbar no topo

    public MainViewModel(IServiceProvider services)
    {
        _services = services;
        NavigateToHome(); // Inicia no HUB de módulos
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        var vm = _services.GetRequiredService<HomeModulesViewModel>();
        CurrentPage = vm;
        IsHome = true;
    }

    [RelayCommand]
    private void NavigateToDashboard() 
    {
        var vm = _services.GetRequiredService<DashboardViewModel>();
        _ = vm.CarregarMetricasAsync();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    private void NavigateToPdv() 
    {
        var vm = _services.GetRequiredService<PdvViewModel>();
        _ = vm.InicializarAsync();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    private void NavigateToEstoque() 
    {
        var vm = _services.GetRequiredService<EstoqueViewModel>();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    private void NavigateToEntradaNfe()
    {
        var vm = _services.GetRequiredService<EntradaNfeViewModel>();
        _ = vm.CarregarCatalogoAsync();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    private void NavigateToConfiguracoes() 
    {
        var vm = _services.GetRequiredService<ConfiguracoesViewModel>();
        CurrentPage = vm;
        IsHome = false;
    }
}
