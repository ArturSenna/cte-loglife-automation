namespace CteLogLife.Core.Automation;

/// <summary>Who pays the freight, as Bsoft's "Tomador" field offers it.</summary>
public enum Payer { Sender, Receiver, Other }

/// <summary>Sender, receiver and payer of a CT-e. With several CNPJs the first one Bsoft finds is used.</summary>
public sealed record PartiesInput(
    IReadOnlyList<string> SenderCnpjs,
    IReadOnlyList<string> ReceiverCnpjs,
    Payer Payer,
    string? PayerCnpj = null)
{
    /// <summary>Maps the API's "Remetente" / "Destinatário" / anything else.</summary>
    public static Payer ParsePayer(string? text) => text switch
    {
        "Remetente" => Payer.Sender,
        "Destinatário" => Payer.Receiver,
        _ => Payer.Other,
    };
}

/// <summary>
/// Cargo of a CT-e. Weights are typed only when <see cref="CteInstance"/> is null or 1, as twice the
/// number of volumes.
/// </summary>
public sealed record CargoInput(string Volumes = "1", int? CteInstance = null);

/// <summary>The values of the fourth emission tab.</summary>
/// <param name="Price">Freight value, typed as is.</param>
/// <param name="Tax">ICMS rate, typed as is.</param>
/// <param name="TpInfo">ICMS amount for the GNRE note of the states that require it.</param>
/// <param name="Uf">UF of the sender.</param>
/// <param name="IcmsText">Observation text.</param>
public sealed record TaxInput(string Price, string Tax, string TpInfo, string Uf, string IcmsText,
    bool Complementary = false);

public sealed record IcmsSlip(string CteNumber, string SlipValue, string SupplierCode, string CostCenter,
    string Barcode, string EmissionDate);

/// <summary>A required Bsoft screen element did not show up in time, before anything was transmitted.</summary>
public class ElementNotFoundException(string label, string? detail = null)
    : Exception($"Elemento '{label}' não encontrado na tela do Bsoft.{(detail is null ? "" : " " + detail)}")
{
    public string Label { get; } = label;
}

/// <summary>
/// Something went wrong after the point of no return: the CT-e may or may not have been authorized,
/// so only the operator can say what happened.
/// </summary>
public sealed class TransmitUncertainException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// What the automation does in Bsoft, one method per screen. The production implementation drives the
/// real UI; tests use a fake.
/// </summary>
public interface IBsoftDriver
{
    /// <summary>First emission tab: sender, receiver and payer.</summary>
    void FillParties(PartiesInput parties);

    /// <summary>Third tab of a normal CT-e: nature of the cargo, weights and volumes.</summary>
    void FillCargo(CargoInput cargo);

    /// <summary>Starts a complementary CT-e and links it to the original one.</summary>
    void SelectOriginalCte(string originalCteNumber);

    /// <summary>
    /// Fourth tab: values and taxes, then saves and transmits. <paramref name="pointOfNoReturn"/> is
    /// called right before the key that saves the CT-e. Returns the authorized number, or null when it
    /// was authorized but the number could not be read.
    /// </summary>
    /// <exception cref="TransmitUncertainException">A step failed after the point of no return.</exception>
    long? FillTaxesAndTransmit(TaxInput tax, Action pointOfNoReturn);

    /// <summary>A number read from the screen that looks like the CT-e number but could not be confirmed.</summary>
    string? LastNumberHint { get; }

    /// <summary>
    /// Cancels one CT-e. <paramref name="pointOfNoReturn"/> is called right before the cancellation is
    /// confirmed. Returns false when Bsoft shows the CT-e as already cancelled.
    /// </summary>
    bool CancelCte(string cteNumber, Action pointOfNoReturn);

    void EnterIcmsSlip(IcmsSlip slip);
}

public interface IRunLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

/// <summary>The questions and notices a run needs from the person at the screen.</summary>
public interface IOperator
{
    /// <summary>Asks for a line of text on top of every window. Null when cancelled.</summary>
    string? AskText(string title, string message, string? prefill = null);

    /// <summary>Something the operator should read, shown without stopping the bot.</summary>
    void Notice(string message);
}
