using System.Collections.Generic;
using System.Windows;
using MyTools.MyCalculator.Services;

namespace MyTools.MyCalculator;

public partial class HistoryWindow : Window
{
    public record Row(long Id, string Expression, string Result, long UsageCount);

    public string? SelectedExpression { get; private set; }

    public HistoryWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if ((Application.Current as App)?.HistoryStore is { } store && (Application.Current as App) is App app)
        {
            var limit = app.HistoryLimit;
            await LoadListAsync(string.Empty, limit, store);
        }
    }

    private async System.Threading.Tasks.Task LoadListAsync(string query, int limit, HistoryStore store)
    {
        var items = string.IsNullOrWhiteSpace(query)
            ? await store.GetLatestAsync(limit)
            : await store.SearchAsync(query, limit);
        var rows = new List<Row>(items.Count);
        foreach (var it in items) rows.Add(new Row(it.Id, it.Expression, it.Result, it.UsageCount));
        HistoryList.ItemsSource = rows;
    }

    private void HistoryList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is Row row)
        {
            SelectedExpression = row.Expression;
            DialogResult = true;
            Close();
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if ((Application.Current as App)?.HistoryStore is { } store && (Application.Current as App) is App app)
        {
            var query = SearchBox.Text?.Trim() ?? string.Empty;
            await LoadListAsync(query, app.HistoryLimit, store);
        }
    }

    private async void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        if ((Application.Current as App)?.HistoryStore is { } store && (Application.Current as App) is App app)
        {
            await LoadListAsync(string.Empty, app.HistoryLimit, store);
        }
    }
}
