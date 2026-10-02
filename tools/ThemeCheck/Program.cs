using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Notch.Core.Plugins;
using Notch.Glass;

internal static class Program
{
    private static Application _app = null!;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string folder = Path.GetFullPath(args.ElementAtOrDefault(0) ?? "dist/brick-bread.glass");
            string output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts");
            Directory.CreateDirectory(output);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "plugin.json")));
            Require(manifest.RootElement.GetProperty("apiVersion").GetInt32() == 6, "Manifest API mismatch.");
            Require(File.Exists(Path.Combine(folder, manifest.RootElement.GetProperty("assembly").GetString()!)), "Missing plugin DLL.");
            Require(!File.Exists(Path.Combine(folder, "Notch.Core.dll")), "Package includes Notch.Core.dll.");

            IPluginHost host = DispatchProxy.Create<IPluginHost, ThemeHost>();
            var collector = ((ThemeHost)(object)host).Collector;
            var plugin = new GlassThemePlugin();
            plugin.Start(host);
            Require(collector.Themes.Count == 2, "Expected both theme variants.");
            _app = new Application();

            foreach (PluginTheme theme in collector.Themes)
            {
                string path = Path.Combine(folder, theme.File);
                using var stream = File.OpenRead(path);
                var dictionary = (ResourceDictionary)XamlReader.Load(stream, new ParserContext { BaseUri = new Uri(path) });
                _app.Resources.MergedDictionaries.Clear();
                _app.Resources.MergedDictionaries.Add(dictionary);
                ValidateTypes();
                ValidateContrast();
                Render(theme.Id, output);
                Console.WriteLine($"PASS {theme.Id}: XAML, resource types, contrast and native rendering.");
            }

            plugin.Stop();
            Console.WriteLine($"Previews: {output}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void ValidateTypes()
    {
        foreach (string key in new[] { "IslandBrush", "TextBrush", "AccentBrush", "AccentHoverBrush", "AccentPressedBrush", "CardBrush", "ControlBrush", "HoverBrush", "PressedBrush", "TrackBrush" })
            Require(Resource(key) is Brush, $"{key} must be a brush.");
        foreach (string key in new[] { "UiFontFamily", "CodeFontFamily", "IconFont" })
            Require(Resource(key) is FontFamily, $"{key} must be a font.");
        foreach (string key in new[] { "CardRadius", "ControlRadius", "SmallRadius", "IconButtonRadius" })
            Require(Resource(key) is CornerRadius, $"{key} must be a corner radius.");
        Require(Resource("UiFontSize") is double && Resource("IslandRadiusScale") is double, "Invalid numeric tokens.");
        foreach (var (key, type) in new (string, Type)[] { ("IconButton", typeof(Button)), ("PillButton", typeof(Button)), ("PillTextBox", typeof(TextBox)), ("TabButton", typeof(RadioButton)), ("Card", typeof(Border)), ("CardLabel", typeof(TextBlock)), ("CardValue", typeof(TextBlock)) })
            Require(Resource(key) is Style style && style.TargetType == type, $"Invalid style target for {key}.");
        foreach (string key in new[] { "TerminalBackgroundBrush", "TerminalForegroundBrush", "TerminalCursorBrush", "TerminalSelectionBrush" }.Concat(Enumerable.Range(0, 16).Select(i => $"TerminalAnsi{i}Brush")))
            Require(Resource(key) is SolidColorBrush { Color.A: 255 }, $"{key} must be an opaque solid brush.");
    }

    private static void ValidateContrast()
    {
        var island = (GradientBrush)Resource("IslandBrush");
        var card = (GradientBrush)Resource("CardBrush");
        foreach (Color desktop in new[] { Colors.White, Colors.Black })
        foreach (GradientStop islandStop in island.GradientStops)
        {
            Color background = Composite(islandStop.Color, desktop);
            foreach (Color surface in card.GradientStops.Select(stop => Composite(stop.Color, background)).Append(background))
            foreach (string key in new[] { "TextBrush", "GlassMutedBrush", "AccentBrush" })
            {
                double ratio = Contrast(((SolidColorBrush)Resource(key)).Color, surface);
                Require(ratio >= 4.5, $"{key} contrast is {ratio:F2}:1 on {surface}; expected 4.5:1.");
            }
        }
    }

    private static Color Composite(Color foreground, Color background)
    {
        double alpha = foreground.A / 255.0;
        return Color.FromRgb((byte)Math.Round(foreground.R * alpha + background.R * (1 - alpha)), (byte)Math.Round(foreground.G * alpha + background.G * (1 - alpha)), (byte)Math.Round(foreground.B * alpha + background.B * (1 - alpha)));
    }

    private static double Contrast(Color first, Color second)
    {
        static double Channel(byte value) => value / 255.0 <= 0.04045 ? value / 255.0 / 12.92 : Math.Pow((value / 255.0 + 0.055) / 1.055, 2.4);
        static double Luminance(Color color) => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        double a = Luminance(first), b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static void Render(string id, string output)
    {
        var canvas = new StackPanel { Width = 740, Margin = new Thickness(40) };
        var compact = new Border { Background = Brush("IslandBrush"), CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 8, 16, 8), Width = 340, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 24) };
        compact.Child = Text("Focus session                                      24:38", 12);
        canvas.Children.Add(compact);

        var body = new StackPanel { Width = 600 };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (string label in new[] { "Home", "Terminal", "Stats", "Shelf", "Plugins" })
            tabs.Children.Add(new RadioButton { Content = label, Style = Style("TabButton"), IsChecked = label == "Stats" });
        body.Children.Add(tabs);
        var cards = new UniformGrid { Columns = 3, Margin = new Thickness(-6, 12, -6, 0) };
        foreach (var (label, value, detail) in new[] { ("CPU", "18%", "4.2 GHz"), ("Memory", "8.4 GB", "of 32 GB"), ("GPU", "24%", "48 C"), ("Network", "12.8 MB/s", "Download"), ("Battery", "86%", "Connected"), ("Focus", "24:38", "Session 2 of 4") })
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = label, Style = Style("CardLabel") });
            content.Children.Add(new TextBlock { Text = value, Style = Style("CardValue"), Foreground = Brush("TextBrush") });
            content.Children.Add(new TextBlock { Text = detail, Style = Style("CardLabel"), Margin = new Thickness(0, 4, 0, 8) });
            content.Children.Add(new ProgressBar { Value = 42, Height = 3, BorderThickness = new Thickness(0), Foreground = Brush("AccentBrush"), Background = Brush("TrackBrush") });
            cards.Children.Add(new Border { Style = Style("Card"), Child = content, Height = 124, Margin = new Thickness(6) });
        }
        body.Children.Add(cards);
        canvas.Children.Add(new Border { Background = Brush("IslandBrush"), CornerRadius = new CornerRadius(24), Padding = new Thickness(22, 18, 22, 22), Child = body, HorizontalAlignment = HorizontalAlignment.Center });

        var controls = new StackPanel { Width = 600 };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        foreach (string label in new[] { "+ Codex", "+ Shell", "Disabled" })
            toolbar.Children.Add(new Button { Content = label, Style = Style("PillButton"), IsEnabled = label != "Disabled" });
        foreach (string glyph in new[] { "\uE892", "\uE768", "\uE893" })
            toolbar.Children.Add(new Button { Content = glyph, Style = Style("IconButton"), Margin = new Thickness(8, 0, 0, 0) });
        controls.Children.Add(toolbar);
        controls.Children.Add(new Border { Background = Brush("TerminalBackgroundBrush"), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Child = new TextBlock { Text = "PS C:\\Projects\\Notch> dotnet build\n\n  Notch.Glass -> Notch.Glass.dll\n  Build succeeded.\n  0 Warning(s)   0 Error(s)", FontFamily = (FontFamily)Resource("CodeFontFamily"), FontSize = 12, Foreground = Brush("TerminalForegroundBrush") } });
        controls.Children.Add(new TextBox { Text = "Send a command", Style = Style("PillTextBox"), Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(10, 5, 10, 5) });
        canvas.Children.Add(new Border { Background = Brush("IslandBrush"), CornerRadius = new CornerRadius(24), Padding = new Thickness(22), Child = controls, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) });

        var root = new Border { Background = new SolidColorBrush(Color.FromRgb(109, 127, 124)), Child = canvas };
        root.SetValue(TextElement.FontFamilyProperty, Resource("UiFontFamily"));
        root.SetValue(TextElement.FontSizeProperty, Resource("UiFontSize"));
        root.SetValue(TextElement.ForegroundProperty, Brush("TextBrush"));
        root.Measure(new Size(820, double.PositiveInfinity));
        root.Arrange(new Rect(root.DesiredSize));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(820, (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        Require(pixels.Where((_, index) => index % 4 != 3).Distinct().Count() > 64, "Preview is blank.");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(output, $"{id}.png"));
        encoder.Save(file);
    }

    private static TextBlock Text(string text, double size) => new() { Text = text, FontSize = size, Foreground = Brush("TextBrush"), FontFamily = (FontFamily)Resource("UiFontFamily") };
    private static object Resource(string key) => _app.FindResource(key);
    private static Brush Brush(string key) => (Brush)Resource(key);
    private static Style Style(string key) => (Style)Resource(key);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

public class ThemeHost : DispatchProxy
{
    public ThemeCollector Collector { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "get_Themes" ? Collector : throw new InvalidOperationException($"Unexpected host call: {method?.Name}");
}

public sealed class ThemeCollector : IPluginThemes
{
    public List<PluginTheme> Themes { get; } = [];
    public void Set(PluginTheme theme) => Themes.Add(theme);
    public bool Remove(string id) => Themes.RemoveAll(theme => theme.Id == id) > 0;
    public void Clear() => Themes.Clear();
}
