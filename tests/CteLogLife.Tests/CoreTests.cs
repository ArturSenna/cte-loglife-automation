using System.Text.Json;
using CteLogLife.Core;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Excel;
using CteLogLife.Core.Flows;
using CteLogLife.Core.Tax;

namespace CteLogLife.Tests;

public class PyFormatTests
{
    [Theory]
    [InlineData(12.0, "12.0")]
    [InlineData(20.5, "20.5")]
    [InlineData(150.50, "150.5")]
    [InlineData(0.1, "0.1")]
    [InlineData(1234567.89, "1234567.89")]
    public void Float_matches_python_str(double value, string expected) =>
        Assert.Equal(expected, PyFormat.Float(value));

    [Theory]
    [InlineData("150", "150")]
    [InlineData("150.0", "150.0")]
    [InlineData("150.50", "150.5")]
    [InlineData("\"5,00\"", "5,00")]
    [InlineData("true", "True")]
    [InlineData("null", "None")]
    public void Json_values_are_typed_as_python_printed_them(string json, string expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, PyFormat.Json(document.RootElement));
    }

    [Fact]
    public void Title_and_money_and_rounding()
    {
        Assert.Equal("Rua Das Flores Nº 3A", PyFormat.Title("RUA DAS FLORES nº 3a"));
        Assert.Equal(5.00m, PyFormat.Money("5,00"));
        Assert.Equal(150.5m, PyFormat.Money(" 150.5 "));
        Assert.Equal("0.48", PyFormat.Fixed2(0.48m));
        Assert.Equal("2.35", PyFormat.Fixed2(2.345m));
        Assert.Throws<FormatException>(() => PyFormat.Money("abc"));
    }

    [Fact]
    public void Json_helpers_follow_python_truthiness()
    {
        using var document = JsonDocument.Parse("""{"a":0,"b":"","c":12.5,"d":null,"e":[],"f":{"x":1}}""");
        var root = document.RootElement;
        Assert.False(root.Get("a").Truthy());
        Assert.False(root.Get("b").Truthy());
        Assert.True(root.Get("c").Truthy());
        Assert.Null(root.Get("d"));
        Assert.False(root.Get("e").Truthy());
        Assert.True(root.Get("f").Truthy());
        Assert.Equal("12.5", PyFormat.Json(root.Get("a").Or(root.Get("c"))!.Value));
        Assert.Equal("fallback", root.Get("missing").Str("x", "fallback"));
        Assert.Equal("1", root.Get("f").Str("x"));
    }
}

public class TaxTableTests
{
    private static readonly TaxTables Tables = TaxTables.Load(TestRig.DataDir);

    [Fact]
    public void States_resolve_by_full_name_and_by_code()
    {
        Assert.Equal("MG", Tables.UfOf("MINAS GERAIS"));
        Assert.Equal("MG", Tables.UfOf("MG"));
        Assert.Equal("SP", Tables.UfOf("SÃO PAULO"));
        Assert.Null(Tables.UfOf("ATLANTIS"));
        Assert.StartsWith("ICMS devido a MG.", Tables.InfoOf("MG"));
        Assert.StartsWith("ICMS de R$#, pago com crédito presumido de 20%", Tables.InfoOf("SP"));
    }

    [Theory]
    [InlineData("MG", "SP", 12, "12")]
    [InlineData("MG", "MG", 18, "18")]
    [InlineData("MG", "AC", 7, "7")]
    [InlineData("MG", "AL", 7, "7.0")]   // this column has a 20.5, so pandas made it float
    [InlineData("MG", "PR", 12, "12.0")]
    [InlineData("AL", "AL", 20.5, "20.5")]
    [InlineData("SP", "MG", 12, "12")]
    public void Aliquot_text_is_what_the_python_app_typed(string origin, string destination, double value, string text)
    {
        var aliquot = Tables.AliquotFor(origin, destination);
        Assert.NotNull(aliquot);
        Assert.Equal((decimal)value, aliquot.Value.Value);
        Assert.Equal(text, aliquot.Value.Text);
    }

    [Fact]
    public void Icms_from_minas_types_the_rate_and_other_origins_put_the_amount_in_the_observation()
    {
        var fromMg = IcmsCalculator.Compute(Tables, "MINAS GERAIS", "SP", "Protocolo 7", 100m);
        Assert.Equal(("MG", "SP", "12", "9.60"), (fromMg.OriginUf, fromMg.DestinationUf, fromMg.TaxText, fromMg.TpInfo));
        Assert.StartsWith("Protocolo 7 - ICMS devido a MG.", fromMg.Observation);

        var fromSp = IcmsCalculator.Compute(Tables, "SP", "MG", "Protocolo 8", 200m);
        Assert.Equal(("0", "19.20"), (fromSp.TaxText, fromSp.TpInfo));
        Assert.Contains("ICMS de R$19.20, pago", fromSp.Observation);
        Assert.DoesNotContain("#", fromSp.Observation);
    }

