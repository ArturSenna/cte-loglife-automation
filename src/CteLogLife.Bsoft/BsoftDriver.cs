using System.Globalization;
using CteLogLife.Core.Automation;

namespace CteLogLife.Bsoft;

/// <summary>
/// The Bsoft screen sequences, step for step what the Python bot did. Two rules were added: a required
/// control that does not show up stops the run instead of letting the next keystrokes go to whatever is
/// on screen, and a sender, receiver or payer is only confirmed when Bsoft's search found it (when it
/// lists more than one company, the first row is taken).
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

        if (bot.Find("emissao_incluir_f1", M, 10))
            bot.Act("Iniciar a emissão", () =>
            {
                bot.Wait(500);
                bot.Click();
            });

        bot.Step("Abrir a busca do remetente", "remetente_codigo", () => bot.ClickRelative(58, 40));
        SearchCnpj(parties.SenderCnpjs, "remetente");
        bot.Step("Confirmar o remetente", "busca_cliente_confirmar", bot.Click);

        bot.Step("Abrir a busca do destinatário", "destinatario_codigo", () => bot.ClickRelative(56, 36));
        SearchCnpj(parties.ReceiverCnpjs, "destinatário");
        bot.Step("Confirmar o destinatário", "busca_cliente_confirmar", bot.Click);

        switch (parties.Payer)
        {
            case Payer.Sender:
                break;
            case Payer.Receiver:
                bot.Step("Tomador: destinatário", "tomador_resp_pagamento", () =>
                {
                    bot.Click();
                    bot.Press(Key.Down);
                    bot.Enter();
                });
                break;
            default:
                bot.Step("Tomador: lista", "tomador_resp_pagamento", bot.Click);
                bot.Step("Tomador: outros", "tomador_opcao_outros", bot.Click);
                bot.Step("Abrir a busca do tomador", "tomador_codigo", () => bot.ClickRelative(132, 20));
                SearchCnpj([parties.PayerCnpj ?? ""], "tomador", waitBeforePasteMs: 300);
                bot.Step("Confirmar o tomador", "busca_cliente_confirmar", bot.Click);
                break;
        }
    }

    // Bsoft's company search window ("Localização de Clientes e Fornecedores").
    private const string CnpjField = "busca_cliente_campo_cnpj";
    private const string OneResult = "busca_cliente_qtde_1";          // "Qtde.: 1" under the grid
    private const string FirstRow = "busca_cliente_primeira_linha";   // the grid's row marker (▶) on the first row

    /// <summary>From the top left of <see cref="FirstRow"/> to the middle of the first row's first cell.</summary>
    internal const int FirstRowDx = 41, FirstRowDy = 28;

    /// <summary>
    /// Tries each CNPJ in Bsoft's search and stops at the first one that lists anything, with the first
    /// row of the result clicked. One CNPJ can list several registrations (the same company registered
    /// twice): the first one is taken. Nothing is confirmed when no CNPJ finds anything.
    /// </summary>
    /// <param name="waitBeforePasteMs">A pause between clicking into the field and pasting.</param>
    private void SearchCnpj(IReadOnlyList<string> cnpjs, string role, int waitBeforePasteMs = 0)
    {
        var rowsBefore = false;
        foreach (var cnpj in cnpjs)
        {
            // Rows already in the grid before the CNPJ is typed did not come from it.
            bot.Require(CnpjField);
            rowsBefore = bot.Find(FirstRow, M, 0);

            bot.Step($"CNPJ do {role}", CnpjField, () =>
            {
                bot.DoubleClickRelative(13, 24);
                if (waitBeforePasteMs > 0) bot.Wait(waitBeforePasteMs);
                bot.PasteText(cnpj);
            }, redo: true);
            if (SelectFirstResult(rowsBefore, 1500)) return;
        }

        // A slow search gets a few more seconds; without a result nothing is confirmed.
        if (SelectFirstResult(rowsBefore, 6000)) return;
        throw new ElementNotFoundException(OneResult,
            $"O Bsoft não localizou o {role} ({string.Join(", ", cnpjs)}). Confira o cadastro.");
    }

    /// <summary>
    /// Waits for the search to answer and clicks the first row it lists. True when it found anything.
    /// </summary>
    /// <param name="rowsBefore">
    /// The grid had rows before the CNPJ was typed. They may still be the old ones, so only "Qtde.: 1"
    /// shows that this search answered (the only result the bot accepted before).
    /// </param>
    private bool SelectFirstResult(bool rowsBefore, int waitMs)
    {
        // Whichever comes first: "Qtde.: 1", or a first row without it (two or more results).
        var one = rowsBefore
            ? bot.Find(OneResult, M, waitMs)
            : bot.FindBefore(OneResult, FirstRow, waitMs, waitMs);
        if (rowsBefore && !one) return false;

        // Bsoft already has the first row selected; the click makes sure of it. A single result whose
        // row is not recognised is confirmed as it is, as before.
        if (!bot.Find(FirstRow, M, one ? 500 : 0)) return one;
        bot.Act("Selecionar o primeiro resultado da busca", () => bot.ClickRelative(FirstRowDx, FirstRowDy));
        return true;
    }

    // ------------------------------------------------------------------ emission, tab 3

    public void FillCargo(CargoInput cargo)
    {
        bot.Step("Aba da carga", "carga_aba_parte3", bot.Click);
        bot.Step("Incluir a natureza da carga", "carga_incluir", bot.Click);

        if (bot.Find("carga_modelo_nfe", M, 1000))
            bot.Act("Tipo de documento", () =>
            {
                bot.Click();
                bot.Press(Key.Up);
                bot.Enter();
            });

        // "carga_campo_natureza" and "carga_campo_peso" are the field labels: the inputs below them may still hold the previous
        // emission's values. Click into the field under the label and select all before pasting.
        bot.Step("Natureza da carga", "carga_campo_natureza", () =>
        {
            ClickBelowLast();
            bot.SelectAll();
            bot.PasteText("MATERIAL BIOLÓGICO");
            bot.Enter();
        }, redo: true);

        bot.Step("Pesos e volumes", "carga_campo_peso", () =>
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

        bot.Step("Confirmar a natureza da carga", "carga_confirmar", bot.Click);
    }

    public void SelectOriginalCte(string originalCteNumber)
    {
        if (bot.Find("emissao_incluir_f1", M, 10))
            bot.Act("Iniciar a emissão", () =>
            {
                bot.Wait(500);
                bot.Click();
            });

        bot.Step("Tipo de emissão: complementar", "complementar_finalidade_emissao", () =>
        {
            bot.ClickRelative(100, 30);
            bot.Press(Key.Down);
            bot.Enter();
        }, waitMs: 60000);

        bot.Step("Aba da carga", "carga_aba_parte3", bot.Click, waitMs: 60000);
        bot.Step("Incluir o CT-e original", "complementar_incluir_cte", bot.Click, waitMs: 60000);
        bot.Step("Abrir a busca do CT-e original", "complementar_abrir_busca", () => bot.ClickRelative(-37, 24), waitMs: 60000);

        bot.Step("Período da busca", "complementar_busca_periodo", () =>
        {
            bot.ClickRelative(175, 43);
            bot.Press(Key.Up);
            bot.Enter();
        }, waitMs: 60000);

        bot.Step("Número do CT-e original", "complementar_busca_numero", () =>
        {
            bot.ClickRelative(66, 35);
            bot.TypeKey(originalCteNumber);
        }, waitMs: 60000);

        bot.Step("Localizar o CT-e original", "complementar_busca_localizar", bot.Click, waitMs: 60000);

        bot.Step("Selecionar o CT-e original", "complementar_busca_marcar_todos", () =>
        {
            bot.Click();
            bot.Wait(300);
        }, waitMs: 60000);

        bot.Step("Confirmar o CT-e original", "complementar_busca_confirmar", () =>
        {
            bot.Click();
            bot.Wait(300);
        }, waitMs: 60000);
    }

    // ------------------------------------------------------------------ emission, tab 4

    public long? FillTaxesAndTransmit(TaxInput tax, Action pointOfNoReturn)
    {
        LastNumberHint = null;

        bot.Step("Aba de valores", "valores_aba_parte4", bot.Click, waitMs: 30000);

        bot.Step("Valor do frete", "valores_frete_valor", () =>
        {
            bot.Click();
            bot.Tab();
            bot.TypeKey(tax.Price);
        });

        if (bot.Find("valores_aba_icms", M, 500)) bot.Act("Abrir o ICMS", bot.Click);

        bot.Step("CST e alíquota do ICMS", "valores_icms_cst", () =>
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

        bot.Step("Aba IBS/CBS", "valores_aba_ibs_cbs", () =>
        {
            bot.Click();
            bot.Wait(200);
        });

        bot.Step("CST do IBS/CBS", "valores_ibs_cbs_cst", () =>
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

        if (bot.Find("valores_aba_observacoes", M, 250)) bot.Act("Aba de observações", bot.Click);
        bot.Step("Observação do ICMS", "valores_campo_observacao", () =>
        {
            bot.Click();
            bot.PasteText(tax.IcmsText);
        }, matching: 0.9);

        if (GnreStates.Contains(tax.Uf))
        {
            bot.Step("Observações do tomador", "valores_obs_contribuinte", bot.RightClick);

            bot.Step("Informação do GNRE", "valores_obs_contribuinte_incluir", () =>
            {
                bot.Click();
                bot.PasteText("GNRE_ICMSST");
                bot.Tab();
                bot.PasteText(tax.TpInfo);
            });

            bot.Step("Confirmar a informação do GNRE", "valores_obs_contribuinte_confirmar", bot.Click);
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
                bot.Require("transmissao_aviso_icms_zero");
                bot.Enter();
            }

            if (tax.Complementary)
            {
                if (bot.FindBefore("transmissao_aviso_valores_alterados", "transmissao_numero_documento", 10000, 70000)) bot.Enter();
                else log.Warn("Elemento 'transmissao_aviso_valores_alterados' não apareceu; seguindo sem confirmar o valor.");
            }

            bot.Require("transmissao_numero_documento", 60000);
            bot.ClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
            bot.Tab();
            bot.Tab();
            bot.Enter();

            if (bot.FindBefore("transmissao_aviso_valor_credito", "transmissao_pergunta_incluir_cte", 3000, 13000))
            {
                bot.Press(Key.Right);
                bot.Enter();
            }

            bot.Require("transmissao_pergunta_incluir_cte");
            bot.Press(Key.Right);
            bot.Enter();

            bot.Require("transmissao_botao_mdfe");
            bot.Press(Key.Right);
            bot.Press(Key.Right);
            bot.Press(Key.Right);
            bot.Enter();

            bot.Require("transmissao_pergunta_gerar_cte");
            bot.Press(Key.Left);
            bot.Enter();

            if (bot.FindBefore("transmissao_aviso_contingencia", "transmissao_impressao_vias", 5000, 65000)) bot.Enter();

            bot.Require("transmissao_impressao_vias", 60000);
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
                if (!bot.Find("transmissao_numero_cte", M, attempt == 1 ? 10000 : 3000))
                {
                    log.Warn("Elemento 'transmissao_numero_cte' não encontrado.");
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
        // The Consultas tab. The bot knows it as it looks when another tab is the open one. After a
        // CT-e that turned out to be cancelled already, or a run that stopped there, Bsoft is still on
        // Consultas: the tab looks different, and there is nothing to click because its search field
        // is already on screen. So the field showing up without the tab ends the wait.
        if (bot.FindBefore("cancelamento_aba_consultas", "cancelamento_campo_numero_cte", 30000, 30000))
            bot.Act("Consultas", () =>
            {
                bot.Click();
                bot.Wait(500);
            });

        bot.Step("Número do CT-e na busca", "cancelamento_campo_numero_cte", () =>
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

        bot.Step("Localizar o CT-e", "cancelamento_localizar", () =>
        {
            bot.Click();
            bot.Wait(200);
        }, waitMs: 5000);

        if (bot.Find("cancelamento_status_autorizado", M, 5000))
        {
            bot.Act("Abrir o CT-e", () =>
            {
                bot.DoubleClick();
                bot.Wait(200);
            });

            if (bot.Find("cancelamento_menu_cte", M, 10000))
                bot.Act("Menu do CT-e", () =>
                {
                    bot.Click();
                    bot.Wait(200);
                });

            bot.Step("Justificativa do cancelamento", "cancelamento_opcao_cancelar", () =>
            {
                bot.Click();
                bot.Wait(200);

                bot.PasteText("TRANSPORTE CANCELADO");
                bot.Wait(200);
            });

            bot.Require("cancelamento_confirmar");
            pointOfNoReturn();
            bot.Click();
            bot.Wait(200);

            // The answer comes from the tax authority and can take a while. Giving up early would report
            // an error for a CT-e that then gets cancelled, and leave its message open on the screen.
            if (!bot.Find("cancelamento_sucesso", M, 60000))
                throw new InvalidOperationException(
                    "A mensagem de cancelamento realizado ('cancelamento_sucesso') não apareceu: confira no Bsoft se o CT-e foi cancelado.");
            bot.Enter();
            return true;
        }

        if (bot.Find("cancelamento_ja_cancelado", M, 5000)) return false;
        throw new InvalidOperationException($"CT-e {cteNumber} não encontrado ou status desconhecido.");
    }

    // ------------------------------------------------------------------ ICMS slip

    public void EnterIcmsSlip(IcmsSlip slip)
    {
        var emission = DateTime.ParseExact(slip.EmissionDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        var changeDates = emission.Date != DateTime.Today;

        bot.Step("Nova guia", "guia_icms_incluir_f1", bot.Click, waitMs: 30000);

        bot.Step("Documento da guia", "guia_icms_documento", () =>
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
            bot.Step("Data de emissão da guia", "guia_icms_data_emissao", () =>
            {
                bot.DoubleClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
                bot.TypeKeys(slip.EmissionDate);
            });

            bot.Step("Data de pagamento e valor da guia", "guia_icms_data_movimento", () =>
            {
                bot.DoubleClickRelative(bot.Last.Width / 2.0, 1.5 * bot.Last.Height);
                bot.TypeKeys(slip.EmissionDate);
                bot.Tab();
                bot.TypeKeys(slip.SlipValue);
            });
        }

        bot.Step("Fornecedor da guia", "guia_icms_codigo_fornecedor", () =>
        {
            bot.ClickAt(bot.Last.Left + bot.Last.Width / 2, bot.Last.Top + bot.Last.Height);
            bot.Wait(200);
            bot.PasteText(slip.SupplierCode);
            bot.Enter();
        });

        bot.Step("Centro de custo da guia", "guia_icms_centro_custo", () =>
        {
            bot.DoubleClickRelative(bot.Last.Width / 2, bot.Last.Height);
            bot.Wait(300);
            bot.TypeLikeAPerson(slip.CostCenter, 80);
            bot.Enter();
        });

        bot.Step("Aba de pagamento da guia", "guia_icms_pre_cadastrada", bot.Click, waitMs: 30000);

        bot.Step("Banco da guia", "guia_icms_local_cobranca", () =>
        {
            ClickBelowLast();
            bot.Wait(200);
            bot.PasteText("BANCO BRADESCO");
            bot.Enter();
        });

        bot.Step("Código de barras da guia", "guia_icms_codigo_barras", () =>
        {
            ClickBelowLast();
            bot.Wait(200);
            bot.PasteText(slip.Barcode);
            bot.Enter();
        });

        bot.Step("Observação da guia", "guia_icms_observacao", () =>
        {
            bot.Click();
            bot.Wait(200);
            bot.PasteText($"REF. A ICMS ST {slip.EmissionDate}");
        });

        bot.Step("Confirmar a guia", "guia_icms_confirmar", () =>
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
