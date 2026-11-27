using Microsoft.Extensions.DependencyInjection;
using RightRule;
using RightRule.Models;
using RightRule.ServiceRegister;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace RightRule_Developer;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;
    public static RightRuleOptions CurrentOptions { get; private set; } = new();
    public static string OptionsFilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RightRule Developer", "RightRuleOptions.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CurrentOptions = LoadOptions();
        ConfigureServices(CurrentOptions);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    public static void ConfigureServices(RightRuleOptions options)
    {
        Services?.Dispose();
        CurrentOptions = options;
        var services = new ServiceCollection();
        services.AddRightRule(options);
        Services = services.BuildServiceProvider();
    }

    private static RightRuleOptions LoadOptions()
    {
        RightRuleOptions options;
        try
        {
            options = File.Exists(OptionsFilePath)
                ? JsonSerializer.Deserialize<RightRuleOptions>(File.ReadAllText(OptionsFilePath)) ?? new RightRuleOptions()
                : CreateDefaultOptions();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load RightRule options. Defaults will be used.\n\n{ex.Message}", "RightRule options", MessageBoxButton.OK, MessageBoxImage.Warning);
            options = CreateDefaultOptions();
        }

        if (string.IsNullOrWhiteSpace(options.CustomOperatorLibraryPath))
            options.CustomOperatorLibraryPath = Path.Combine(AppContext.BaseDirectory, "Operators", "CustomOperators", "customoperatorconfig.json");
        return options;
    }
    private static RightRuleOptions CreateDefaultOptions() => new()
    {
        Diagnostics = true,
        DataCheck = false,
        LoadDefaultOperators = true,
        DefaultOperatorNames = [],
        LoadCustomOperators = true,
        CustomOperatorNames = [],
        CustomOperatorLibraryPath = Path.Combine(AppContext.BaseDirectory, "Operators", "CustomOperators", "customoperatorconfig.json")
    };
    protected override void OnExit(ExitEventArgs e)
    {
        Services?.Dispose();
        base.OnExit(e);
    }
}
