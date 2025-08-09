using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using MyTools.MyCalculator.Services;

namespace MyTools.MyCalculator;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	public Services.LocalDb? Db { get; private set; }
	public Services.SettingsStore? SettingsStore { get; private set; }
	public Services.HistoryStore? HistoryStore { get; private set; }
	public int HistoryLimit { get; set; } = 100;
	public string Mode { get; set; } = "General";

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		Log("Startup begin");
		try
		{
			// Initialize local DB and read persisted settings
			Db = new Services.LocalDb();
			Db.InitializeAsync().GetAwaiter().GetResult();
			SettingsStore = new Services.SettingsStore(Db);
			HistoryStore = new Services.HistoryStore(Db);
			var theme = SettingsStore.GetAsync("Theme").GetAwaiter().GetResult() ?? "Themes/Light.xaml";
			var size = SettingsStore.GetAsync("Size").GetAwaiter().GetResult() ?? "Sizes/Medium.xaml";
			Mode = SettingsStore.GetAsync("Mode").GetAwaiter().GetResult() ?? "General";
			if (int.TryParse(SettingsStore.GetAsync("HistoryLimit").GetAwaiter().GetResult(), out var lim))
				HistoryLimit = lim;

			Log($"Loaded settings: Theme='{theme}', Size='{size}', Mode='{Mode}', HistoryLimit={HistoryLimit}");

			ApplyTheme(theme);
			// Validate theme loaded essential resources; if missing, fallback to Light
			if (!HasEssentialThemeResources())
			{
				Log($"Theme '{theme}' missing essentials; falling back to Themes/Light.xaml");
				ApplyTheme("Themes/Light.xaml");
				_ = SettingsStore.SetAsync("Theme", "Themes/Light.xaml");
			}
			ApplySize(size);
		}
		catch (Exception ex)
		{
			Log($"Local theme load failed: {ex}");
		}

		this.DispatcherUnhandledException += (s, ex) =>
		{
			MessageBox.Show($"Unhandled UI exception:\n{ex.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			ex.Handled = true;
		};

		AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
		{
			var msg = (ex.ExceptionObject as Exception)?.Message ?? ex.ExceptionObject?.ToString() ?? "Unknown";
			MessageBox.Show($"Unhandled exception:\n{msg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		};

		TaskScheduler.UnobservedTaskException += (s, ex) =>
		{
			MessageBox.Show($"Unobserved task exception:\n{ex.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			ex.SetObserved();
		};

		try
		{
			Log("Creating MainWindow");
			var win = new MainWindow();
			win.Show();
			Log("MainWindow shown");
		}
		catch (Exception ex)
		{
			Log($"Failed to start: {ex}");
			MessageBox.Show($"Failed to start: {ex}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
			Shutdown(1);
		}
	}

	public void ApplyTheme(string relativePath)
	{
		// Remove previous theme dictionaries (identified by path segment 'Themes/')
		for (int i = Resources.MergedDictionaries.Count - 1; i >= 0; i--)
		{
			var src = Resources.MergedDictionaries[i].Source?.ToString() ?? string.Empty;
			if (src.IndexOf("Themes/", StringComparison.OrdinalIgnoreCase) >= 0)
				Resources.MergedDictionaries.RemoveAt(i);
		}

		// Add via component pack URI to avoid relative resolution issues
		var pack = new Uri($"/MyTools.MyCalculator;component/{relativePath}", UriKind.Relative);
		Resources.MergedDictionaries.Add(new ResourceDictionary { Source = pack });
		Log($"Applied theme: {relativePath}");
	}

	private bool HasEssentialThemeResources()
	{
		// Check for a couple of keys we rely on for visuals
		return Resources.Contains("WindowBackgroundBrush")
			&& Resources.Contains("CalculatorBezelBrush")
			&& Resources.Contains("CalculatorScreenBrush")
			&& Resources.Contains("SquarePadButton");
	}

	public void ApplySize(string relativePath)
	{
		// Remove previous size dictionaries (identified by path segment 'Sizes/')
		for (int i = Resources.MergedDictionaries.Count - 1; i >= 0; i--)
		{
			var src = Resources.MergedDictionaries[i].Source?.ToString() ?? string.Empty;
			if (src.IndexOf("Sizes/", StringComparison.OrdinalIgnoreCase) >= 0)
				Resources.MergedDictionaries.RemoveAt(i);
		}

		var pack = new Uri($"/MyTools.MyCalculator;component/{relativePath}", UriKind.Relative);
		Resources.MergedDictionaries.Add(new ResourceDictionary { Source = pack });
		Log($"Applied size: {relativePath}");

		// Apply to open windows so the overall calculator resizes instantly
		if (Resources["AppWindowWidth"] is double w && Resources["AppWindowHeight"] is double h)
		{
			foreach (Window win in Current.Windows)
			{
				win.Width = w;
				win.Height = h;
				win.MinWidth = w;
				win.MinHeight = h;
				// If this is the main window, allow it to further adjust based on mode
				if (win is MainWindow mw)
				{
					mw.AdjustWindowSizeForMode();
				}
			}
		}
	}

	private static void Log(string message)
	{
		try
		{
			var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyTools", "MyCalculator");
			Directory.CreateDirectory(dir);
			var path = Path.Combine(dir, "app.log");
			File.AppendAllText(path, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
		}
		catch { /* ignore logging errors */ }
	}
}

