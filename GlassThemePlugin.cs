using System.Windows;
using Notch.Core.Plugins;

namespace Notch.Glass;

public sealed class GlassThemePlugin : INotchPlugin
{
    private TopBarThemeBinding? _topBar;

    public void Start(IPluginHost host)
    {
        host.Themes.Set(new PluginTheme
        {
            Id = "smoked",
            Name = "Glass / Smoked",
            Description = "Charcoal glass, silver edges and mint highlights.",
            File = "themes/smoked.xaml",
            Base = PluginThemeBase.Dark,
        });

        host.Themes.Set(new PluginTheme
        {
            Id = "frosted",
            Name = "Glass / Frosted",
            Description = "Frosted white glass, graphite text and teal highlights.",
            File = "themes/frosted.xaml",
            Base = PluginThemeBase.Light,
        });

        if (Application.Current is { } app)
        {
            _topBar = new TopBarThemeBinding(app, host.Log);
            _topBar.Start();
        }
    }

    public void Stop()
    {
        _topBar?.Dispose();
        _topBar = null;
        // The host removes registered themes after this call.
    }
}