    [Fact]
    public void Unknown_state_is_lenient_by_default_and_an_error_when_strict()
    {
        var lenient = IcmsCalculator.Compute(Tables, "", "SP", "Protocolo 9", 50m);
        Assert.Equal(("", "0", "0.00", "Protocolo 9 - "), (lenient.OriginUf, lenient.TaxText, lenient.TpInfo, lenient.Observation));
        Assert.Throws<InvalidDataException>(() =>
            IcmsCalculator.Compute(Tables, "", "SP", "Protocolo 9", 50m, strict: true));
    }
}

public class RunQueueTests
{
    private static RunQueue Loaded(params string[] ids)
    {
        var queue = new RunQueue();
        queue.Load("teste", ids.Select(id => new QueueItem(id, id, "")));
        return queue;
    }

    private static string Order(RunQueue queue) => string.Join("", queue.Snapshot().Select(i => i.Id));

    [Fact]
    public void Items_are_taken_in_order_and_only_pending_ones_move()
    {
        var queue = Loaded("a", "b", "c", "d");
        Assert.Equal("a", queue.TakeNext()!.Id);
        queue.Finish("a", QueueItemState.Done, "ok");

        Assert.False(queue.Move("a", 1));      // already done
        Assert.True(queue.Move("d", -1));      // d before c
        Assert.Equal("abdc", Order(queue));
        Assert.True(queue.MoveToTop("c"));
        Assert.Equal("acbd", Order(queue));
        Assert.False(queue.Move("c", -1));     // already the next one
        Assert.True(queue.Move("c", 99));      // clamped to the end
        Assert.Equal("abdc", Order(queue));

        Assert.Equal("b", queue.TakeNext()!.Id);
        Assert.Equal((4, 1), (queue.Total, queue.Finished));
    }

    [Fact]
    public void Changes_are_announced_and_leftovers_can_be_skipped()
    {
        var queue = Loaded("a", "b");
        var changes = 0;
        queue.Changed += () => changes++;
        queue.Step = "Carga";
        queue.Step = "Carga"; // same value: no event
        queue.TakeNext();
        queue.SkipPending("interrompido");

        Assert.Equal(3, changes);
        Assert.Equal(QueueItemState.Skipped, queue.Snapshot()[1].State);
        Assert.Null(queue.TakeNext());
    }
}

public class RunControlTests
{
    [Fact]
    public void Checkpoint_passes_when_nothing_is_requested_and_throws_on_stop()
    {
        var control = new RunControl();
        Assert.Equal(TimeSpan.Zero, control.Checkpoint());
        control.RequestStop();
        Assert.Throws<RunStoppedException>(() => control.Checkpoint());
        control.Reset();
        Assert.Equal(TimeSpan.Zero, control.Checkpoint());
    }

    [Fact]
    public async Task Pause_blocks_until_resumed_and_reports_the_time_paused()
    {
        var control = new RunControl();
        control.RequestPause();
        var worker = Task.Run(() => control.Checkpoint());
        while (!control.IsPaused) await Task.Delay(5);
        await Task.Delay(60);
        Assert.False(worker.IsCompleted);

        control.Resume();
        var paused = await worker;
        Assert.True(paused >= TimeSpan.FromMilliseconds(50));
        Assert.False(control.IsPaused);
    }

    [Fact]
    public async Task Stop_while_paused_ends_the_wait_with_an_exception()
    {
        var control = new RunControl();
        control.RequestPause();
        var worker = Task.Run(() => control.Checkpoint());
        while (!control.IsPaused) await Task.Delay(5);
        control.RequestStop();
        await Assert.ThrowsAsync<RunStoppedException>(() => worker);
    }

    [Fact]
    public void Critical_section_holds_pause_and_stop_back()
    {
        var control = new RunControl();
        control.BeginCritical();
        control.RequestPause();
        control.RequestStop();
        Assert.Equal(TimeSpan.Zero, control.Checkpoint()); // neither blocks nor throws
        control.EndCritical();
        Assert.Throws<RunStoppedException>(() => control.Checkpoint());
    }
}

