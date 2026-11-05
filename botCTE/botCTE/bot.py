from botcity.core import DesktopBot
from ctypes import windll
import time
from datetime import datetime, date
import pyautogui


# Uncomment the line below for integrations with BotMaestro
# Using the Maestro SDK
# from botcity.maestro import *

class Bot(DesktopBot):

    @staticmethod
    def not_found(label):
        print(f"Element not found: {label}")

    def paste_text(self, text):
        self.copy_to_clipboard(str(text))
        self.paste()

    def type_text_char_by_char(self, text, wait_time=80):
        pyautogui.write(str(text), interval=wait_time / 1000)

    # ------------------------------------------------------------------
    # Image lookup with "-alt" fallback
    #
    # The app layout shrank horizontally, so some elements no longer match
    # their original screenshot. Any label may have alternative images
    # named "<label>-alt.png", "<label>-alt2.png", ... in the resources
    # folder (e.g. the same tab in its selected and unselected look).
    # find() cycles quickly through the main image and every alternate
    # until waiting_time runs out, so falling back costs at most
    # FIND_STEP_MS per attempt, and the image that matched last time is
    # tried first on the next call.
    #
    # NOTE: DesktopBot.__init__ binds self.find = self.find_until on the
    # instance, so the override must be on find_until (find, findUntil and
    # findLastUntil all end up here).
    # ------------------------------------------------------------------
    ALT_SUFFIX = "-alt"
    FIND_STEP_MS = 400
    _alt_labels = {}   # label -> tuple of existing alternates ("<label>-alt", "<label>-alt2", ...)
    _preferred = {}    # label -> image that matched last time (tried first)

    def _alt_labels_for(self, label):
        """Return the alternate image labels that exist for `label` ("-alt", "-alt2", ...)."""
        if label not in self._alt_labels:
            alts = []
            n = 1
            while True:
                alt = f"{label}{self.ALT_SUFFIX}{n if n > 1 else ''}"
                if not self._search_image_file(alt):
                    break
                alts.append(alt)
                n += 1
            self._alt_labels[label] = tuple(alts)
        return self._alt_labels[label]

    def _alt_label(self, label):
        """Return the first alternate image label for `label`, or None."""
        alts = self._alt_labels_for(label)
        return alts[0] if alts else None

    def find_until(self, label, x=None, y=None, width=None, height=None, *,
                   threshold=None, matching=0.9, waiting_time=10000, best=True, grayscale=False):
        """Same as DesktopBot.find_until, but also tries '<label>-alt', '<label>-alt2', ... when they exist."""
        kwargs = dict(threshold=threshold, matching=matching, best=best, grayscale=grayscale)
        alts = self._alt_labels_for(label)
        if not alts:
            return super().find_until(label, x, y, width, height, waiting_time=waiting_time, **kwargs)

        candidates = (label,) + alts
        first = self._preferred.get(label, label)
        order = (first,) + tuple(c for c in candidates if c != first)
        deadline = time.monotonic() + waiting_time / 1000.0
        while True:
            for candidate in order:
                remaining_ms = max(0, int((deadline - time.monotonic()) * 1000))
                element = super().find_until(candidate, x, y, width, height,
                                             waiting_time=min(self.FIND_STEP_MS, remaining_ms), **kwargs)
                if element:
                    self._preferred[label] = candidate
                    return element
            if time.monotonic() >= deadline:
                return None

    def action(self,
               execution=None,
               cnpj_sender=None,
               cnpj_receiver=None,
               payer=None,
               payer_cnpj=None):

        # Fetch the Activity ID from the task:
        # task = self.maestro.get_task(execution.task_id)
        # activity_id = task.activity_id

        # for protocol in emission list:

        if cnpj_sender is None:
            cnpj_sender = []

        if cnpj_receiver is None:
            cnpj_receiver = []

        windll.user32.EmptyClipboard()

        if self.find( "iniciarEmissao", matching=0.97, waiting_time=10):
            self.wait(500)
            self.click()

        if not self.find( "searchSender", matching=0.97, waiting_time=10000):
            self.not_found("searchSender")
        self.click_relative(58, 40)

        for cnpj0 in cnpj_sender:

            if not self.find( "CNPJ_input", matching=0.97, waiting_time=10000):
                self.not_found("CNPJ_input")
            self.doubleClickRelative(13, 24)
            self.paste_text(cnpj0)

            if self.find( "Found1", matching=0.97, waiting_time=1500):
                break
            else:
                continue

        if not self.find( "confirmCNPJ", matching=0.97, waiting_time=10000):
            self.not_found("confirmCNPJ")
        self.click()

        if not self.find( "searchReceiver", matching=0.97, waiting_time=10000):
            self.not_found("searchReceiver")
        self.click_relative(56, 36)

        for cnpj1 in cnpj_receiver:

            if not self.find( "CNPJ_input", matching=0.97, waiting_time=10000):
                self.not_found("CNPJ_input")
            self.doubleClickRelative(13, 24)
            self.paste_text(cnpj1)

            if self.find( "Found1", matching=0.97, waiting_time=1500):
                break
            else:
                continue

        if not self.find( "confirmCNPJ", matching=0.97, waiting_time=10000):
            self.not_found("confirmCNPJ")
        self.click()

        if payer == "Remetente":
            pass
        elif payer == "Destinatário":
            if not self.find( "Tomador1", matching=0.97, waiting_time=10000):
                self.not_found("Tomador1")
            self.click()
            self.type_down()
            self.enter()
        else:
            if not self.find( "Tomador1", matching=0.97, waiting_time=10000):
                self.not_found("Tomador1")
            self.click()
            if not self.find( "Others", matching=0.97, waiting_time=10000):
                self.not_found("Others")
            self.click()
            if not self.find( "searchPayer", matching=0.97, waiting_time=10000):
                self.not_found("searchPayer")
            self.click_relative(132, 20)
            if not self.find( "CNPJ_input", matching=0.97, waiting_time=10000):
                self.not_found("CNPJ_input")
            self.doubleClickRelative(13, 24)
            self.wait(300)
            self.paste_text(payer_cnpj)

            if not self.find( "Found1", matching=0.97, waiting_time=1500):
                self.not_found("Found1")

            if not self.find( "confirmCNPJ", matching=0.97, waiting_time=10000):
                self.not_found("confirmCNPJ")
            self.click()

    def part3_normal(self,
                     volumes=None,
                     cte_instance=None):

        if not self.find( "Part3", matching=0.97, waiting_time=10000):
            self.not_found("Part3")
        self.click()

        if not self.find( "IncludeNat", matching=0.97, waiting_time=10000):
            self.not_found("IncludeNat")
        self.click()

        if self.find( "NF-e", matching=0.97, waiting_time=1000):
            self.click()
            self.type_up()
            self.enter()

        # "Nat" and "Weight1" are the field *labels* (the inputs below them may
        # still hold the previous emission's values, which would break a
        # label+field image). Click into the field just below the label and
        # select all before pasting so the old value is replaced.
        if not self.find( "Nat", matching=0.97, waiting_time=10000):
            self.not_found("Nat")
        x, y, w, h = self.get_last_element()
        self.click_at(x + w // 2, y + 1.5 * h)
        self.control_a()
        self.paste_text('MATERIAL BIOLÓGICO')
        self.enter()

        if volumes is None:
            volumes = 1

        if not self.find( "Weight1", matching=0.97, waiting_time=10000):
            self.not_found("Weight1")
        x, y, w, h = self.get_last_element()
        self.click_at(x + w // 2, y + 1.5 * h)
        if cte_instance is None or cte_instance == 1:
            self.control_a()
            self.paste_text(str(int(volumes) * 2))   # Peso
            self.tab()
            self.control_a()
            self.paste_text(str(int(volumes) * 2))   # Peso Aferido
            self.tab()
            self.control_a()
            self.paste_text(str(int(volumes) * 2))   # Cubagem
            self.tab()
            self.tab(wait=500)
            self.control_a()
            self.type_keys(str(volumes))             # Volumes

        if not self.find( "confirmNAT", matching=0.97, waiting_time=10000):
            self.not_found("confirmNAT")
        self.click()


    def part3_complimentary(self,
                            cte=None
                            ):

        if self.find( "iniciarEmissao", matching=0.97, waiting_time=10):
            self.wait(500)
            self.click()

        if not self.find( "emissionComp", matching=0.97, waiting_time=60000):
            self.not_found("emissionComp")
        self.click_relative(100, 30)

        self.type_down()
        self.enter()

        if not self.find( "Part3", matching=0.97, waiting_time=60000):
            self.not_found("Part3")
        self.click()

        if not self.find( "includeCTe", matching=0.97, waiting_time=60000):
            self.not_found("includeCTe")
        self.click()        

        if not self.find( "searchCTe", matching=0.97, waiting_time=60000):
            self.not_found("searchCTe")
        self.click_relative(-37, 24)
        
        if not self.find( "clearDate", matching=0.97, waiting_time=60000):
            self.not_found("clearDate")
        self.click_relative(175, 43)
        self.type_up()
        self.enter()

        if not self.find( "inputCTE", matching=0.97, waiting_time=60000):
            self.not_found("inputCTE")
        self.click_relative(66, 35)

        self.type_key(cte)

        if not self.find( "findCTe", matching=0.97, waiting_time=60000):
            self.not_found("findCTe")
        self.click()

        if not self.find( "selectAll", matching=0.97, waiting_time=60000):
            self.not_found("selectAll")
        self.click()

        time.sleep(0.3)

        if not self.find( "confirmCTE", matching=0.97, waiting_time=60000):
            self.not_found("confirmCTE")
        self.click()

        time.sleep(0.3)

    def part4(self,
              icms_text=None,
              price=None,
              uf=None,
              tax=None,
              tp_info=None,
              complimentary=False):

        if not self.find( "part4", matching=0.97, waiting_time=30000):
            self.not_found("part4")
        self.click()

        if not self.find( "valueShipping22", matching=0.97, waiting_time=10000):
            self.not_found("valueShipping22")
        self.click()

        self.tab()
        self.type_key(price)
        
        if self.find("icms", matching=0.97, waiting_time=500):
            self.click()

        if not self.find( "CST", matching=0.97, waiting_time=10000):
            self.not_found("CST")
        self.click_relative(9, 8)

        self.tab()

        if uf == "MG":
            self.type_key('00')
            self.tab()
            self.tab()
        else:
            self.type_key('90')
            self.type_down()
            self.enter()
            self.tab()
            self.tab()
            self.type_key('0')
            self.tab()
        if complimentary:
            self.tab()

        self.type_key(tax)
        self.tab()
        if complimentary:
            self.tab()
        self.enter()
        
        if not self.find("ibs-cbs", matching=0.97, waiting_time=10000):
            self.not_found("ibs-cbs")
        self.click()
        
        self.wait(200)
            
        if self.find("ibs-cst", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.click_at(x + w // 2, y + 1.5*h)
            self.click()
            self.type_keys("000")
            self.enter()
            self.type_keys("0")
            self.enter()
            self.tab()
            self.enter()
        else:
            raise Exception("Elemento 'ibs-cst' não encontrado")

        if self.find("Obs2", matching=0.97, waiting_time=250):
            self.click()
        if not self.find("Obs3", matching=0.9, waiting_time=10000):
            self.not_found("Obs3")
        self.click()
        self.paste_text(icms_text)

        uf_list = ["AL", "AP", "GO", "MS", "MT", "PA", "PE", "PI", "PR", "RO", "RR", "RS", "SC", "TO"]

        if uf in uf_list:

            if not self.find( "payerObs", matching=0.97, waiting_time=10000):
                self.not_found("payerObs")
            self.right_click()

            if not self.find( "include_tp_info", matching=0.97, waiting_time=10000):
                self.not_found("include_tp_info")
            self.click()

            self.paste_text("GNRE_ICMSST")
            self.tab()
            self.paste_text(tp_info)

            if not self.find( "confirm_tp_info", matching=0.97, waiting_time=10000):
                self.not_found("confirm_tp_info")
            self.click()
            
        self.key_f4()

        if uf != "MG":
            if not self.find( "zeroValue", matching=0.97, waiting_time=10000):
                self.not_found("zeroValue")
            self.enter()
            
        if complimentary:
            if self.find( "confirmValue", matching=0.97, waiting_time=10000):
                self.enter()
            else:
                self.not_found("confirmValue")

        if not self.find( "finalizeEmission", matching=0.97, waiting_time=60000):
            self.not_found("finalizeEmission")
        x, y, w, h = self.get_last_element()
        xPos = w / 2
        yPos = 1.5*h
        self.click_relative(xPos, yPos)
        self.tab()
        self.tab()
        self.enter()

        if self.find( "creditconfirmPopUp", matching=0.97, waiting_time=3000):
            self.type_right()
            self.enter()

        if not self.find( "secondPopUp", matching=0.97, waiting_time=10000):
            self.not_found("secondPopUp")
        self.type_right()
        self.enter()

        if not self.find( "thirdPopUp", matching=0.97, waiting_time=10000):
            self.not_found("thirdPopUp")
        self.type_right()
        self.type_right()
        self.type_right()
        self.enter()

        if not self.find( "fourthPopUp", matching=0.97, waiting_time=10000):
            self.not_found("fourthPopUp")
        self.type_left()
        self.enter()

        # if not self.find( "Validation", matching=0.97, waiting_time=5000):
        #     pass
        # else:
        #     self.enter()

        if self.find( "contingencia", matching=0.97, waiting_time=5000):
            self.enter()

        if not self.find( "fifthPopUp", matching=0.97, waiting_time=60000):
            self.not_found("fifthPopUp")
        self.key_f10()
        self.wait(2000)
        if self.find("clicarCte", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            xPos = w / 2
            yPos = 1.5*h
            self.double_click_relative(xPos, yPos)
            self.control_c()
        else:
            raise Exception("Elemento 'clicarCte' não encontrado")
        self.wait(2000)

        # self.action()
        # Uncomment to mark this task as finished on BotMaestro
        # self.maestro.finish_task(
        #     task_id=execution.task_id,
        #     status=AutomationTaskFinishStatus.SUCCESS,
        #     message="Task Finished OK."
        # )

    def cancel_cte(self, numero_cte):
        """
        Cancel a single CTe document via UI automation.
        Args:
            numero_cte: CTe number to cancel
        Raises:
            Exception if cancellation fails
        """
        print(f"\n🔄 Cancelando CT-e: {numero_cte}")

        if not self.find("2consultas", matching=0.97, waiting_time=30000):
            raise Exception("Elemento '2consultas' não encontrado")
        self.click()
        self.wait(500)

        # Clear field
        if not self.find("10ClicarBordaInferior", matching=0.97, waiting_time=5000):
            raise Exception("Campo para digitar CT-e não encontrado ('10ClicarBordaInferior')")
        x, y, w, h = self.get_last_element()
        self.click_at(x + int(w * 0.5), y + h - 1)
        self.wait(500)
        self.control_a()
        self.wait(300)
        self.delete()
        self.wait(100)

        # Type CTe number
        self.type_keys(str(numero_cte))
        self.wait(200)

        # Search and process
        if not self.find("4Localizar", matching=0.97, waiting_time=5000):
            raise Exception("Elemento '4Localizar' não encontrado")
        self.click()
        self.wait(200)

        if self.find("5duploClickStatus", matching=0.97, waiting_time=5000):
            
            self.double_click()
            self.wait(200)

            if self.find("5.1ct-e", matching=0.97, waiting_time=10000):
                self.click()
                self.wait(200)

            # Click cancel button
            if not self.find("5.2Cancelar_CTE", matching=0.97, waiting_time=10000):
                raise Exception("Elemento '5.2Cancelar_CTE' não encontrado")
            self.click()
            self.wait(200)

            # Type cancellation reason
            self.paste_text("TRANSPORTE CANCELADO")
            self.wait(200)

            # Confirm cancellation
            if not self.find("8confirmar", matching=0.97, waiting_time=10000):
                raise Exception("Elemento '8confirmar' não encontrado")
            self.click()
            self.wait(200)

            # Confirm success popup
            if self.find("9-sucesso", matching=0.97, waiting_time=20000):
                self.enter()
                print("✅ Pop-up de sucesso confirmado.")
            else:
                raise Exception("Pop-up '9-sucesso' não encontrado.")

            print(f"✅ CT-e {numero_cte} cancelado com sucesso!")
        elif self.find("cancelled", matching=0.97, waiting_time=5000):
            print(f"⚠️ CT-e {numero_cte} já está cancelado.")
        else:
            raise Exception(f"CT-e {numero_cte} não encontrado ou status desconhecido.")

    
    def icms_slip_entry(self, cte_number, slip_value, supplier, cost_center, barcode_number, emission_date):
        print(f"\n🔄 Lançando guia do CT-e: {cte_number}")
        
        change_due_date = False
        # Inside the function:
        provided_date = datetime.strptime(str(emission_date), "%d/%m/%Y").date()
        if provided_date != date.today():
            change_due_date = True

        if not self.find("icms_start", matching=0.97, waiting_time=30000):
            raise Exception("Elemento 'icms_start' não encontrado")
        self.click()

        if self.find("icms_document", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.click_at(x + w // 2, y + 1.5*h)
            self.wait(200)
            self.type_keys(str(cte_number))
            self.wait(500)
                    
            if change_due_date:
                if self.find("icms_emission_date", matching=0.97, waiting_time=10000):
                    x, y, w, h = self.get_last_element()
                    xPos = w / 2
                    yPos = 1.5*h
                    self.double_click_relative(xPos, yPos)
                    self.type_keys(str(emission_date))
                else:
                    raise Exception("Elemento 'icms_emission_date' não encontrado")
                
                if self.find("icms_payment_date", matching=0.97, waiting_time=10000):
                    x, y, w, h = self.get_last_element()
                    xPos = w / 2
                    yPos = 1.5*h
                    self.double_click_relative(xPos, yPos)
                    self.type_keys(str(emission_date))
                else:
                    raise Exception("Elemento 'icms_payment_date' não encontrado")


                # if self.find("icms_payment_type", matching=0.97, waiting_time=10000):
                #     x, y, w, h = self.get_last_element()
                #     self.click_at(x + w // 2, y + 1.5*h)
                #     self.wait(200)
                #     self.type_down()
                #     self.enter()
                # else:
                #     raise Exception("Elemento 'icms_payment_type' não encontrado")
                
                # if not self.find("icms_due_date", matching=0.97, waiting_time=10000):
                #     raise Exception("Elemento 'icms_due_date' não encontrado")
                # self.click()
                
                # if self.find("icms_due_date_selected", matching=0.97, waiting_time=10000):
                #     x, y, w, h = self.get_last_element()
                #     self.click_at(x + w // 2, y + 1.5*h)
                #     self.wait(300)
                #     self.click_at(x + w // 2, y + 1.5*h)
                #     self.type_keys(str(emission_date))
                # else:
                #     raise Exception("Elemento 'icms_due_date_selected' não encontrado")
            else:
                self.tab()
                self.tab()

            self.tab()
            self.type_keys(str(slip_value))
        else:
            raise Exception("Elemento 'icms_document' não encontrado")
        
        if self.find("icms_state", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.click_at(x + w // 2, y + h)
            self.wait(200)
            self.paste_text(supplier)
            self.enter()
        else:
            raise Exception("Elemento 'icms_state' não encontrado")
        
        if self.find("icms_cost_center", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.double_click_relative(w // 2, h)
            self.wait(300)
            self.type_text_char_by_char(cost_center, wait_time=80)
            self.enter()


        else:
            raise Exception("Elemento 'icms_cost_center' não encontrado")
        
        if not self.find("icms_payment", matching=0.97, waiting_time=30000):
            raise Exception("Elemento 'icms_payment' não encontrado")
        self.click()

        if self.find("icms_bank", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.click_at(x + w // 2, y + 1.5*h)
            self.wait(200)
            self.paste_text("BANCO BRADESCO")
            self.enter()
        else:
            raise Exception("Elemento 'icms_bank' não encontrado")
        
        if self.find("icms_barcode", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.click_at(x + w // 2, y + 1.5*h)
            self.wait(200)
            self.paste_text(str(barcode_number))
            self.enter()
        else:
            raise Exception("Elemento 'icms_barcode' não encontrado")
        
        if self.find("icms_obs", matching=0.97, waiting_time=10000):
            self.click()
            self.wait(200)
            self.paste_text(f"REF. A ICMS ST {emission_date}")
        else:
            raise Exception("Elemento 'icms_obs' não encontrado")
        
        if not self.find("icms_confirm", matching=0.97, waiting_time=30000):
            raise Exception("Elemento 'icms_confirm' não encontrado")
        self.click()

        self.wait(500)
        

if __name__ == '__main__':
    Bot.main()


