using System.Globalization;
using CteLogLife.Core.Automation;

namespace CteLogLife.Bsoft;

/// <summary>
/// The Bsoft screen sequences, step for step what the Python bot did. Two rules were added: a required
/// control that does not show up stops the run instead of letting the next keystrokes go to whatever is
/// on screen, and a sender, receiver or payer is only confirmed when Bsoft's search found it.
/// </summary>
public sealed class BsoftDriver(DesktopBot bot, IRunLog log) : IBsoftDriver, IDisposable
{
    private const double M = 0.97;
    private const int CopyAttempts = 5;
    private const int CopyWaitMs = 2500;

    /// <summary>States whose CT-e carries the GNRE note in the payer observations.</summary>
    private static readonly HashSet<string> GnreStates =
        ["AL", "AP", "GO", "MS", "MT", "PA", "PE", "PI", "PR", "RO", "RR", "RS", "SC", "TO"];

    public string? LastNumberHint { get; private set; }

    public void Dispose() => bot.Dispose();

    // ------------------------------------------------------------------ emission, tab 1

    public void FillParties(PartiesInput parties)
    {
        // Start from an empty clipboard. Not fatal if another program has it open.
        try
        {
            ClipboardText.Clear();
        }
        catch (ClipboardBusyException ex)
        {
            log.Warn($"Não foi possível limpar a área de transferência: {ex.Message}");
        }

        if (bot.Find("iniciarEmissao", M, 10))
        {
            bot.Wait(500);
            bot.Click();
        }

        bot.Require("searchSender");
        bot.ClickRelative(58, 40);
        SearchCnpj(parties.SenderCnpjs, "remetente");
        bot.Require("confirmCNPJ");
        bot.Click();

        bot.Require("searchReceiver");
        bot.ClickRelative(56, 36);
        SearchCnpj(parties.ReceiverCnpjs, "destinatário");
        bot.Require("confirmCNPJ");
        bot.Click();

        switch (parties.Payer)
        {
            case Payer.Sender:
                break;
            case Payer.Receiver:
                bot.Require("Tomador1");
                bot.Click();
                bot.Press(Key.Down);
                bot.Enter();
                break;
            default:
                bot.Require("Tomador1");
                bot.Click();
                bot.Require("Others");
                bot.Click();
                bot.Require("searchPayer");
                bot.ClickRelative(132, 20);
                bot.Require("CNPJ_input");
                bot.DoubleClickRelative(13, 24);
                bot.Wait(300);
                bot.PasteText(parties.PayerCnpj ?? "");
                RequireSearchResult([parties.PayerCnpj ?? ""], "tomador");
                bot.Require("confirmCNPJ");
                bot.Click();
                break;
        }
    }

    /// <summary>Tries each CNPJ in Bsoft's search and stops at the first one it finds.</summary>
    private void SearchCnpj(IReadOnlyList<string> cnpjs, string role)
    {
        foreach (var cnpj in cnpjs)
        {
            bot.Require("CNPJ_input");
            bot.DoubleClickRelative(13, 24);
            bot.PasteText(cnpj);
            if (bot.Find("Found1", M, 1500)) return;
        }
        RequireSearchResult(cnpjs, role);
    }

    /// <summary>Gives a slow search a few more seconds; without a result nothing is confirmed.</summary>
    private void RequireSearchResult(IReadOnlyList<string> cnpjs, string role)
    {
        if (bot.Find("Found1", M, 1500)) return;
        if (bot.Find("Found1", M, 6000)) return;
        throw new ElementNotFoundException("Found1",
            $"O Bsoft não localizou o {role} ({string.Join(", ", cnpjs)}). Confira o cadastro.");
    }

    // ------------------------------------------------------------------ emission, tab 3

