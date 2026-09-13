using System;
using System.Windows;
using Microsoft.Win32;
using RdpVault.Services;

namespace RdpVault;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly SettingsService _settingsService = new();

    public string ThemeChoice { get; private set; } = "Auto";

    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeChoice = _settingsService.Load().Theme;
        Resources.MergedDictionaries.Insert(0, BuildThemeDictionary(ThemeChoice));

        base.OnStartup(e);
    }

    /// <summary>
    /// Switches the active theme immediately and persists the choice for next launch.
    /// </summary>
    public void ApplyTheme(string choice)
    {
        ThemeChoice = choice;
        Resources.MergedDictionaries[0] = BuildThemeDictionary(choice);
        _settingsService.Save(new Models.AppSettings { Theme = choice });
    }

    private static ResourceDictionary BuildThemeDictionary(string choice)
    {
        var effective = choice switch
        {
            "Light" => "Light",
            "Dark" => "Dark",
            _ => IsSystemInLightMode() ? "Light" : "Dark",
        };

        return new ResourceDictionary { Source = new Uri($"Themes/{effective}Theme.xaml", UriKind.Relative) };
    }

    private static bool IsSystemInLightMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (System.Security.SecurityException)
        {
            return true;
        }
    }
}
