using System.Globalization;
using CteLogLife.Core.Automation;

namespace CteLogLife.Bsoft;

/// <summary>
/// The Bsoft screen sequences, step for step what the Python bot did. Two rules were added: a required
/// control that does not show up stops the run instead of letting the next keystrokes go to whatever is
/// on screen, and a sender, receiver or payer is only confirmed when Bsoft's search found it.
///
/// Everything before the point of no return is written as steps (<see cref="DesktopBot.Step"/>): one
/// screen element and what is done on it. A pause never starts inside a step. The steps marked
/// <c>redo</c> click into their field and replace its contents, so they can be done again from the
/// start; they are the ones the bot repeats when Bsoft was used during a pause.
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
            bot.ClearClipboard();
        }
        catch (ClipboardBusyException ex)
        {
            log.Warn($"Não foi possível limpar a área de transferência: {ex.Message}");
        }

        if (bot.Find("iniciarEmissao", M, 10))
            bot.Act("Iniciar a emissão", () =>
            {
                bot.Wait(500);
                bot.Click();
            });

        bot.Step("Abrir a busca do remetente", "searchSender", () => bot.ClickRelative(58, 40));
        SearchCnpj(parties.SenderCnpjs, "remetente");
        bot.Step("Confirmar o remetente", "confirmCNPJ", bot.Click);

        bot.Step("Abrir a busca do destinatário", "searchReceiver", () => bot.ClickRelative(56, 36));
        SearchCnpj(parties.ReceiverCnpjs, "destinatário");
        bot.Step("Confirmar o destinatário", "confirmCNPJ", bot.Click);

        switch (parties.Payer)
        {
            case Payer.Sender:
                break;
            case Payer.Receiver:
                bot.Step("Tomador: destinatário", "Tomador1", () =>
                {
                    bot.Click();
                    bot.Press(Key.Down);
                    bot.Enter();
                });
                break;
            default:
                bot.Step("Tomador: lista", "Tomador1", bot.Click);
                bot.Step("Tomador: outros", "Others", bot.Click);
                bot.Step("Abrir a busca do tomador", "searchPayer", () => bot.ClickRelative(132, 20));
                bot.Step("CNPJ do tomador", "CNPJ_input", () =>
                {
                    bot.DoubleClickRelative(13, 24);
                    bot.Wait(300);
                    bot.PasteText(parties.PayerCnpj ?? "");
                }, redo: true);
                RequireSearchResult([parties.PayerCnpj ?? ""], "tomador");
                bot.Step("Confirmar o tomador", "confirmCNPJ", bot.Click);
                break;
        }
    }

    /// <summary>Tries each CNPJ in Bsoft's search and stops at the first one it finds.</summary>
    private void SearchCnpj(IReadOnlyList<string> cnpjs, string role)
    {
        foreach (var cnpj in cnpjs)
        {
            bot.Step($"CNPJ do {role}", "CNPJ_input", () =>
            {
                bot.DoubleClickRelative(13, 24);
                bot.PasteText(cnpj);
            }, redo: true);
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
        bot.Step("Aba da carga", "Part3", bot.Click);
        bot.Step("Incluir a natureza da carga", "IncludeNat", bot.Click);

        if (bot.Find("NF-e", M, 1000))
            bot.Act("Tipo de documento", () =>
            {
                bot.Click();
                bot.Press(Key.Up);
                bot.Enter();
            });

        // "Nat" and "Weight1" are the field labels: the inputs below them may still hold the previous
        // emission's values. Click into the field under the label and select all before pasting.
        bot.Step("Natureza da carga", "Nat", () =>
        {
            ClickBelowLast();
            bot.SelectAll();
            bot.PasteText("MATERIAL BIOLÓGICO");
            bot.Enter();
        }, redo: true);

        bot.Step("Pesos e volumes", "Weight1", () =>
        {
            ClickBelowLast();
            if (cargo.CteInstance is not (null or 1)) return;
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
        }, redo: true);

        bot.Step("Confirmar a natureza da carga", "confirmNAT", bot.Click);
    }

    public void SelectOriginalCte(string originalCteNumber)
    {
        if (bot.Find("iniciarEmissao", M, 10))
            bot.Act("Iniciar a emissão", () =>
            {
                bot.Wait(500);
                bot.Click();
            });

        bot.Step("Tipo de emissão: complementar", "emissionComp", () =>
        {
            bot.ClickRelative(100, 30);
            bot.Press(Key.Down);
            bot.Enter();
        }, waitMs: 60000);

        bot.Step("Aba da carga", "Part3", bot.Click, waitMs: 60000);
        bot.Step("Incluir o CT-e original", "includeCTe", bot.Click, waitMs: 60000);
        bot.Step("Abrir a busca do CT-e original", "searchCTe", () => bot.ClickRelative(-37, 24), waitMs: 60000);

        bot.Step("Período da busca", "clearDate", () =>
        {
            bot.ClickRelative(175, 43);
            bot.Press(Key.Up);
            bot.Enter();
        }, waitMs: 60000);

        bot.Step("Número do CT-e original", "inputCTE", () =>
        {
            bot.ClickRelative(66, 35);
            bot.TypeKey(originalCteNumber);
        }, waitMs: 60000);

        bot.Step("Localizar o CT-e original", "findCTe", bot.Click, waitMs: 60000);

        bot.Step("Selecionar o CT-e original", "selectAll", () =>
        {
            bot.Click();
            bot.Wait(300);
        }, waitMs: 60000);

        bot.Step("Confirmar o CT-e original", "confirmCTE", () =>
        {
            bot.Click();
            bot.Wait(300);
        }, waitMs: 60000);
    }

    // ------------------------------------------------------------------ emission, tab 4

    public long? FillTaxesAndTransmit(TaxInput tax, Action pointOfNoReturn)
    {
        LastNumberHint = null;

        bot.Step("Aba de valores", "part4", bot.Click, waitMs: 30000);

        bot.Step("Valor do frete", "valueShipping22", () =>
        {
            bot.Click();
            bot.Tab();
            bot.TypeKey(tax.Price);
        });

        if (bot.Find("icms", M, 500)) bot.Act("Abrir o ICMS", bot.Click);

        bot.Step("CST e alíquota do ICMS", "CST", () =>
        {
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
        });

        bot.Step("Aba IBS/CBS", "ibs-cbs", () =>
        {
            bot.Click();
            bot.Wait(200);
        });

        bot.Step("CST do IBS/CBS", "ibs-cst", () =>
        {
            ClickBelowLast();
            bot.Click();
            bot.TypeKeys("000");
            bot.Enter();
            bot.TypeKeys("0");
            bot.Enter();
            bot.Tab();
            bot.Enter();
        });

        if (bot.Find("Obs2", M, 250)) bot.Act("Aba de observações", bot.Click);
        bot.Step("Observação do ICMS", "Obs3", () =>
        {
            bot.Click();
            bot.PasteText(tax.IcmsText);
        }, matching: 0.9);

        if (GnreStates.Contains(tax.Uf))
        {
            bot.Step("Observações do tomador", "payerObs", bot.RightClick);

            bot.Step("Informação do GNRE", "include_tp_info", () =>
            {
                bot.Click();
                bot.PasteText("GNRE_ICMSST");
                bot.Tab();
                bot.PasteText(tax.TpInfo);
            });

            bot.Step("Confirmar a informação do GNRE", "confirm_tp_info", bot.Click);
        }

        // The last place where a pause can start: after it the CT-e is sent.
        bot.Checkpoint();

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
                if (bot.FindBefore("confirmValue", "finalizeEmission", 10000, 70000)) bot.Enter();
                else log.Warn("Elemento 'confirmValue' não apareceu; seguindo sem confirmar o valor.");
            }

            bot.Require("finalizeEmission", 60000);
            bot.ClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
            bot.Tab();
            bot.Tab();
            bot.Enter();

            if (bot.FindBefore("creditconfirmPopUp", "secondPopUp", 3000, 13000))
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

            if (bot.FindBefore("contingencia", "fifthPopUp", 5000, 65000)) bot.Enter();

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
                bot.ClearClipboard();
                bot.DoubleClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
                bot.Control('c');
                if (WaitClipboardNumber(CopyWaitMs) is { } number) return number;
                log.Warn($"Número do CT-e não copiado (tentativa {attempt}/{CopyAttempts}).");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Warn($"Falha ao copiar o número do CT-e (tentativa {attempt}/{CopyAttempts}): {ex.Message}");
            }
            bot.Sleep(500 * attempt);
        }

        // Last resort without the clipboard: what the focused field holds becomes a suggestion for
        // the operator, never a value used on its own.
        try
        {
            var focused = bot.FocusedText();
            if (!string.IsNullOrEmpty(focused) && focused.All(char.IsAsciiDigit)) LastNumberHint = focused;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Warn($"Leitura direta do campo Nº CT-e falhou: {ex.Message}");
        }
        return null;
    }

    private long? WaitClipboardNumber(int timeoutMs)
    {
        var deadline = bot.Now + timeoutMs;
        while (true)
        {
            var text = (bot.ClipboardNow() ?? "").Trim();
            if (text.Length > 0 && text.All(char.IsAsciiDigit) &&
                long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return number;
            if (bot.Now >= deadline) return null;
            bot.Sleep(100);
        }
    }

    // ------------------------------------------------------------------ cancellation

    public bool CancelCte(string cteNumber, Action pointOfNoReturn)
    {
        bot.Step("Consultas", "2consultas", () =>
        {
            bot.Click();
            bot.Wait(500);
        }, waitMs: 30000);

        bot.Step("Número do CT-e na busca", "10ClicarBordaInferior", () =>
        {
            // Clear the search field, then type the number.
            var field = bot.Last;
            bot.ClickAt(field.Left + (int)(field.Width * 0.5), field.Top + field.Height - 1);
            bot.Wait(500);
            bot.SelectAll();
            bot.Wait(300);
            bot.Press(Key.Delete);
            bot.Wait(100);

            bot.TypeKeys(cteNumber);
            bot.Wait(200);
        }, redo: true, waitMs: 5000);

        bot.Step("Localizar o CT-e", "4Localizar", () =>
        {
            bot.Click();
            bot.Wait(200);
        }, waitMs: 5000);

        if (bot.Find("5duploClickStatus", M, 5000))
        {
            bot.Act("Abrir o CT-e", () =>
            {
                bot.DoubleClick();
                bot.Wait(200);
            });

            if (bot.Find("5.1ct-e", M, 10000))
                bot.Act("Menu do CT-e", () =>
                {
                    bot.Click();
                    bot.Wait(200);
                });

            bot.Step("Justificativa do cancelamento", "5.2Cancelar_CTE", () =>
            {
                bot.Click();
                bot.Wait(200);

                bot.PasteText("TRANSPORTE CANCELADO");
                bot.Wait(200);
            });

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

        bot.Step("Nova guia", "icms_start", bot.Click, waitMs: 30000);

        bot.Step("Documento da guia", "icms_document", () =>
        {
            ClickBelowLast();
            bot.Wait(200);
            bot.TypeKeys(slip.CteNumber);
            bot.Wait(500);
            if (changeDates) return;

            // Today's dates are already there: skip them and type the value.
            bot.Tab();
            bot.Tab();
            bot.Tab();
            bot.TypeKeys(slip.SlipValue);
        });

        if (changeDates)
        {
            bot.Step("Data de emissão da guia", "icms_emission_date", () =>
            {
                bot.DoubleClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
                bot.TypeKeys(slip.EmissionDate);
            });

            bot.Step("Data de pagamento e valor da guia", "icms_payment_date", () =>
            {
                bot.DoubleClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
                bot.TypeKeys(slip.EmissionDate);
                bot.Tab();
                bot.TypeKeys(slip.SlipValue);
            });
        }

        bot.Step("Fornecedor da guia", "icms_state", () =>
        {
            bot.ClickAt(bot.Last.Left + bot.Last.Width / 2, bot.Last.Top + bot.Last.Height);
            bot.Wait(200);
            bot.PasteText(slip.SupplierCode);
            bot.Enter();
        });

        bot.Step("Centro de custo da guia", "icms_cost_center", () =>
        {
            bot.DoubleClickRelative(bot.Last.Width / 2, bot.Last.Height);
            bot.Wait(300);
            bot.TypeLikeAPerson(slip.CostCenter, 80);
            bot.Enter();
        });

        bot.Step("Aba de pagamento da guia", "icms_payment", bot.Click, waitMs: 30000);

        bot.Step("Banco da guia", "icms_bank", () =>
        {
            ClickBelowLast();
            bot.Wait(200);
            bot.PasteText("BANCO BRADESCO");
            bot.Enter();
        });

        bot.Step("Código de barras da guia", "icms_barcode", () =>
        {
            ClickBelowLast();
            bot.Wait(200);
            bot.PasteText(slip.Barcode);
            bot.Enter();
        });

        bot.Step("Observação da guia", "icms_obs", () =>
        {
            bot.Click();
            bot.Wait(200);
            bot.PasteText($"REF. A ICMS ST {slip.EmissionDate}");
        });

        bot.Step("Confirmar a guia", "icms_confirm", () =>
        {
            bot.Click();
            bot.Wait(500);
        }, waitMs: 30000);
    }

    /// <summary>Clicks the input that sits right under the label found last.</summary>
    private void ClickBelowLast()
    {
        var label = bot.Last;
        bot.ClickAt(label.Left + label.Width / 2, label.Top + 1.5 * label.Height);
    }
}
