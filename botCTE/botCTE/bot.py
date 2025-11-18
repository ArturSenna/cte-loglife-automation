from botcity.core import DesktopBot
from ctypes import windll
import time


# Uncomment the line below for integrations with BotMaestro
# Using the Maestro SDK
# from botcity.maestro import *

class Bot(DesktopBot):

    @staticmethod
    def not_found(label):
        print(f"Element not found: {label}")

    # NOT IN USE!
    def open_bsoft(self, path=None, login=None, password=None):

        # Open Bsoft
        self.execute(path)

        # Login
        if not self.find( "User", matching=0.97, waiting_time=120000):
            self.not_found("User")
        self.click()
        self.wait(500)
        self.type_key(login)

        # Senha
        if not self.find( "Password", matching=0.97, waiting_time=10000):
            self.not_found("Password")
        self.click()
        self.type_key(password)

        # Enter
        if not self.find( "loginFinish", matching=0.97, waiting_time=100000):
            self.not_found("loginFinish")
        self.click()

        # Close Inbox and confirm
        if not self.find( "close_inbox", matching=0.97, waiting_time=60000):
            self.not_found("close_inbox")
        self.click()

        if not self.find( "confirm_exit", matching=0.97, waiting_time=100000):
            self.not_found("confirm_exit")
        self.click()

        # Start emission
        if not self.find( "Emissão", matching=0.97, waiting_time=100000):
            self.not_found("Emissão")
        self.click()

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
            self.copy_to_clipboard(cnpj0)
            self.type_key(cnpj0)

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
            self.copy_to_clipboard(cnpj1)
            self.paste(cnpj1)

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
            self.paste(payer_cnpj)

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

        if not self.find( "Nat", matching=0.97, waiting_time=10000):
            self.not_found("Nat")
        self.click()
        self.type_key('M')
        self.enter()

        if volumes is None:
            volumes = 1

        if not self.find( "Weight1", matching=0.97, waiting_time=10000):
            self.not_found("Weight1")
        self.click()
        if cte_instance is None:
            self.paste(str(int(volumes) * 2))
            self.tab()
            self.paste(str(int(volumes) * 2))
            self.tab()
            self.paste(str(int(volumes) * 2))
            self.tab()
            self.tab(wait=500)
            self.paste(str(volumes))
        if cte_instance == 1:
            self.paste(str(int(volumes) * 2))
            self.tab()
            self.paste(str(int(volumes) * 2))
            self.tab()
            self.paste(str(int(volumes) * 2))
            self.tab()
            self.tab(wait=500)
            self.paste(str(volumes))

        if not self.find( "confirmNAT", matching=0.97, waiting_time=10000):
            self.not_found("confirmNAT")
        self.click()


    def part3_complimentary(self,
                            cte=None
                            ):

        if self.find( "Start emission", matching=0.97, waiting_time=100000):
            self.wait(500)
            self.key_f1()

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
            self.not_found("findCTe")
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
            self.not_found("confirmNAT")
        self.click()

        self.tab()
        self.type_key(price)

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

        if self.find("Obs2", matching=0.97, waiting_time=250):
            self.click()
        if not self.find("Obs3", matching=0.9, waiting_time=10000):
            self.not_found("Obs3")
        self.click()
        self.type_key(icms_text)

        uf_list = ["AL", "AP", "GO", "MS", "MT", "PA", "PE", "PI", "PR", "RO", "RR", "RS", "SC", "TO"]

        if uf in uf_list:

            if not self.find( "payerObs", matching=0.97, waiting_time=10000):
                self.not_found("payerObs")
            self.right_click()

            if not self.find( "include_tp_info", matching=0.97, waiting_time=10000):
                self.not_found("include_tp_info")
            self.click()

            self.type_key("GNRE_ICMSST")
            self.tab()
            self.type_key(tp_info)

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

        if not self.find( "confirmEmission", matching=0.97, waiting_time=60000):
            self.not_found("confirmEmission")
        self.double_click_relative(0, 26)
        self.control_c()
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

        if not self.find("5duploClickStatus", matching=0.97, waiting_time=5000):
            raise Exception("Elemento '5duploClickStatus' não encontrado")
        self.double_click()
        self.wait(200)

        if not self.find("5.1ct-e", matching=0.97, waiting_time=10000):
            raise Exception("Elemento '5.1ct-e' não encontrado")
        self.click()
        self.wait(200)

        # Click cancel button
        if not self.find("5.2Cancelar_CTE", matching=0.97, waiting_time=10000):
            raise Exception("Elemento '5.2Cancelar_CTE' não encontrado")
        self.click()
        self.wait(200)

        # Type cancellation reason
        self.type_keys("TRANSPORTE CANCELADO")
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

    
    def icms_slip_entry(self, cte_number, slip_value, supplier, cost_center, barcode_number):
        print(f"\n🔄 Lançando guia do CT-e: {cte_number}")

        if not self.find("icms_start", matching=0.97, waiting_time=30000):
            raise Exception("Elemento 'icms_start' não encontrado")
        self.click()

        if self.find("icms_document", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.click_at(x + w // 2, y + 1.5*h)
            self.wait(200)
            self.type_keys(str(cte_number))
            self.wait(500)
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
            self.type_keys(supplier)
            self.enter()
        else:
            raise Exception("Elemento 'icms_state' não encontrado")
        
        if self.find("icms_cost_center", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.click_at(x + w // 2, y + 1.5*h)
            self.wait(200)
            self.type_keys(cost_center)
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
            self.type_keys("BANCO BRADESCO")
            self.enter()
        else:
            raise Exception("Elemento 'icms_bank' não encontrado")
        
        if self.find("icms_barcode", matching=0.97, waiting_time=10000):
            x, y, w, h = self.get_last_element()
            self.click_at(x + w // 2, y + 1.5*h)
            self.wait(200)
            self.type_keys(str(barcode_number))
            self.enter()
        else:
            raise Exception("Elemento 'icms_barcode' não encontrado")
        
        if not self.find("icms_confirm", matching=0.97, waiting_time=30000):
            raise Exception("Elemento 'icms_confirm' não encontrado")
        self.click()

        self.wait(500)
        

if __name__ == '__main__':
    Bot.main()


