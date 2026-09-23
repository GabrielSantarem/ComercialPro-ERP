using Avalonia;
using Avalonia.Headless;
using GetStartedApp;

[assembly: AvaloniaTestApplication(typeof(GetStartedApp.Tests.TestAppBuilder))]

namespace GetStartedApp.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
