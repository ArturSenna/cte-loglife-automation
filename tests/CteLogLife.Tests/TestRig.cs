using System.Net;
using System.Text;
using CteLogLife.Core;
using CteLogLife.Core.Api;
using CteLogLife.Core.Automation;
using CteLogLife.Core.Flows;
using CteLogLife.Core.Journal;
using CteLogLife.Core.Tax;

namespace CteLogLife.Tests;

public sealed record ApiCall(string Method, string Path, string Body, string? XToken, string? Authorization);

/// <summary>Stands in for transportebiologico.com.br: records every call and answers from a table.</summary>
public sealed class FakeApi : HttpMessageHandler
{
    private readonly object _lock = new();
    private readonly List<ApiCall> _calls = [];

    /// <summary>Custom answers; return null to fall back to an empty 200.</summary>
    public Func<ApiCall, HttpResponseMessage?>? Responder { get; set; }

    public IReadOnlyList<ApiCall> Calls { get { lock (_lock) return _calls.ToArray(); } }

    public IEnumerable<ApiCall> To(string pathStart) => Calls.Where(c => c.Path.StartsWith(pathStart));

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        var call = new ApiCall(request.Method.Method, request.RequestUri!.PathAndQuery, body,
            request.Headers.TryGetValues("xtoken", out var x) ? x.First() : null,
            request.Headers.TryGetValues("authorization", out var a) ? a.First() : null);
        lock (_lock) _calls.Add(call);
        if (call.Path == "/api/sessions") return Json("""{"token":"SESSION"}""");
        return Responder?.Invoke(call) ?? Json("{}");
    }
}

/// <summary>A Bsoft that always works unless told otherwise, and writes the PDF like the real one.</summary>
public sealed class FakeDriver : IBsoftDriver
{
    public List<string> Steps { get; } = [];
    public List<PartiesInput> Parties { get; } = [];
    public List<CargoInput> Cargos { get; } = [];
    public List<TaxInput> Taxes { get; } = [];
    public List<string> Cancelled { get; } = [];
    public List<IcmsSlip> Slips { get; } = [];
    public long NextNumber { get; set; } = 1001;
    public string? PdfFolder { get; set; }

    /// <summary>Runs before each transmit; throw to simulate a failure before the point of no return.</summary>
    public Action<TaxInput>? BeforeTransmit { get; set; }

    /// <summary>Runs after the point of no return; return null for "authorized but number unreadable".</summary>
    public Func<long, long?>? AfterTransmit { get; set; }

    public Action<string>? OnCancel { get; set; }
    public string? LastNumberHint { get; set; }

    public void FillParties(PartiesInput parties)
    {
        Steps.Add("parties");
        Parties.Add(parties);
    }

    public void FillCargo(CargoInput cargo)
    {
        Steps.Add("cargo");
        Cargos.Add(cargo);
    }

    public void SelectOriginalCte(string originalCteNumber) => Steps.Add($"original:{originalCteNumber}");

    public long? FillTaxesAndTransmit(TaxInput tax, Action pointOfNoReturn)
    {
        Steps.Add("taxes");
        Taxes.Add(tax);
        BeforeTransmit?.Invoke(tax);
        pointOfNoReturn();
        var number = NextNumber++;
        var result = AfterTransmit is null ? number : AfterTransmit(number);
        if (result is { } written && PdfFolder is not null)
            File.WriteAllBytes(Path.Combine(PdfFolder, $"{written:D8}.pdf"), "%PDF-1.4 test"u8.ToArray());
        return result;
    }

    public bool CancelCte(string cteNumber, Action pointOfNoReturn)
    {
        OnCancel?.Invoke(cteNumber);
        pointOfNoReturn();
        Cancelled.Add(cteNumber);
        return true;
    }

    public void EnterIcmsSlip(IcmsSlip slip) => Slips.Add(slip);
}

public sealed class RecordingOperator : IOperator
{
    public List<string> Notices { get; } = [];
    public List<(string Message, string? Prefill)> Questions { get; } = [];
    public Queue<string?> Answers { get; } = new();

    public string? AskText(string title, string message, string? prefill = null)
    {
        Questions.Add((message, prefill));
        return Answers.Count > 0 ? Answers.Dequeue() : null;
    }

    public void Notice(string message) => Notices.Add(message);
}

public sealed class ListLog : IRunLog
{
    public List<string> Lines { get; } = [];
    public void Info(string message) { lock (Lines) Lines.Add("INFO " + message); }
    public void Warn(string message) { lock (Lines) Lines.Add("WARN " + message); }
    public void Error(string message) { lock (Lines) Lines.Add("ERROR " + message); }
}

public sealed class FixedCredentials : ICredentialSource
{
    public ApiCredentials? Load() => new("user@example.com", "secret");
}

