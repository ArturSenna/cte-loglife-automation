using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CteLogLife.Core.Api;
using CteLogLife.Core.Flows;
using Microsoft.Win32;

namespace CteLogLife.App;

public partial class ConferenceRow(PayerInconsistency item) : ObservableObject
{
    public PayerInconsistency Item { get; } = item;

    [ObservableProperty] private bool _isSelected;
}

/// <summary>Payer conference: pick the Bsoft spreadsheet, review the differences, cancel the wrong CT-es.</summary>
public partial class ConferenceWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ObservableCollection<ConferenceRow> _rows = [];

    public ConferenceWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        ColumnBox.Text = viewModel.PayerColumn;
        Grid.ItemsSource = _rows;
    }

    /// <summary>Fills the table (also used to render a preview of this window).</summary>
    public void ShowRows(IEnumerable<PayerInconsistency> inconsistencies)
    {
        _rows.Clear();
        foreach (var item in inconsistencies) _rows.Add(new ConferenceRow(item));
        EmptyText.Visibility = Visibility.Collapsed;
        Grid.Visibility = Visibility.Visible;
        SelectionButtons.IsEnabled = true;
        CancelCtesButton.IsEnabled = true;
        StatusText.Text = $"{_rows.Count} inconsistência(s) encontrada(s)";
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecione o arquivo para conferência",
            Filter = "Arquivos Excel (*.xlsx;*.xls)|*.xlsx;*.xls|Excel 2007+ (*.xlsx)|*.xlsx|Excel 97-2003 (*.xls)|*.xls",
        };
        if (dialog.ShowDialog(this) == true) FileBox.Text = dialog.FileName;
    }

    private void OnSaveDefault(object sender, RoutedEventArgs e)
    {
        var column = ColumnBox.Text.Trim();
        if (column.Length == 0)
        {
            MessageDialog.Show(this, "Conferência de tomador", "⚠️ Informe o nome da coluna.");
            return;
        }
        _viewModel.PayerColumn = column;
        MessageDialog.Show(this, "Conferência de tomador", "✅ Coluna padrão salva.");
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        var file = FileBox.Text.Trim();
        var column = ColumnBox.Text.Trim();
        if (file.Length == 0)
        {
            MessageDialog.Show(this, "Conferência de tomador", "⚠️ Selecione um arquivo Excel.");
            return;
        }
        if (column.Length == 0)
        {
            MessageDialog.Show(this, "Conferência de tomador", "⚠️ Informe o nome da coluna.");
            return;
        }

        StartButton.IsEnabled = BrowseButton.IsEnabled = false;
        EmptyText.Text = "Conferindo tomadores…";
        try
        {
            var result = await _viewModel.ConferPayersAsync(file, column);
            if (result is null) return;
            if (result.Inconsistencies.Count == 0)
            {
                EmptyText.Text = "Nenhuma divergência encontrada.";
                MessageDialog.Show(this, "Conferência de tomador",
                    "✅ Conferência concluída!\n\n"
                    + $"Linhas conferidas: {result.CheckedRows}\n"
                    + $"Linhas ignoradas: {result.SkippedRows}\n\n"
                    + $"Linhas com valor 5 ignoradas: {result.ValueFiveRows}\n"
                    + $"Serviços não encontrados na API: {result.NotFoundRows}\n\n"
                    + "Nenhuma divergência encontrada.");
                return;
            }
            ShowRows(result.Inconsistencies);
        }
        catch (Exception ex) when (ex is FlowException or ApiException or IOException or InvalidDataException)
        {
            EmptyText.Text = "Selecione a planilha e inicie a conferência.";
            MessageDialog.Show(this, "Conferência de tomador", "❌ " + ex.Message);
        }
        finally
        {
            StartButton.IsEnabled = BrowseButton.IsEnabled = true;
        }
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.IsSelected = true;
    }

    private void OnClearSelection(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.IsSelected = false;
    }

    private void OnCopySelected(object sender, RoutedEventArgs e)
    {
        var selected = _rows.Where(r => r.IsSelected).Select(r => r.Item).ToList();
        if (selected.Count == 0)
        {
            MessageDialog.Show(this, "Conferência de tomador", "⚠️ Selecione pelo menos uma linha para copiar.");
            return;
        }
        var lines = new List<string> { "CT-e\tLinha\tPagador\tPlanilha\tCadastro\tMotivo" };
        lines.AddRange(selected.Select(item =>
            $"{item.CteNumber}\t{item.RowNumber}\t{Dash(item.PayerName)}\t{Dash(item.FileValue)}\t{Dash(item.ApiValue)}\t{item.Reason}"));
        try
        {
            Clipboard.SetText(string.Join("\n", lines));
            StatusText.Text = $"{selected.Count} linha(s) copiada(s).";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            StatusText.Text = "A área de transferência está ocupada; tente de novo.";
        }

        static string Dash(string value) => value.Length == 0 ? "-" : value;
    }

    private async void OnCancelSelected(object sender, RoutedEventArgs e)
    {
        var numbers = _rows.Where(r => r.IsSelected).Select(r => r.Item.CteNumber).Distinct().ToList();
        if (numbers.Count == 0)
        {
            MessageDialog.Show(this, "Conferência de tomador", "⚠️ Selecione pelo menos um CT-e.");
            return;
        }
        if (!MessageDialog.Confirm(this, "Confirmar cancelamento",
                $"Cancelar {numbers.Count} CT-e(s) selecionado(s)?\n\nEsta ação não pode ser desfeita.", "Cancelar CT-es"))
            return;

        Close();
        await _viewModel.CancelFromConferenceAsync(numbers);
    }
}