public class SmallPiecesTests
{
    [Fact]
    public void Csv_matches_what_pandas_wrote()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            CsvFile.Write(path, ["Protocolo", "Arquivo PDF"], ["1001 ,1002", "00000042.pdf"]);
            var bytes = File.ReadAllBytes(path);
            Assert.NotEqual(0xEF, bytes[0]); // no BOM
            Assert.Equal("Protocolo,Arquivo PDF\r\n\"1001 ,1002\",00000042.pdf\r\n", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void State_names_capitals_and_supplier_codes()
    {
        Assert.Equal("MG", BrazilStates.Abbreviation(" minas gerais "));
        Assert.Equal("SP", BrazilStates.Abbreviation("sp"));
        Assert.Equal("", BrazilStates.Abbreviation(null));
        Assert.Equal("sp", BrazilStates.AbbreviationOrSame("sp"));
        Assert.Equal("Belo Horizonte", BrazilStates.Capital("MG"));
        Assert.Equal("Belo Horizonte", BrazilStates.Capital("MINAS GERAIS"));
        Assert.Equal("80", BrazilStates.SupplierCode("MG"));
        Assert.Null(BrazilStates.SupplierCode("XX"));
    }

    [Theory]
    [InlineData("2.0.0", "1.4.4", true)]
    [InlineData("v1.4.5", "1.4.4", true)]
    [InlineData("1.3.2.1", "1.3.2", true)]
    [InlineData("1.4.4", "1.4.4", false)]
    [InlineData("1.4.3", "1.4.4", false)]
    [InlineData("", "1.4.4", false)]
    public void Update_check_compares_versions(string candidate, string current, bool newer) =>
        Assert.Equal(newer, UpdateChecker.IsNewer(candidate, current));

    [Fact]
    public void Currency_and_document_helpers_of_the_payer_conference()
    {
        Assert.Equal(1234.56m, MaintenanceFlows.ParseCurrency("R$ 1.234,56"));
        Assert.Equal(1234.56m, MaintenanceFlows.ParseCurrency("1,234.56"));
        Assert.Equal(5m, MaintenanceFlows.ParseCurrency(5.0));
        Assert.Null(MaintenanceFlows.ParseCurrency("-"));
        Assert.True(MaintenanceFlows.IsValueFive("5,00"));
        Assert.False(MaintenanceFlows.IsValueFive(50));

        Assert.Equal(14, MaintenanceFlows.ExpectedDocumentLength("1234567000190", ""));
        Assert.Equal(11, MaintenanceFlows.ExpectedDocumentLength("1234567890"));
        Assert.Null(MaintenanceFlows.ExpectedDocumentLength("123"));
        Assert.Equal("01234567000190", MaintenanceFlows.NormalizeDocument("1234567000190", 14));
        Assert.Equal("01.234.567/0001-90", MaintenanceFlows.FormatDocument("1234567000190", 14));
        Assert.Equal("012.345.678-90", MaintenanceFlows.FormatDocument("1234567890", null));
        Assert.Equal("abc", MaintenanceFlows.FormatDocument(" abc ", null));
    }

    [Fact]
    public void Settings_round_trip_and_import_of_the_python_config_files()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "cte-settings-" + Guid.NewGuid())).FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "folderpath.txt"), "C:/Relatórios/Normal\n");
            File.WriteAllBytes(Path.Combine(folder, "filename.txt"), [0x43, 0x3A, 0x2F, 0x45, 0x6D, 0x69, 0x73, 0x73, 0xE3, 0x6F]); // "C:/Emissão" in Windows-1252

            var settings = new AppSettings { ComplementaryFolder = @"D:\já definido" };
            Assert.True(SettingsStore.ImportLegacy(settings, [folder, Path.Combine(folder, "missing")]));
            Assert.Equal(@"C:\Relatórios\Normal", settings.NormalFolder);
            Assert.Equal(@"C:\Emissão", settings.CtePdfFolder);
            Assert.Equal(@"D:\já definido", settings.ComplementaryFolder);

            var file = Path.Combine(folder, "settings.json");
            SettingsStore.Save(settings, file);
            Assert.Equal(@"C:\Emissão", SettingsStore.Load(file).CtePdfFolder);
            File.WriteAllText(file, "{ not json");
            Assert.Equal("", SettingsStore.Load(file).CtePdfFolder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Env_file_credentials_are_parsed_like_dotenv()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".env");
        try
        {
            File.WriteAllText(path, "# auth\nLOGLIFE_USER = a@b.com\nLOGLIFE_PASSWORD=\"p=1\"\nLOGLIFE_XTOKEN = tok \n");
            var credentials = new EnvFileCredentialSource(path).Load();
            // The x-token line of old files is ignored: the token is built into the app.
            Assert.Equal(new Core.Api.ApiCredentials("a@b.com", "p=1"), credentials);
            File.WriteAllText(path, "LOGLIFE_USER=a\n");
            Assert.Null(new EnvFileCredentialSource(path).Load());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
