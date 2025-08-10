using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.Globalization;
using MyTools.Core;
using MyTools.MyCalculator.Services;
using System.Diagnostics;
using System.Windows.Navigation;

namespace MyTools.MyCalculator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ICalculator _calc = new Calculator();

    public MainWindow()
    {
        InitializeComponent();
        ApplyModeVisibility();
        Loaded += (_, __) =>
        {
            ApplyModeMenuChecks();
            // Initial focus is set in XAML via FocusManager.FocusedElement
            // Defer sizing to after initial layout to ensure correct measurement
            Dispatcher.BeginInvoke(new Action(AdjustWindowSizeForMode), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            // Ensure caret blinks by explicitly focusing the input after load
            Dispatcher.BeginInvoke(new Action(() => { FormulaInput.Focus(); Keyboard.Focus(FormulaInput); }), System.Windows.Threading.DispatcherPriority.Background);
        };
        ContentRendered += (_, __) => Dispatcher.BeginInvoke(new Action(AdjustWindowSizeForMode), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void PadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var content = btn.Content?.ToString() ?? string.Empty;

        switch (content)
        {
            case "C":
                FormulaInput.Text = string.Empty;
                ResultText.Text = string.Empty;
                break;
            case "⌫":
                if (!string.IsNullOrEmpty(FormulaInput.Text))
                {
                    var caret = FormulaInput.CaretIndex;
                    if (caret > 0)
                    {
                        FormulaInput.Text = FormulaInput.Text.Remove(caret - 1, 1);
                        FormulaInput.CaretIndex = caret - 1;
                    }
                    else
                    {
                        FormulaInput.Text = FormulaInput.Text[..^1];
                    }
                }
                break;
            case "=":
                EvaluateFormula();
                break;
            case "×":
                InsertText("*");
                break;
            case "÷":
                InsertText("/");
                break;
            default:
                InsertText(content);
                break;
        }
    }

    private void InsertText(string text)
    {
        var caret = FormulaInput.CaretIndex;
        FormulaInput.Text = FormulaInput.Text.Insert(caret, text);
        FormulaInput.CaretIndex = caret + text.Length;
        FormulaInput.Focus();
    }

    private void EvaluateFormula()
    {
        var expr = (FormulaInput.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expr))
        {
            ResultText.Text = string.Empty;
            return;
        }

        try
        {
            var eval = new ExpressionEvaluator(_calc);
            var result = eval.Evaluate(expr);
            ResultText.Text = result.ToString(CultureInfo.InvariantCulture);

            // Save to history
            if (Application.Current is App app && app.HistoryStore is not null)
            {
                _ = app.HistoryStore.AddAsync(expr, ResultText.Text);
                _ = app.HistoryStore.TrimAsync(app.HistoryLimit);
            }
        }
        catch (Exception ex)
        {
            ResultText.Text = ex.Message;
        }
    }

    private bool TryEvalSimpleBinary(string expr, out double result)
    {
        result = double.NaN;

        // Normalize unicode operators
        expr = expr.Replace('×', '*').Replace('÷', '/');

        // Try each operator by first split occurrence not in parentheses (simple approach: use IndexOf)
        // Note: this is simplistic and intended as a starting point
        char[] ops = ['+', '-', '*', '/'];
        foreach (var op in ops)
        {
            var idx = expr.IndexOf(op);
            if (idx > 0 && idx < expr.Length - 1)
            {
                var left = expr[..idx].Trim();
                var right = expr[(idx + 1)..].Trim();
                if (double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    && double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                {
                    result = op switch
                    {
                        '+' => _calc.Add(a, b),
                        '-' => _calc.Subtract(a, b),
                        '*' => _calc.Multiply(a, b),
                        '/' => _calc.Divide(a, b),
                        _ => double.NaN
                    };
                    return true;
                }
            }
        }
        return false;
    }

    private void Evaluate_Click(object sender, RoutedEventArgs e) => EvaluateFormula();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        FormulaInput.Text = string.Empty;
        ResultText.Text = string.Empty;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "My Calculator\nPart of the MyTools suite.", "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenHelp_Click(object sender, RoutedEventArgs e)
    {
        var win = new HelpWindow { Owner = this };
        win.ShowDialog();
    }

    public void HelpLink_RequestNavigate(object? sender, RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch { }
    }

    private void OpenLearnLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe is MenuItem mi)
        {
            var url = mi.CommandParameter as string;
            if (!string.IsNullOrWhiteSpace(url))
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            }
        }
    // Return focus to the input so caret remains visible
    FormulaInput.Focus();
    Keyboard.Focus(FormulaInput);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow { Owner = this };
        if (win.ShowDialog() == true)
        {
            ApplyModeVisibility();
        }
    }

    private void OpenHistory_Click(object sender, RoutedEventArgs e)
    {
        var win = new HistoryWindow { Owner = this };
        if (win.ShowDialog() == true && !string.IsNullOrEmpty(win.SelectedExpression))
        {
            FormulaInput.Text = win.SelectedExpression;
            FormulaInput.CaretIndex = FormulaInput.Text.Length;
            FormulaInput.Focus();
        }
    }

    private void FormulaInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            EvaluateFormula();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Clear_Click(sender, e);
            e.Handled = true;
        }
    }

    private void ApplyModeVisibility()
    {
        var mode = (Application.Current as App)?.Mode ?? "General";
        GeneralPad.Visibility = mode == "General" ? Visibility.Visible : Visibility.Collapsed;
        SciPad.Visibility = mode == "Scientific" ? Visibility.Visible : Visibility.Collapsed;
        FinPad.Visibility = mode == "Financial" ? Visibility.Visible : Visibility.Collapsed;
        // Update screen label text by mode (optional visual cue)
        if (ScreenModeLabel is not null)
        {
            ScreenModeLabel.Text = mode switch
            {
                "Scientific" => "SCI Calculator",
                "Financial" => "FIN Calculator",
                _ => "Calculator"
            };
        }
        ApplyModeMenuChecks();
    // Ensure window fits the visible content for this mode
    AdjustWindowSizeForMode();
    }

    private void ApplyModeMenuChecks()
    {
        var mode = (Application.Current as App)?.Mode ?? "General";
        ModeGeneralItem.IsChecked = mode == "General";
        ModeScientificItem.IsChecked = mode == "Scientific";
        ModeFinancialItem.IsChecked = mode == "Financial";
    }

    private void Mode_General_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.Mode = "General";
            ApplyModeVisibility();
            _ = app.SettingsStore?.SetAsync("Mode", app.Mode);
            FormulaInput.Focus();
            Keyboard.Focus(FormulaInput);
        }
    }

    private void Mode_Scientific_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.Mode = "Scientific";
            ApplyModeVisibility();
            _ = app.SettingsStore?.SetAsync("Mode", app.Mode);
            FormulaInput.Focus();
            Keyboard.Focus(FormulaInput);
        }
    }

    private void Mode_Financial_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.Mode = "Financial";
            ApplyModeVisibility();
            _ = app.SettingsStore?.SetAsync("Mode", app.Mode);
            FormulaInput.Focus();
            Keyboard.Focus(FormulaInput);
        }
    }

    /// <summary>
    /// Resize the window so that all controls for the current mode are visible.
    /// Uses the base AppWindowHeight plus an extra height based on how many
    /// additional rows a mode adds on top of the General pad.
    /// </summary>
    public void AdjustWindowSizeForMode()
    {
        try
        {
            var resources = Application.Current.Resources;
            // Keep width aligned to base width from size resources
            if (resources["AppWindowWidth"] is double baseWidth)
            {
                this.Width = baseWidth;
                this.MinWidth = baseWidth;
            }

            // Fit the height to the current content precisely
            var prev = this.SizeToContent;
            var prevMin = this.MinHeight;
            try
            {
                // Allow shrinking below the base size to compute natural height
                this.MinHeight = 0;
                this.SizeToContent = SizeToContent.Height;
                this.UpdateLayout();

                // Clamp to work area
                var desired = this.ActualHeight;
                double maxHeight = SystemParameters.WorkArea.Height - 40;
                if (desired > maxHeight) desired = maxHeight;

                // Lock the computed height so the window looks stable
                this.MinHeight = desired;
                this.Height = desired;
            }
            finally
            {
                this.SizeToContent = prev;
            }
        }
        catch
        {
            // Non-fatal: keep current size
        }
    }
}