    public void FillCargo(CargoInput cargo)
    {
        bot.Require("Part3");
        bot.Click();

        bot.Require("IncludeNat");
        bot.Click();

        if (bot.Find("NF-e", M, 1000))
        {
            bot.Click();
            bot.Press(Key.Up);
            bot.Enter();
        }

        // "Nat" and "Weight1" are the field labels: the inputs below them may still hold the previous
        // emission's values. Click into the field under the label and select all before pasting.
        bot.Require("Nat");
        ClickBelowLast();
        bot.SelectAll();
        bot.PasteText("MATERIAL BIOLÓGICO");
        bot.Enter();

        bot.Require("Weight1");
        ClickBelowLast();
        if (cargo.CteInstance is null or 1)
        {
            var volumes = int.Parse(cargo.Volumes, CultureInfo.InvariantCulture);
            var weight = (volumes * 2).ToString(CultureInfo.InvariantCulture);
            bot.SelectAll();
            bot.PasteText(weight); // Peso
            bot.Tab();
            bot.SelectAll();
            bot.PasteText(weight); // Peso aferido
            bot.Tab();
            bot.SelectAll();
            bot.PasteText(weight); // Cubagem
            bot.Tab();
            bot.Tab(500);
            bot.SelectAll();
            bot.TypeKeys(cargo.Volumes); // Volumes
        }

        bot.Require("confirmNAT");
        bot.Click();
    }

    public void SelectOriginalCte(string originalCteNumber)
    {
        if (bot.Find("iniciarEmissao", M, 10))
        {
            bot.Wait(500);
            bot.Click();
        }

        bot.Require("emissionComp", 60000);
        bot.ClickRelative(100, 30);
        bot.Press(Key.Down);
        bot.Enter();

        bot.Require("Part3", 60000);
        bot.Click();

        bot.Require("includeCTe", 60000);
        bot.Click();

        bot.Require("searchCTe", 60000);
        bot.ClickRelative(-37, 24);

        bot.Require("clearDate", 60000);
        bot.ClickRelative(175, 43);
        bot.Press(Key.Up);
        bot.Enter();

        bot.Require("inputCTE", 60000);
        bot.ClickRelative(66, 35);
        bot.TypeKey(originalCteNumber);

        bot.Require("findCTe", 60000);
        bot.Click();

        bot.Require("selectAll", 60000);
        bot.Click();
        bot.Wait(300);

        bot.Require("confirmCTE", 60000);
        bot.Click();
        bot.Wait(300);
    }

    // ------------------------------------------------------------------ emission, tab 4

