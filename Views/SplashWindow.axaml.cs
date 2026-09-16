using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GetStartedApp.Services;
using GetStartedApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GetStartedApp.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var statusText = this.FindControl<TextBlock>("StatusText");
        var app = (App)Application.Current!;

        // Roda a rotina de boot pesada fora da Thread UI para não travar a animação!
        await Task.Run(async () =>
        {
            // 1. Fase: Checando e Migrando Banco de Dados
            Dispatcher.UIThread.Post(() => statusText!.Text = "Verificando consistência do Banco de Dados...");
            await Task.Delay(1500); // Simulando o tempo de infra
            var pdvService = app.Services!.GetRequiredService<PdvService>();
            await pdvService.InicializarBancoDadosAsync();

            // 2. Fase: (Exemplo) Comunicar com Hardware
            Dispatcher.UIThread.Post(() => statusText!.Text = "Iniciando comunicação com Impressora Térmica...");
            await Task.Delay(1500);

            // 3. Fase: Carregar Tela Mestre
            Dispatcher.UIThread.Post(() => statusText!.Text = "Carregando interface principal...");
            await Task.Delay(1000);
        });

        // ============================================
        //  Transição (Swap) de Janelas
        // ============================================
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = app.Services!.GetRequiredService<MainViewModel>();
            
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            
            // Atribui a nova janela principal
            desktop.MainWindow = mainWindow;
            
            // Exibe a janela real de trabalho
            mainWindow.Show();
            
            // Fecha e joga fora a tela de Splash
            this.Close();
        }
    }
}