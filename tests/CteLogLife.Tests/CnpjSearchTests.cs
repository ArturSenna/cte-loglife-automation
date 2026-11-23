using System.Globalization;
using CteLogLife.Bsoft;
using CteLogLife.Core.Automation;
using OpenCvSharp;

namespace CteLogLife.Tests;

/// <summary>
/// Bsoft's company search ("Localização de Clientes e Fornecedores") as it behaves: the grid is empty
/// when the window opens and fills in once a CNPJ is pasted. One CNPJ can list the same company more
/// than once; then there is no "Qtde.: 1" and the first row is the one taken.
/// </summary>
public class CnpjSearchTests : IDisposable
{
    private const string OneResult = "busca_cliente_qtde_1";
    private const string FirstRow = DriverScreen.SearchResultRow;

    private static readonly PartiesInput Parties =
        new(["11111111000111", "22222222000122"], ["33333333000133"], Payer.Sender);

    private readonly TemplateStore _templates = new(Path.Combine(AppContext.BaseDirectory, "Templates"));
    private readonly FakeDesktop _desktop = new();
    private readonly RunControl _control = new();
    private readonly ListLog _log = new();
    private readonly Dictionary<string, Rect> _places;
    private readonly FakeScreen _screen;
    private readonly BsoftDriver _driver;

    public CnpjSearchTests()
    {
        _places = DriverScreen.Layout(_templates, out _);
        _screen = DriverScreen.Build(_templates, OneResult); // and without the result's first row
        _driver = new BsoftDriver(new DesktopBot(_templates, _screen, _control, _log, desktop: _desktop), _log);
    }

    public void Dispose()
    {
        _driver.Dispose();
        _templates.Dispose();
    }

    [Fact]
    public void Several_registrations_of_the_same_cnpj_the_first_row_is_taken()
    {
        Answer(new() { ["11111111000111"] = 2, ["33333333000133"] = 3 });

        _driver.FillParties(Parties);

        Assert.Equal(
            ["clipboard 11111111000111", FirstRowClick, ConfirmClick, "clipboard 33333333000133", FirstRowClick, ConfirmClick],
            SearchEvents());
    }

    [Fact]
    public void A_single_result_is_also_clicked_before_it_is_confirmed()
    {
        Answer(new() { ["11111111000111"] = 1, ["33333333000133"] = 1 });

        _driver.FillParties(Parties);

        Assert.Equal(
            ["clipboard 11111111000111", FirstRowClick, ConfirmClick, "clipboard 33333333000133", FirstRowClick, ConfirmClick],
            SearchEvents());
    }

    [Fact]
    public void The_next_cnpj_is_tried_when_one_lists_nothing()
    {
        Answer(new() { ["22222222000122"] = 2, ["33333333000133"] = 1 });

        _driver.FillParties(Parties);

        Assert.Equal(
            ["clipboard 11111111000111", "clipboard 22222222000122", FirstRowClick, ConfirmClick,
                "clipboard 33333333000133", FirstRowClick, ConfirmClick],
            SearchEvents());
    }

    [Fact]
    public void The_payer_search_takes_the_first_row_too()
    {
        Answer(new() { ["11111111000111"] = 1, ["33333333000133"] = 1, ["44444444000144"] = 2 });

        _driver.FillParties(Parties with { Payer = Payer.Other, PayerCnpj = "44444444000144" });

        Assert.Equal(
            ["clipboard 11111111000111", FirstRowClick, ConfirmClick, "clipboard 33333333000133", FirstRowClick, ConfirmClick,
                "clipboard 44444444000144", FirstRowClick, ConfirmClick],
            SearchEvents());
    }

    [Fact]
    public void Nothing_is_confirmed_when_no_cnpj_finds_anything()
    {
        Answer(new());

        var error = Assert.Throws<ElementNotFoundException>(() => _driver.FillParties(Parties));

        Assert.Contains("não localizou o remetente", error.Message);
        Assert.Equal(["clipboard 11111111000111", "clipboard 22222222000122"], SearchEvents());
    }

    [Fact]
    public void Rows_already_in_the_grid_are_not_taken_for_the_answer()
    {
        // The grid has rows before any CNPJ is typed, and the search never shows "Qtde.: 1": nothing
        // says that the rows came from this CNPJ.
        Show(FirstRow);
        Answer(new() { ["11111111000111"] = 2 });

        Assert.Throws<ElementNotFoundException>(() => _driver.FillParties(Parties));

        Assert.DoesNotContain(FirstRowClick, _desktop.Events);
        Assert.DoesNotContain(ConfirmClick, _desktop.Events);
    }

    // ------------------------------------------------------------------ the fake search window

    /// <summary>
    /// Bsoft answering the search: pasting a CNPJ lists, a moment later, the number of rows given for it
    /// (none when it is not there); "Confirmar" closes the window, which takes the rows away.
    /// </summary>
    private void Answer(Dictionary<string, int> rows)
    {
        var pasted = "";
        var onConfirm = false;
        _desktop.OnEvent = e =>
        {
            if (e.StartsWith("clipboard ", StringComparison.Ordinal))
            {
                pasted = e["clipboard ".Length..];
            }
            else if (e == "up Control")
            {
                var count = rows.GetValueOrDefault(pasted);
                if (count > 0) _desktop.After(150, () => List(count));
            }
            else if (e.StartsWith("move ", StringComparison.Ordinal))
            {
                onConfirm = e == ConfirmClick;
            }
            else if (e == "click" && onConfirm)
            {
                Clear();
            }
        };
    }

    private void List(int count)
    {
        Show(FirstRow);
        if (count == 1) Show(OneResult);
    }

    private void Show(string label) => _screen.Show(_templates.Image(label)!, _places[label].X, _places[label].Y);

    private void Clear()
    {
        foreach (var label in new[] { FirstRow, OneResult })
        {
            var place = _places[label];
            using var blank = new Mat(place.Height, place.Width, MatType.CV_8UC3, new Scalar(240, 240, 240));
            _screen.Show(blank, place.X, place.Y);
        }
    }

    /// <summary>Where the driver clicks to take the first row of the result.</summary>
    private string FirstRowClick =>
        MoveTo(_places[FirstRow].X + BsoftDriver.FirstRowDx, _places[FirstRow].Y + BsoftDriver.FirstRowDy);

    private string ConfirmClick
    {
        get
        {
            var confirm = _places["busca_cliente_confirmar"];
            return MoveTo(confirm.X + confirm.Width / 2.0, confirm.Y + confirm.Height / 2.0);
        }
    }

    private static string MoveTo(double x, double y) =>
        string.Create(CultureInfo.InvariantCulture, $"move {Math.Round(x)},{Math.Round(y)}");

    /// <summary>The CNPJs pasted and the clicks on the first row and on "Confirmar", in order.</summary>
    private List<string> SearchEvents() => _desktop.Events
        .Where(e => e.StartsWith("clipboard ", StringComparison.Ordinal) || e == FirstRowClick || e == ConfirmClick)
        .ToList();
}