    public long? FillTaxesAndTransmit(TaxInput tax, Action pointOfNoReturn)
    {
        LastNumberHint = null;

        bot.Require("part4", 30000);
        bot.Click();

        bot.Require("valueShipping22");
        bot.Click();
        bot.Tab();
        bot.TypeKey(tax.Price);

        if (bot.Find("icms", M, 500)) bot.Click();

        bot.Require("CST");
        bot.ClickRelative(9, 8);
        bot.Tab();

        if (tax.Uf == "MG")
        {
            bot.TypeKey("00");
            bot.Tab();
            bot.Tab();
        }
        else
        {
            bot.TypeKey("90");
            bot.Press(Key.Down);
            bot.Enter();
            bot.Tab();
            bot.Tab();
            bot.TypeKey("0");
            bot.Tab();
        }
        if (tax.Complementary) bot.Tab();

        bot.TypeKey(tax.Tax);
        bot.Tab();
        if (tax.Complementary) bot.Tab();
        bot.Enter();

        bot.Require("ibs-cbs");
        bot.Click();
        bot.Wait(200);

        bot.Require("ibs-cst");
        ClickBelowLast();
        bot.Click();
        bot.TypeKeys("000");
        bot.Enter();
        bot.TypeKeys("0");
        bot.Enter();
        bot.Tab();
        bot.Enter();

        if (bot.Find("Obs2", M, 250)) bot.Click();
        bot.Require("Obs3", 10000, matching: 0.9);
        bot.Click();
        bot.PasteText(tax.IcmsText);

        if (GnreStates.Contains(tax.Uf))
        {
            bot.Require("payerObs");
            bot.RightClick();

            bot.Require("include_tp_info");
            bot.Click();
            bot.PasteText("GNRE_ICMSST");
            bot.Tab();
            bot.PasteText(tax.TpInfo);

            bot.Require("confirm_tp_info");
            bot.Click();
        }

        // From here the CT-e is being saved and transmitted: no pause, no stop, and a step that
        // fails is reported as "may have been authorized" instead of typing on.
        pointOfNoReturn();
        try
        {
            bot.Press(Key.F4);

            if (tax.Uf != "MG")
            {
                bot.Require("zeroValue");
                bot.Enter();
            }

            if (tax.Complementary)
            {
                if (bot.Find("confirmValue", M, 10000)) bot.Enter();
                else log.Warn("Elemento 'confirmValue' não apareceu; seguindo sem confirmar o valor.");
            }

            bot.Require("finalizeEmission", 60000);
            bot.ClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
            bot.Tab();
            bot.Tab();
            bot.Enter();

            if (bot.Find("creditconfirmPopUp", M, 3000))
            {
                bot.Press(Key.Right);
                bot.Enter();
            }

            bot.Require("secondPopUp");
            bot.Press(Key.Right);
            bot.Enter();

            bot.Require("thirdPopUp");
            bot.Press(Key.Right);
            bot.Press(Key.Right);
            bot.Press(Key.Right);
            bot.Enter();

            bot.Require("fourthPopUp");
            bot.Press(Key.Left);
            bot.Enter();

            if (bot.Find("contingencia", M, 5000)) bot.Enter();

            bot.Require("fifthPopUp", 60000);
            bot.Press(Key.F10);
            bot.Wait(2000);
        }
        catch (ElementNotFoundException ex)
        {
            throw new TransmitUncertainException(
                $"O elemento '{ex.Label}' não apareceu depois do F4, e a automação parou sem digitar mais nada.", ex);
        }

        // The CT-e is authorized at this point. Read its number, with retries (null = could not be read).
        var number = CopyCteNumber();
        bot.Wait(2000);
        return number;
    }

    /// <summary>
    /// Copies the number shown in the "Nº CT-e" field. It runs right after the authorization, so it
    /// never throws: the caller asks the operator when this returns null.
    /// </summary>
    private long? CopyCteNumber()
    {
        for (var attempt = 1; attempt <= CopyAttempts; attempt++)
        {
            try
            {
                if (!bot.Find("clicarCte", M, attempt == 1 ? 10000 : 3000))
                {
                    log.Warn("Elemento 'clicarCte' não encontrado.");
                    continue;
                }
                // Clear first: only a value that shows up after this point can be the number.
                ClipboardText.Clear();
                bot.DoubleClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
                bot.Control('c');
                if (WaitClipboardNumber(CopyWaitMs) is { } number) return number;
                log.Warn($"Número do CT-e não copiado (tentativa {attempt}/{CopyAttempts}).");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Warn($"Falha ao copiar o número do CT-e (tentativa {attempt}/{CopyAttempts}): {ex.Message}");
            }
            Thread.Sleep(500 * attempt);
        }

        // Last resort without the clipboard: what the focused field holds becomes a suggestion for
        // the operator, never a value used on its own.
        try
        {
            var focused = DesktopBot.FocusedControlText();
            if (!string.IsNullOrEmpty(focused) && focused.All(char.IsAsciiDigit)) LastNumberHint = focused;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Warn($"Leitura direta do campo Nº CT-e falhou: {ex.Message}");
        }
        return null;
    }

    private static long? WaitClipboardNumber(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            var text = (ClipboardText.TryGet() ?? "").Trim();
            if (text.Length > 0 && text.All(char.IsAsciiDigit) &&
                long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return number;
            if (DateTime.UtcNow >= deadline) return null;
            Thread.Sleep(100);
        }
    }

    // ------------------------------------------------------------------ cancellation

