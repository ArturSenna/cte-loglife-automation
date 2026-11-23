using System.Runtime.CompilerServices;
using CteLogLife.Bsoft;
using CteLogLife.Core.Automation;

namespace CteLogLife.Tests;

/// <summary>
/// What the driver sends to Bsoft, key by key and click by click, for each kind of work, compared with
/// the sequences kept in the Golden folder. They were recorded from the driver as it was when it was
/// validated against Bsoft, so any change in what is typed or clicked, or in its order, shows up here.
/// Timing is not part of them. To record again on purpose: set CTE_WRITE_GOLDEN=1 and run the tests.
/// </summary>
public class DriverSequenceTests : IDisposable
{
    private readonly TemplateStore _templates = new(Path.Combine(AppContext.BaseDirectory, "Templates"));
    private readonly RunControl _control = new();
    private readonly ListLog _log = new();

    public void Dispose() => _templates.Dispose();

    private static string GoldenFolder([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "Golden");

    private void Check(string name, Action<BsoftDriver> work, bool adaptive = true, params string[] without) =>
        Run(name, work, adaptive, searchResult: false, without);

    /// <summary>
    /// <see cref="Check"/> for the work that searches companies: each search lists one result, so the
    /// first row of the result is on screen too (<see cref="DriverScreen.SearchResultRow"/>).
    /// </summary>
    private void CheckParties(string name, PartiesInput parties, bool adaptive = true, params string[] without) =>
        Run(name, d => d.FillParties(parties), adaptive, searchResult: true, without);

    private void Run(string name, Action<BsoftDriver> work, bool adaptive, bool searchResult, string[] without)
    {
        var desktop = new FakeDesktop();
        using var driver = new BsoftDriver(
            new DesktopBot(_templates, DriverScreen.Build(_templates, searchResult, without), _control, _log, desktop: desktop)
            {
                Adaptive = adaptive,
            }, _log);
        try
        {
            work(driver);
        }
        finally
        {
            _control.EndCritical();
        }

        // Today's date (the ICMS slip dated today) is kept as a mark, so the recording holds on any day.
        var today = DateTime.Today.ToString("dd/MM/yyyy");
        var events = desktop.Events.Select(e => e.Replace(today, TodayMark)).ToList();

        var path = Path.Combine(GoldenFolder(), name + ".txt");
        if (Environment.GetEnvironmentVariable("CTE_WRITE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(GoldenFolder());
            File.WriteAllLines(path, events);
            return;
        }
        Assert.True(File.Exists(path), $"sem sequência gravada: {path}");
        Assert.Equal(File.ReadAllLines(path), events);
    }

    private const string TodayMark = "{hoje}";

    private void Critical() => _control.BeginCritical();

    private static readonly PartiesInput Sender =
        new(["11111111000111", "22222222000122"], ["33333333000133"], Payer.Sender);

    private static readonly TaxInput Mg = new("80.0", "12.0", "9.60", "MG", "ICMS: texto da observação");
    private static readonly TaxInput Pr = Mg with { Uf = "PR" };
    private static readonly TaxInput Complementary = Mg with { Uf = "SP", Complementary = true };

    private static IcmsSlip Slip(DateTime date) =>
        new("123456", "45,67", "7001", "CC LOGLIFE", "8581000000045670", date.ToString("dd/MM/yyyy"));

    // ------------------------------------------------------------------ every element on screen

    [Fact]
    public void Parties_with_the_sender_paying() => CheckParties("parties-sender", Sender);

    [Fact]
    public void Parties_with_the_receiver_paying() =>
        CheckParties("parties-receiver", Sender with { Payer = Payer.Receiver });

    [Fact]
    public void Parties_with_someone_else_paying() =>
        CheckParties("parties-other", Sender with { Payer = Payer.Other, PayerCnpj = "44444444000144" });

    [Fact]
    public void Cargo() => Check("cargo", d => d.FillCargo(new CargoInput("3")));

    [Fact]
    public void Cargo_of_a_second_cte_of_the_same_protocol() =>
        Check("cargo-second", d => d.FillCargo(new CargoInput("3", 2)));

    [Fact]
    public void Original_cte_of_a_complementary_one() => Check("original", d => d.SelectOriginalCte("12345"));

    [Fact]
    public void Taxes_and_transmission_in_minas() =>
        Check("taxes-mg", d => Assert.Equal(4321, d.FillTaxesAndTransmit(Mg, Critical)));

    [Fact]
    public void Taxes_and_transmission_with_the_gnre_note() =>
        Check("taxes-pr", d => Assert.Equal(4321, d.FillTaxesAndTransmit(Pr, Critical)));

    [Fact]
    public void Taxes_and_transmission_of_a_complementary_cte() =>
        Check("taxes-complementary", d => Assert.Equal(4321, d.FillTaxesAndTransmit(Complementary, Critical)));

    [Fact]
    public void Cancellation() => Check("cancel", d => Assert.True(d.CancelCte("98765", Critical)));

    [Fact]
    public void Icms_slip_dated_today() => Check("slip-today", d => d.EnterIcmsSlip(Slip(DateTime.Today)));

    [Fact]
    public void Icms_slip_of_another_day() =>
        Check("slip-other-day", d => d.EnterIcmsSlip(Slip(new DateTime(2020, 3, 14))));

    // ------------------------------------------------------------------ without the optional elements

    [Fact]
    public void Parties_when_the_emission_is_already_started() =>
        CheckParties("parties-sender-started", Sender, true, "emissao_incluir_f1");

    [Fact]
    public void Cargo_when_the_document_type_needs_no_change() =>
        Check("cargo-no-nfe", d => d.FillCargo(new CargoInput("3")), true, "carga_modelo_nfe");

    [Fact]
    public void Taxes_without_any_optional_screen() =>
        Check("taxes-mg-plain", d => Assert.Equal(4321, d.FillTaxesAndTransmit(Mg, Critical)), true,
            "valores_aba_icms", "valores_aba_observacoes", "transmissao_aviso_valor_credito", "transmissao_aviso_contingencia");

    [Fact]
    public void Complementary_without_the_value_confirmation() =>
        Check("taxes-complementary-plain", d => Assert.Equal(4321, d.FillTaxesAndTransmit(Complementary, Critical)),
            true, "transmissao_aviso_valores_alterados", "transmissao_aviso_valor_credito", "transmissao_aviso_contingencia");

    [Fact]
    public void Cancellation_without_the_cte_menu() =>
        Check("cancel-no-menu", d => Assert.True(d.CancelCte("98765", Critical)), true, "cancelamento_menu_cte");

    [Fact]
    public void Cancellation_when_bsoft_is_already_on_the_consultas_tab() =>
        Check("cancel-tab-open", d => Assert.True(d.CancelCte("98765", Critical)), true, "cancelamento_aba_consultas");

    [Fact]
    public void A_cte_that_is_already_cancelled_is_only_looked_up() =>
        Check("cancel-already", d => Assert.False(d.CancelCte("98765", Critical)), true, "cancelamento_status_autorizado");

    // ------------------------------------------------------------------ the fixed-wait mode sends the same things

    [Fact]
    public void With_the_fixed_waits_the_same_is_sent()
    {
        CheckParties("parties-other", Sender with { Payer = Payer.Other, PayerCnpj = "44444444000144" }, adaptive: false);
        Check("cargo", d => d.FillCargo(new CargoInput("3")), adaptive: false);
        Check("taxes-pr", d => Assert.Equal(4321, d.FillTaxesAndTransmit(Pr, Critical)), adaptive: false);
        Check("taxes-mg-plain", d => Assert.Equal(4321, d.FillTaxesAndTransmit(Mg, Critical)), false,
            "valores_aba_icms", "valores_aba_observacoes", "transmissao_aviso_valor_credito", "transmissao_aviso_contingencia");
    }
}
