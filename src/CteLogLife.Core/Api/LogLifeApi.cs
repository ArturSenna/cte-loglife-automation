using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CteLogLife.Core.Api;

/// <summary>The login of one person in the LogLife system.</summary>
public sealed record ApiCredentials(string Email, string Password);

public interface ICredentialSource
{
    /// <summary>The stored credentials, or null when none are configured.</summary>
    ApiCredentials? Load();
}

public sealed class ApiException(string message, HttpStatusCode? status = null, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>Which headers a request carries: the public x-token, the session token, or both.</summary>
public enum ApiAuth { Public, Private, Both }

/// <summary>
/// Client for transportebiologico.com.br. Every call has a timeout, transient failures are retried,
/// and an expired session is renewed once.
/// </summary>
public sealed class LogLifeApi : IDisposable
{
    public const string DefaultBaseUrl = "https://transportebiologico.com.br/api/";
    public const string UploadTypeLogLife = "CTE LOGLIFE";
    public const string UploadTypeComplementary = "CTE COMPLEMENTAR";

    /// <summary>The "xtoken" header of the public routes. It is one value for every user of the system.</summary>
    public const string PublicToken = "myqhF6Nbzx";

    private readonly HttpClient _http;
    private readonly ICredentialSource _credentials;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private ApiCredentials? _loaded;
    private string? _token;

    public LogLifeApi(ICredentialSource credentials, HttpMessageHandler? handler = null,
        string baseUrl = DefaultBaseUrl)
    {
        _credentials = credentials;
        // No proxy: Windows proxy auto-detection (WPAD) used to add ~40 s to the first request.
        handler ??= new SocketsHttpHandler
        {
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(120),
        };
    }

    /// <summary>Waits between attempts after a network error or a 5xx answer.</summary>
    public TimeSpan[] RetryDelays { get; set; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(8)];

    public void Dispose()
    {
        _http.Dispose();
        _loginLock.Dispose();
    }

    /// <summary>Drops the session so the next call logs in again (after the credentials were changed).</summary>
    public void ForgetSession()
    {
        _token = null;
        _loaded = null;
    }

    // ------------------------------------------------------------------ emission sources

    public Task<JsonElement> GetUnifiedEmissionAsync(DateOnly start, DateOnly end, CancellationToken ct) =>
        GetJsonAsync($"public/service/cte-emission-unified?initialDate={Iso(start)}&finalDate={Iso(end)}",
            ApiAuth.Both, ct);

    public Task<JsonElement> GetSymbolicEmissionAsync(DateOnly start, DateOnly end, CancellationToken ct) =>
        GetJsonAsync($"public/service/cte-emission-symbolic?initialDate={Iso(start)}&finalDate={Iso(end)}",
            ApiAuth.Both, ct);

    /// <summary>The service of one protocol prepared for emission, or null when the API has none.</summary>
    public async Task<JsonElement?> GetEmissionServiceAsync(string protocol, CancellationToken ct)
    {
        var root = await GetJsonAsync($"public/service/cte-emission/{Uri.EscapeDataString(protocol)}",
            ApiAuth.Both, ct);
        return root.Get("service");
    }

    /// <summary>Services eligible for a complementary CT-e. The backend message is the exception text on refusal.</summary>
    public async Task<IReadOnlyList<JsonElement>> GetComplementaryEmissionAsync(DateOnly start, DateOnly end,
        IReadOnlyList<long>? protocols, CancellationToken ct)
    {
        var url = $"public/service/cte-emission-complementary?initialDate={Iso(start)}&finalDate={Iso(end)}";
        if (protocols is { Count: > 0 })
            url += "&protocols=" + Uri.EscapeDataString(string.Join(',', protocols));
        var root = await GetJsonAsync(url, ApiAuth.Both, ct);
        return root.Items("services");
    }

    /// <summary>The service a CT-e number belongs to, or null when the API does not know the number.</summary>
    public async Task<JsonElement?> GetServiceByCteAsync(string cteNumber, CancellationToken ct)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get,
                $"public/service/by-cte?cteNumber={Uri.EscapeDataString(cteNumber)}"),
            ApiAuth.Public, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);
        var root = await ReadJsonAsync(response, ct);
        if (root.ValueKind != JsonValueKind.Object)
            throw new ApiException($"Retorno inesperado da API para CT-e {cteNumber}");
        return root;
    }

    public Task<JsonElement> GetCancelledUnsuccessfulAsync(DateOnly start, DateOnly end, CancellationToken ct) =>
        GetJsonAsync($"public/service/cancelled-unsuccessful?initialDate={Iso(start)}&finalDate={Iso(end)}",
            ApiAuth.Public, ct);

    public Task<JsonElement> GetServicesWithCteAsync(DateOnly start, DateOnly end, CancellationToken ct) =>
        GetJsonAsync($"public/service/with-cte?initialDate={Iso(start)}&finalDate={Iso(end)}",
            ApiAuth.Public, ct);

    // ------------------------------------------------------------------ legacy listings (CT-e por UF, limpar simbólico)

    public Task<JsonElement> GetAddressesAsync(CancellationToken ct) =>
        GetJsonAsync("public/address", ApiAuth.Public, ct);

    public Task<JsonElement> GetCollectorsAsync(CancellationToken ct) =>
        GetJsonAsync("public/collector", ApiAuth.Public, ct);

    public Task<JsonElement> GetOngoingServicesAsync(CancellationToken ct) =>
        GetJsonAsync("public/service", ApiAuth.Public, ct);

    public async Task<JsonElement> GetFinalizedServicesAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var url = $"public/service/finalized/?startFilter={Slashed(start)}&endFilter={Slashed(end)}";
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, url), ApiAuth.Public, ct);
        await EnsureSuccessAsync(response, ct);
        return await ReadJsonAsync(response, ct);
    }

    public async Task<JsonElement> GetBillingReportAsync(DateOnly start, DateOnly end, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["is_driver"] = "False",
            ["startFilter"] = $"{Iso(start)}T00:00:00.000-03:00",
            ["endFilter"] = $"{Iso(end)}T23:59:00.000-03:00",
            ["is_customer"] = "False",
            ["is_collector"] = "False",
        };
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "report/billing") { Content = new FormUrlEncodedContent(form) },
            ApiAuth.Private, ct);
        await EnsureSuccessAsync(response, ct);
        return await ReadJsonAsync(response, ct);
    }

    // ------------------------------------------------------------------ writing CT-e numbers

    /// <summary>Records (or, with a null number, clears) the LogLife CT-e of a protocol.</summary>
    public Task PostCteLoglifeAsync(string protocol, long? cteNumber, string? emissionDate, bool symbolic,
        CancellationToken ct)
    {
        var item = new Dictionary<string, object?>
        {
            ["protocol"] = protocol,
            ["cte_loglife"] = cteNumber,
            ["cte_loglife_emission_date"] = emissionDate,
        };
        if (symbolic) item["symbolic"] = true;
        return PostJsonAsync("uploads/cte-loglife/json", new { data = new[] { item } }, ct);
    }

    /// <summary>Records (or, with a null number, clears) the complementary CT-e of a protocol.</summary>
    public Task PostCteComplementaryAsync(string protocol, long? cteNumber, string? emissionDate,
        CancellationToken ct)
    {
        var item = new Dictionary<string, object?>
        {
            ["protocol"] = protocol,
            ["cte_complementary"] = cteNumber,
            ["cte_complementary_emission_date"] = emissionDate,
        };
        return PostJsonAsync("uploads/cte-complementary/json", new { data = new[] { item } }, ct);
    }

    /// <summary>The older CSV upload of CT-e numbers (Protocolo, CTE Loglife, Data Emissão CTE).</summary>
    public Task UploadCteLoglifeCsvAsync(string csvPath, CancellationToken ct) =>
        UploadAsync("uploads/cte-loglife", csvPath, "csv_file", "text/csv", null, ct);

    public Task UploadPdfAsync(string pdfPath, string uploadType, CancellationToken ct) =>
        UploadAsync("pdf", pdfPath, "pdf_files", "application/pdf", uploadType, ct);

    public Task AssociatePdfAsync(string csvPath, string uploadType, CancellationToken ct) =>
        UploadAsync("pdf/associate", csvPath, "csv_file", "text/csv", uploadType, ct);

    // ------------------------------------------------------------------ plumbing

    private async Task<JsonElement> GetJsonAsync(string url, ApiAuth auth, CancellationToken ct)
    {
        using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, url), auth, ct);
        await EnsureSuccessAsync(response, ct);
        return await ReadJsonAsync(response, ct);
    }

    private async Task PostJsonAsync(string url, object payload, CancellationToken ct)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) },
            ApiAuth.Private, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private async Task UploadAsync(string url, string filePath, string fieldName, string contentType,
        string? uploadType, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, ct);
        var fileName = Path.GetFileName(filePath);
        using var response = await SendAsync(() =>
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            file.Headers.TryAddWithoutValidation("Expires", "0");
            var form = new MultipartFormDataContent { { file, fieldName, fileName } };
            if (uploadType is not null) form.Add(new StringContent(uploadType), "type");
            return new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        }, ApiAuth.Private, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> build, ApiAuth auth,
        CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var canRetry = attempt < RetryDelays.Length;
            try
            {
                var response = await SendOnceAsync(build, auth, ct);
                if ((int)response.StatusCode < 500 || !canRetry) return response;
                response.Dispose();
            }
            catch (HttpRequestException) when (canRetry) { }
            catch (TaskCanceledException) when (canRetry && !ct.IsCancellationRequested) { }
            catch (HttpRequestException ex)
            {
                throw new ApiException($"Sem conexão com o sistema LogLife: {ex.Message}", null, ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new ApiException("O sistema LogLife não respondeu a tempo.", null, ex);
            }
            await Task.Delay(RetryDelays[attempt], ct);
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(Func<HttpRequestMessage> build, ApiAuth auth,
        CancellationToken ct)
    {
        var request = build();
        await AuthorizeAsync(request, auth, ct);
        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized || auth == ApiAuth.Public) return response;

        // Session expired: log in again and repeat once.
        response.Dispose();
        _token = null;
        request = build();
        await AuthorizeAsync(request, auth, ct);
        return await _http.SendAsync(request, ct);
    }

    private async Task AuthorizeAsync(HttpRequestMessage request, ApiAuth auth, CancellationToken ct)
    {
        if (auth is ApiAuth.Public or ApiAuth.Both)
            request.Headers.TryAddWithoutValidation("xtoken", PublicToken);
        if (auth is ApiAuth.Private or ApiAuth.Both)
            request.Headers.TryAddWithoutValidation("authorization", await TokenAsync(ct));
    }

    private ApiCredentials Credentials() =>
        _loaded ??= _credentials.Load() ?? throw new ApiException(
            "Login do sistema LogLife não configurado. Informe usuário e senha em Configurações.");

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        if (_token is { } cached) return cached;
        await _loginLock.WaitAsync(ct);
        try
        {
            if (_token is { } raced) return raced;
            var credentials = Credentials();
            using var response = await _http.PostAsJsonAsync("sessions",
                new { email = credentials.Email, password = credentials.Password }, ct);
            if (!response.IsSuccessStatusCode)
                throw new ApiException(
                    $"Login recusado pelo sistema LogLife (HTTP {(int)response.StatusCode}). Verifique usuário e senha.",
                    response.StatusCode);
            var root = await ReadJsonAsync(response, ct);
            var token = root.Str("token");
            if (token.Length == 0) throw new ApiException("O login não retornou um token de sessão.");
            return _token = token;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException($"Sem conexão com o sistema LogLife: {ex.Message}", null, ex);
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        try
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ApiException("O sistema LogLife devolveu uma resposta que não é JSON.", response.StatusCode, ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? message = null;
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(body);
            message = document.RootElement.Str("message");
        }
        catch (JsonException) { }
        throw new ApiException(
            string.IsNullOrEmpty(message) ? $"HTTP {(int)response.StatusCode}" : message, response.StatusCode);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Slashed(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