    public bool CancelCte(string cteNumber, Action pointOfNoReturn)
    {
        bot.Require("2consultas", 30000);
        bot.Click();
        bot.Wait(500);

        // Clear the search field.
        bot.Require("10ClicarBordaInferior", 5000);
        var field = bot.Last;
        bot.ClickAt(field.Left + (int)(field.Width * 0.5), field.Top + field.Height - 1);
        bot.Wait(500);
        bot.SelectAll();
        bot.Wait(300);
        bot.Press(Key.Delete);
        bot.Wait(100);

        bot.TypeKeys(cteNumber);
        bot.Wait(200);

        bot.Require("4Localizar", 5000);
        bot.Click();
        bot.Wait(200);

        if (bot.Find("5duploClickStatus", M, 5000))
        {
            bot.DoubleClick();
            bot.Wait(200);

            if (bot.Find("5.1ct-e", M, 10000))
            {
                bot.Click();
                bot.Wait(200);
            }

            bot.Require("5.2Cancelar_CTE");
            bot.Click();
            bot.Wait(200);

            bot.PasteText("TRANSPORTE CANCELADO");
            bot.Wait(200);

            bot.Require("8confirmar");
            pointOfNoReturn();
            bot.Click();
            bot.Wait(200);

            if (!bot.Find("9-sucesso", M, 20000))
                throw new InvalidOperationException(
                    "Pop-up '9-sucesso' não encontrado: confira no Bsoft se o CT-e foi cancelado.");
            bot.Enter();
            return true;
        }

        if (bot.Find("cancelled", M, 5000)) return false;
        throw new InvalidOperationException($"CT-e {cteNumber} não encontrado ou status desconhecido.");
    }

    // ------------------------------------------------------------------ ICMS slip

    public void EnterIcmsSlip(IcmsSlip slip)
    {
        var emission = DateTime.ParseExact(slip.EmissionDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        var changeDates = emission.Date != DateTime.Today;

        bot.Require("icms_start", 30000);
        bot.Click();

        bot.Require("icms_document");
        ClickBelowLast();
        bot.Wait(200);
        bot.TypeKeys(slip.CteNumber);
        bot.Wait(500);

        if (changeDates)
        {
            bot.Require("icms_emission_date");
            bot.DoubleClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
            bot.TypeKeys(slip.EmissionDate);

            bot.Require("icms_payment_date");
            bot.DoubleClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
            bot.TypeKeys(slip.EmissionDate);
        }
        else
        {
            bot.Tab();
            bot.Tab();
        }

        bot.Tab();
        bot.TypeKeys(slip.SlipValue);

        bot.Require("icms_state");
        bot.ClickAt(bot.Last.Left + bot.Last.Width / 2, bot.Last.Top + bot.Last.Height);
        bot.Wait(200);
        bot.PasteText(slip.SupplierCode);
        bot.Enter();

        bot.Require("icms_cost_center");
        bot.DoubleClickRelative(bot.Last.Width / 2, bot.Last.Height);
        bot.Wait(300);
        bot.TypeLikeAPerson(slip.CostCenter, 80);
        bot.Enter();

        bot.Require("icms_payment", 30000);
        bot.Click();

        bot.Require("icms_bank");
        ClickBelowLast();
        bot.Wait(200);
        bot.PasteText("BANCO BRADESCO");
        bot.Enter();

        bot.Require("icms_barcode");
        ClickBelowLast();
        bot.Wait(200);
        bot.PasteText(slip.Barcode);
        bot.Enter();

        bot.Require("icms_obs");
        bot.Click();
        bot.Wait(200);
        bot.PasteText($"REF. A ICMS ST {slip.EmissionDate}");

        bot.Require("icms_confirm", 30000);
        bot.Click();
        bot.Wait(500);
    }

    /// <summary>Clicks the input that sits right under the label found last.</summary>
    private void ClickBelowLast()
    {
        var label = bot.Last;
        bot.ClickAt(label.Left + label.Width / 2, label.Top + 1.5 * label.Height);
    }
}
