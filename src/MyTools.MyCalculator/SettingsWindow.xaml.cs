using System.Windows;
using System.Threading.Tasks;

namespace MyTools.MyCalculator;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += SettingsWindow_Loaded;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not App app)
            return;

        // Load from the SettingsStore (falls back to current App values)
        string? theme = null, size = null, mode = null, hist = null;
        try
        {
            if (app.SettingsStore is not null)
            {
                theme = await app.SettingsStore.GetAsync("Theme");
                size = await app.SettingsStore.GetAsync("Size");
                mode = await app.SettingsStore.GetAsync("Mode");
                hist = await app.SettingsStore.GetAsync("HistoryLimit");
            }
        }
        catch { /* ignore and use app defaults */ }

        // Theme: prefer persisted value; if missing, detect from merged dictionaries
        if (string.IsNullOrEmpty(theme))
        {
            foreach (var d in Application.Current.Resources.MergedDictionaries)
            {
                var src = d.Source?.ToString() ?? string.Empty;
                if (src.Contains("Themes/", StringComparison.OrdinalIgnoreCase))
                {
                    // src could be pack URI or relative; take trailing path
                    var idx = src.IndexOf("Themes/", StringComparison.OrdinalIgnoreCase);
                    theme = src[idx..];
                    break;
                }
            }
        }
        var themePath = theme ?? "Themes/Light.xaml";
        ThemeCombo.SelectedIndex = themePath switch
        {
            "Themes/Dark.xaml" => 1,
            "Themes/Teal.xaml" => 2,
            _ => 0
        };

        // Size: prefer persisted value; if missing, detect from merged dictionaries
        if (string.IsNullOrEmpty(size))
        {
            foreach (var d in Application.Current.Resources.MergedDictionaries)
            {
                var src = d.Source?.ToString() ?? string.Empty;
                if (src.Contains("Sizes/", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = src.IndexOf("Sizes/", StringComparison.OrdinalIgnoreCase);
                    size = src[idx..];
                    break;
                }
            }
        }
        var sizePath = size ?? "Sizes/Medium.xaml";
        SizeCombo.SelectedIndex = sizePath switch
        {
            "Sizes/Small.xaml" => 0,
            "Sizes/Large.xaml" => 2,
            _ => 1
        };

        // Mode
        var modeVal = mode ?? app.Mode ?? "General";
        ModeCombo.SelectedIndex = modeVal switch
        {
            "Scientific" => 1,
            "Financial" => 2,
            _ => 0
        };

        // History limit
        if (!string.IsNullOrEmpty(hist))
            HistoryLimitText.Text = hist;
        else
            HistoryLimitText.Text = app.HistoryLimit.ToString();
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
    var idx = ThemeCombo.SelectedIndex;
    // Apply and persist theme/size/mode
    var app = (Application.Current as App)!;

    switch (idx)
        {
            case 0:
                app.ApplyTheme("Themes/Light.xaml");
                break;
            case 1:
                app.ApplyTheme("Themes/Dark.xaml");
                break;
            case 2:
                app.ApplyTheme("Themes/Teal.xaml");
                break;
        }

        var sizeIdx = SizeCombo.SelectedIndex;
        switch (sizeIdx)
        {
            case 0:
                app.ApplySize("Sizes/Small.xaml");
                break;
            case 1:
                app.ApplySize("Sizes/Medium.xaml");
                break;
            case 2:
                app.ApplySize("Sizes/Large.xaml");
                break;
        }

        var modeIdx = ModeCombo.SelectedIndex;
        var mode = modeIdx switch { 0 => "General", 1 => "Scientific", 2 => "Financial", _ => "General" };
        app.Mode = mode;

        if (int.TryParse(HistoryLimitText.Text, out var limit) && limit > 0)
            app.HistoryLimit = limit;

    // Persist settings
        if (app.SettingsStore is not null)
        {
            await app.SettingsStore.SetAsync("Theme", idx switch { 0 => "Themes/Light.xaml", 1 => "Themes/Dark.xaml", 2 => "Themes/Teal.xaml", _ => "Themes/Light.xaml" });
            await app.SettingsStore.SetAsync("Size", sizeIdx switch { 0 => "Sizes/Small.xaml", 1 => "Sizes/Medium.xaml", 2 => "Sizes/Large.xaml", _ => "Sizes/Medium.xaml" });
            await app.SettingsStore.SetAsync("Mode", mode);
            await app.SettingsStore.SetAsync("HistoryLimit", app.HistoryLimit.ToString());
        }

    // Optional: small log entry
    (Application.Current as App)?.GetType(); // no-op to keep file consistent

        DialogResult = true;
        Close();
    }
}
