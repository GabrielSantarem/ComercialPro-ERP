using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using GetStartedApp.ViewModels;

namespace GetStartedApp.Views;

public partial class EntradaNfeView : UserControl
{
    public EntradaNfeView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void BtnImportarXml_Click(object? sender, RoutedEventArgs e)
    {
        if (this.DataContext is not EntradaNfeViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecione o arquivo XML da NF-e (SEFAZ)",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Arquivos XML NF-e (*.xml)")
                {
                    Patterns = ["*.xml", "*.XML"]
                }
            ]
        });

        if (files.Count > 0)
        {
            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            await vm.CarregarXmlAsync(stream);
        }
    }
}
