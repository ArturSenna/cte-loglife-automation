namespace CteLogLife.Core;

public static class BrazilStates
{
    private static readonly Dictionary<string, string> NameToUf = new(StringComparer.Ordinal)
    {
        ["ACRE"] = "AC", ["ALAGOAS"] = "AL", ["AMAPÁ"] = "AP", ["AMAZONAS"] = "AM",
        ["BAHIA"] = "BA", ["CEARÁ"] = "CE", ["DISTRITO FEDERAL"] = "DF",
        ["ESPÍRITO SANTO"] = "ES", ["GOIÁS"] = "GO", ["MARANHÃO"] = "MA",
        ["MATO GROSSO"] = "MT", ["MATO GROSSO DO SUL"] = "MS",
        ["MINAS GERAIS"] = "MG", ["PARÁ"] = "PA", ["PARAÍBA"] = "PB",
        ["PARANÁ"] = "PR", ["PERNAMBUCO"] = "PE", ["PIAUÍ"] = "PI",
        ["RIO DE JANEIRO"] = "RJ", ["RIO GRANDE DO NORTE"] = "RN",
        ["RIO GRANDE DO SUL"] = "RS", ["RONDÔNIA"] = "RO", ["RORAIMA"] = "RR",
        ["SANTA CATARINA"] = "SC", ["SÃO PAULO"] = "SP", ["SERGIPE"] = "SE",
        ["TOCANTINS"] = "TO",
    };

    private static readonly Dictionary<string, string> Capitals = new(StringComparer.Ordinal)
    {
        ["AC"] = "Rio Branco", ["AL"] = "Maceió", ["AP"] = "Macapá", ["AM"] = "Manaus",
        ["BA"] = "Salvador", ["CE"] = "Fortaleza", ["DF"] = "Brasília", ["ES"] = "Vitória",
        ["GO"] = "Goiânia", ["MA"] = "São Luís", ["MT"] = "Cuiabá", ["MS"] = "Campo Grande",
        ["MG"] = "Belo Horizonte", ["PA"] = "Belém", ["PB"] = "João Pessoa", ["PR"] = "Curitiba",
        ["PE"] = "Recife", ["PI"] = "Teresina", ["RJ"] = "Rio de Janeiro", ["RN"] = "Natal",
        ["RS"] = "Porto Alegre", ["RO"] = "Porto Velho", ["RR"] = "Boa Vista", ["SC"] = "Florianópolis",
        ["SP"] = "São Paulo", ["SE"] = "Aracaju", ["TO"] = "Palmas",
    };

    /// <summary>Bsoft supplier code of each state's treasury, used when entering ICMS slips.</summary>
    private static readonly Dictionary<string, string> SupplierCodes = new(StringComparer.Ordinal)
    {
        ["AC"] = "3154", ["AL"] = "523", ["AP"] = "3256", ["AM"] = "1946", ["BA"] = "1924",
        ["CE"] = "1702", ["DF"] = "1617", ["ES"] = "1891", ["GO"] = "1620", ["MA"] = "1973",
        ["MT"] = "1783", ["MS"] = "2053", ["MG"] = "80", ["PA"] = "1890", ["PB"] = "1925",
        ["PR"] = "1784", ["PE"] = "1388", ["PI"] = "1535", ["RJ"] = "1389", ["RN"] = "1391",
        ["RS"] = "1409", ["RO"] = "3782", ["RR"] = "1926", ["SC"] = "1569", ["SP"] = "1390",
        ["SE"] = "1927", ["TO"] = "2496",
    };

    /// <summary>
    /// Full state name to its two-letter code. A two-letter input is returned upper-cased and an
    /// unknown name is returned upper-cased as is (the unified, symbolic and grouped flows).
    /// </summary>
    public static string Abbreviation(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "";
        var upper = fullName.ToUpperInvariant().Trim();
        if (upper.Length == 2) return upper;
        return NameToUf.GetValueOrDefault(upper, upper);
    }

    /// <summary>Same lookup, but an unknown value is returned untouched (the single-emission flow).</summary>
    public static string AbbreviationOrSame(string? fullName)
    {
        if (fullName is null) return "";
        return NameToUf.GetValueOrDefault(fullName.ToUpperInvariant().Trim(), fullName);
    }

    /// <summary>Capital city for a state code or full name; empty when unknown.</summary>
    public static string Capital(string? state)
    {
        if (string.IsNullOrEmpty(state)) return "";
        if (Capitals.TryGetValue(state, out var capital)) return capital;
        return NameToUf.TryGetValue(state, out var uf) ? Capitals[uf] : "";
    }

    public static string? SupplierCode(string uf) => SupplierCodes.GetValueOrDefault(uf);
}
