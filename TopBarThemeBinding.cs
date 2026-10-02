using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Notch.Core.Plugins;

namespace Notch.Glass;

// WNotch API 6 captures plugin-tab styles with FindResource. Rebind those local
// values so they follow the same resource as the built-in tabs when themes change.
internal sealed class TopBarThemeBinding(Application app, IPluginLog log) : IDisposable
{
    private readonly Dictionary<FrameworkElement, Dictionary<DependencyProperty, PropertyChange>> _changes = [];
    private DispatcherTimer? _discovery;
    private StackPanel? _strip;
    private Button? _settings;
    private int _stopped;
    private bool _refreshing;

    internal void Start() => app.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => Guard(() =>
    {
        if (TryAttach()) return;
        _discovery = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _discovery.Tick += Discover;
        _discovery.Start();
    })));

    private void Discover(object? sender, EventArgs args) => Guard(() =>
    {
        if (TryAttach()) _discovery?.Stop();
    });

    private bool TryAttach()
    {
        foreach (Window window in app.Windows)
        {
            if (window.FindName("TabStrip") is not StackPanel strip) continue;
            if (!strip.Children.OfType<RadioButton>().Any(tab => tab.Name == "TabHome")) continue;
            _strip = strip;
            _settings = window.FindName("OpenSettingsButton") as Button;
            strip.LayoutUpdated += LayoutUpdated;
            Refresh(strip, _settings);
            return true;
        }
        return false;
    }

    private void LayoutUpdated(object? sender, EventArgs args) => Guard(() =>
    {
        if (_strip is { } strip) Refresh(strip, _settings);
    });

    internal void Refresh(StackPanel strip, Button? settings = null)
    {
        app.Dispatcher.VerifyAccess();
        if (_refreshing || Volatile.Read(ref _stopped) != 0) return;
        _refreshing = true;
        try
        {
            var tabs = strip.Children.OfType<RadioButton>().ToHashSet();
            foreach (FrameworkElement removed in _changes.Keys.Where(element => element != settings && (element is not RadioButton tab || !tabs.Contains(tab))).ToArray())
                Restore(removed);

            if (strip.TryFindResource("TabButton") is Style)
            {
                foreach (RadioButton tab in tabs)
                {
                    // Built-in tabs already have resource expressions; preserve those.
                    if (tab.ReadLocalValue(FrameworkElement.StyleProperty) is Style)
                        Bind(tab, FrameworkElement.StyleProperty, "TabButton");
                }
            }
            if (settings is not null && strip.TryFindResource("GlassTopBarHeight") is double)
            {
                Bind(settings, FrameworkElement.HeightProperty, "GlassTopBarHeight");
                Bind(settings, FrameworkElement.WidthProperty, "GlassTopBarIconWidth");
                Bind(settings, Control.PaddingProperty, "GlassTopBarIconPadding");
            }
        }
        finally { _refreshing = false; }
    }

    private void Bind(FrameworkElement element, DependencyProperty property, string key)
    {
        object previous = element.ReadLocalValue(property);
        if (!_changes.TryGetValue(element, out var properties))
            _changes[element] = properties = [];
        if (properties.TryGetValue(property, out PropertyChange? change) && ReferenceEquals(previous, change.Applied)) return;
        element.SetResourceReference(property, key);
        properties[property] = new PropertyChange(previous, element.ReadLocalValue(property));
    }

    private void Restore(FrameworkElement element)
    {
        foreach (var (property, change) in _changes[element])
        {
            // Leave any subsequent change made by the host or another plugin intact.
            if (!ReferenceEquals(element.ReadLocalValue(property), change.Applied)) continue;
            if (change.Previous == DependencyProperty.UnsetValue) element.ClearValue(property);
            else element.SetValue(property, change.Previous);
        }
        _changes.Remove(element);
    }

    private void Guard(Action action)
    {
        if (Volatile.Read(ref _stopped) != 0) return;
        try { action(); }
        catch (Exception error) { log.Error("Could not synchronise the notch's top bar.", error); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        if (app.Dispatcher.CheckAccess()) SafeDetach();
        else if (!app.Dispatcher.HasShutdownStarted)
            app.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(SafeDetach));
    }

    private void SafeDetach()
    {
        try { Detach(); }
        catch (Exception error) { log.Error("Could not restore the notch's top bar.", error); }
    }

    private void Detach()
    {
        _discovery?.Stop();
        if (_discovery is not null) _discovery.Tick -= Discover;
        if (_strip is not null) _strip.LayoutUpdated -= LayoutUpdated;
        foreach (FrameworkElement element in _changes.Keys.ToArray()) Restore(element);
        _strip = null;
        _settings = null;
    }

    private sealed record PropertyChange(object Previous, object Applied);
}