/// <summary>A complete flow context over temp folders, the fake API and the fake Bsoft.</summary>
public sealed class TestRig : IDisposable
{
    public static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "Data");

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "cte-tests-" + Guid.NewGuid().ToString("N"));
    public string ReportFolder { get; }
    public string PdfFolder { get; }
    public FakeApi Http { get; } = new();
    public FakeDriver Driver { get; } = new();
    public RecordingOperator Operator { get; } = new();
    public ListLog Log { get; } = new();
    public RunControl Control { get; } = new();
    public RunQueue Queue { get; } = new();
    public EmissionJournal Journal { get; }
    public LogLifeApi Api { get; }
    public AppSettings Settings { get; }
    public FlowContext Ctx { get; }
    public DateTime Now { get; set; } = new(2026, 11, 17, 14, 30, 0);

    public TestRig()
    {
        ReportFolder = Directory.CreateDirectory(Path.Combine(Root, "reports")).FullName;
        PdfFolder = Directory.CreateDirectory(Path.Combine(Root, "pdf")).FullName;
        Driver.PdfFolder = PdfFolder;
        Journal = new EmissionJournal(Path.Combine(Root, "journal", "emissions.jsonl"));
        Api = new LogLifeApi(new FixedCredentials(), Http) { RetryDelays = [] };
        Settings = new AppSettings
        {
            NormalFolder = ReportFolder,
            ComplementaryFolder = ReportFolder,
            CtePdfFolder = PdfFolder,
            PdfWaitSeconds = 2,
        };
        var tax = new Lazy<TaxTables>(() => TaxTables.Load(DataDir));
        Ctx = new FlowContext
        {
            Api = Api,
            Tax = () => tax.Value,
            CreateDriver = () => Driver,
            Journal = Journal,
            Operator = Operator,
            Log = Log,
            Control = Control,
            Queue = Queue,
            Settings = Settings,
            Now = () => Now,
            DebugFolder = Path.Combine(Root, "debug"),
            PdfPollInterval = TimeSpan.FromMilliseconds(10),
        };
    }

    public void Dispose()
    {
        Api.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Two services as the unified endpoint returns them: one symbolic (MG to SP), one normal (SP to MG).</summary>
    public const string UnifiedJson = """
    {
      "total": 2, "symbolic_count": 1, "normal_count": 1, "normal_rodoviario_count": 1,
      "services": [
        {
          "protocol": 101, "is_symbolic": true, "priority_label": "SIMBÓLICO", "step": "toCollectService",
          "customer": { "trading_firstname": "LAB A", "cnpj_cpf": "11.111.111/0001-11" },
          "requested_service": { "collect_date": "2026-11-10T02:00:00.000Z", "price": 150.5 },
          "source_city": "BELO HORIZONTE", "destination_city": "SÃO PAULO",
          "source_addresses": [ { "trading_name": "ORIGEM", "branch": "MATRIZ", "street": "RUA DAS FLORES", "number": "10",
            "neighborhood": "CENTRO", "city_name": "BELO HORIZONTE", "city_state": "MINAS GERAIS", "cep": "30000-000",
            "cnpj_cpf": "11.111.111/0001-11" } ],
          "destination_addresses": [ { "trading_name": "DESTINO", "branch": "FILIAL", "street": "AV PAULISTA", "number": "1000",
            "neighborhood": "BELA VISTA", "city_name": "SÃO PAULO", "city_state": "SP", "cep": "01000-000",
            "cnpj_cpf": "22.222.222/0001-22" } ],
          "source_collector": { "trading_name": "COLETADOR MG" },
          "destination_collector": { "trading_name": "COLETADOR SP" },
          "symbolic_emission_data": { "source_collector_cnpj": "33.333.333/0001-33",
            "destination_collector_cnpj": "44.444.444/0001-44", "tomador": "Remetente", "fixed_value": "5,00",
            "volumes": 3, "fixed_cte_instance": 1 }
        },
        {
          "protocol": 102, "is_symbolic": false, "priority_label": "NORMAL RODOVIÁRIO", "step": "finishedService",
          "customer": { "trading_firstname": "LAB B", "cnpj_cpf": "66.666.666/0001-66" },
          "requested_service": { "collect_date": "2026-11-11T15:00:00.000Z", "price": 200 },
          "source_city": "SÃO PAULO", "destination_city": "UBERLÂNDIA",
          "source_addresses": [
            { "trading_name": "A", "branch": "1", "street": "RUA A", "number": "1", "neighborhood": "X",
              "city_name": "SÃO PAULO", "city_state": "SP", "cep": "01000-000", "cnpj_cpf": "55.555.555/0001-55" } ],
          "destination_addresses": [
            { "trading_name": "B", "branch": "2", "street": "RUA B", "number": "2", "neighborhood": "Y",
              "city_name": "UBERLÂNDIA", "city_state": "MG", "cep": "38000-000", "cnpj_cpf": "66.666.666/0001-66" },
            { "trading_name": "C", "branch": "3", "street": "RUA C", "number": "3", "neighborhood": "Z",
              "city_name": "UBERLÂNDIA", "city_state": "MG", "cep": "38000-001", "cnpj_cpf": "77.777.777/0001-77" } ],
          "source_collector": { "trading_name": "COLETADOR SP" },
          "destination_collector": null
        }
      ]
    }
    """;

    /// <summary>Answers the unified listing with <see cref="UnifiedJson"/> (or the given body).</summary>
    public void ServeUnified(string? json = null, Func<ApiCall, HttpResponseMessage?>? otherwise = null) =>
        Http.Responder = call => call.Path.StartsWith("/api/public/service/cte-emission-unified")
            ? FakeApi.Json(json ?? UnifiedJson)
            : otherwise?.Invoke(call);
}
