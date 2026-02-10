import os
import sys
import threading
from json import loads
from tkinter import filedialog as fd
from tkinter import Toplevel
from tkinter import ttk
from tkinter import *
import csv
import pandas as pd
import numpy as np
import requests
from dotenv import load_dotenv

# Determine the script directory (works for both normal and frozen/packaged)
if getattr(sys, 'frozen', False):
    # Running as compiled executable with PyInstaller
    # Use _MEIPASS to access bundled resources (like icon files)
    _SCRIPT_DIR = getattr(sys, '_MEIPASS', os.path.dirname(sys.executable))
else:
    # Running as script
    _SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

# Load environment variables from .env file in script directory
env_path = os.path.join(_SCRIPT_DIR, '.env')
if os.path.exists(env_path):
    load_dotenv(dotenv_path=env_path)
    print(f"[INFO] Loaded .env from: {env_path}")
else:
    print(f"[WARNING] .env file not found at: {env_path}")
    load_dotenv()  # Try default locations as fallback


# import xlwings as xw
# from pythoncom import CoInitialize


class Start:

    def __init__(self, root_master):
        self.submit_thread = None
        self.master = root_master
        self._running = False

    def is_running(self):
        """Check if a task is currently running."""
        return self._running and self.submit_thread is not None and self.submit_thread.is_alive()

    def start_thread(self, target_function, progress_bar_func=None, arguments=(), status_label=None):
        """Start a task in a background thread. Prevents double-click by ignoring
        calls while a previous task is still running."""
        if self.is_running():
            return  # Guard: ignore if already running

        self._running = True

        def wrapped_target(*args):
            """Wrapper that catches exceptions so the running flag always resets."""
            try:
                target_function(*args)
            except Exception as e:
                print(f"[ERROR] Thread exception: {e}")
            finally:
                self._running = False

        def check_thread():
            if self.submit_thread.is_alive():
                self.master.after(100, check_thread)
            else:
                if progress_bar_func is not None:
                    progress_bar_func.stop()
                if status_label is not None:
                    status_label.config(text="✔ Concluído")
                self._running = False

        if status_label is not None:
            status_label.config(text="⏳ Processando...")

        self.submit_thread = threading.Thread(target=wrapped_target, args=arguments)
        self.submit_thread.daemon = True
        self.submit_thread.start()
        if progress_bar_func is not None:
            progress_bar_func.start()
            self.master.after(100, check_thread)


class Browse:
    """Handles file/folder browsing dialogs and updates the associated StringVar."""

    def __init__(self, string_variable=None):
        self.string_variable = string_variable

    def browse_files(self, filename_variable=None, archive_name=None, **_kwargs):
        """Open a file browser for Excel files."""
        filetypes = (
            ('Arquivos Excel', '*.xlsx'),
            ('Excel habilitado para macro', '*.xlsm')
        )

        file_name = fd.askopenfilename(
            title='Selecione o arquivo',
            initialdir='cd',
            filetypes=filetypes
        )

        if not file_name:
            return  # User cancelled the dialog

        with open(f"{archive_name}", 'w') as w:
            w.write(file_name)

        target_var = filename_variable or self.string_variable
        if target_var is not None:
            target_var.set(file_name)

    def browse_folder(self, folder_variable=None, archive_name='folderpath.txt', **_kwargs):
        """Open a folder browser dialog."""
        folder_path = fd.askdirectory(
            title='Selecione a pasta',
            initialdir='cd',
        )

        if not folder_path:
            return  # User cancelled the dialog

        with open(archive_name, 'w') as w:
            w.write(folder_path)

        target_var = folder_variable or self.string_variable
        if target_var is not None:
            target_var.set(folder_path)

    def browse_exe(self, filename_variable=None, archive_name=None, **_kwargs):
        """Open a file browser for executables."""
        filetypes = (('Arquivo executável', '*.exe'),)

        file_name = fd.askopenfilename(
            title='Selecione o arquivo',
            initialdir='cd',
            filetypes=filetypes
        )

        if not file_name:
            return  # User cancelled the dialog

        with open(f"{archive_name}", 'w') as w:
            w.write(file_name)

        target_var = filename_variable or self.string_variable
        if target_var is not None:
            target_var.set(file_name)


class RequestDataFrame:

    def __init__(self):
        # Get credentials from environment variables
        xtoken = os.getenv("LOGLIFE_XTOKEN")
        email = os.getenv("LOGLIFE_USER")
        password = os.getenv("LOGLIFE_PASSWORD")
        
        if not all([xtoken, email, password]):
            raise ValueError(
                "Missing required environment variables. "
                "Please ensure LOGLIFE_XTOKEN, LOGLIFE_USER, and LOGLIFE_PASSWORD are set in .env file"
            )
        
        self.headers = {"xtoken": xtoken}
        details = {"email": email, "password": password}
        key = requests.post("https://transportebiologico.com.br/api/sessions", json=details)
        key_json = loads(key.text)
        self.auth = {"authorization": key_json["token"]}

    def request_public(self, link, request_type="get"):

        if request_type == "get":
            response = requests.get(link, headers=self.headers)
        elif request_type == "post":
            response = requests.post(link, headers=self.headers)
        else:
            response = requests.get(link, headers=self.headers)  # CHANGE LATER

        response_json = loads(response.text)
        dataframe = pd.json_normalize(response_json)

        return dataframe

    def request_private(self, link: str, request_type="get", payload: dict = None, nested: bool = False, json: bool = False) -> pd.DataFrame:

        if request_type == "get":
            response = requests.get(link, headers=self.auth, data=payload)
        elif request_type == "post":
            if not json:
                response = requests.post (link, headers=self.auth, data=payload)
            else:
                response = requests.post (link, headers=self.auth, json=payload)
        else:
            response = requests.get(link, headers=self.auth)  # CHANGE LATER

        if nested:
            response_data = response.json()
            dataframe = pd.DataFrame(response_data["data"])
        else:
            response_json = loads(response.text)
            dataframe = pd.json_normalize(response_json)

        return dataframe

    def post_file(self, link, file, file_type="csv_file", file_format="text/csv", upload_type=None):

        payload = {
            file_type: (
                file,
                open(file, "rb"),
                file_format,
                {"Expires": "0"},
            )
        }

        if upload_type is not None:
            upload_type = {
                "type": upload_type
            }

        response = requests.post(url=link, headers=self.auth, files=payload, data=upload_type)

        return response

    def post_private(self, link, payload):

        response = requests.post(url=link, headers=self.auth, data=payload)

        return response


# def export_to_excel(df, excel_name, sheet="Planilha1", clear_range="A1:A1", autofit=True, change_header=True,
#                     start_write=None, clear_filters=False):
#     app = xw.App(visible=False)
#     wb = xw.Book(f'{excel_name}')
#     ws = wb.sheets[f'{sheet}']
#     app.kill()
#     if wb.sheets[f'{sheet}'].api.AutoFilter:
#         wb.sheets[f'{sheet}'].api.AutoFilter.ShowAllData()
#     elif clear_filters:
#         wb.sheets[f'{sheet}'].api.AutoFilter.ShowAllData()
#     ws.range(clear_range).clear_contents()
#
#     if start_write is None:
#
#         if change_header:
#             start_write = "A1"
#             header_config = 1
#         else:
#             start_write = "A2"
#             header_config = 0
#     else:
#
#         if change_header:
#             header_config = 1
#         else:
#             header_config = 0
#
#     # Inserção do DataFrame na planilha
#     ws[f"{start_write}"].options(pd.DataFrame, header=header_config, index=False, expand='table').value = df
#
#     if autofit:
#         ws.autofit('r')
#
#
# def clear_data(filename, *sheet):
#     file_name = filename.replace('/', '\\')
#
#     CoInitialize()
#
#     for value in sheet:
#         app = xw.App(visible=False)
#         wb = xw.Book(f"{file_name}")
#         terms = value.split(';')
#         ws = wb.sheets[f'{terms[0]}']
#         app.kill()
#         if wb.sheets[f'{terms[0]}'].api.AutoFilter:
#             wb.sheets[f'{terms[0]}'].api.AutoFilter.ShowAllData()
#         ws.range(terms[1]).clear_contents()


def confirmation_pop_up(root, text):
    pop = Toplevel(root)
    pop.iconbitmap(os.path.join(_SCRIPT_DIR, "my_icon.ico"))
    pop.attributes('-topmost', 'true')
    pop.geometry("")
    pop.title("Confirmação")
    thread_pop = Start(pop)
    ttk.Label(pop, text=text).pack(padx=20, pady=10)
    ttk.Button(
        pop,
        text="OK",
        command=lambda: thread_pop.start_thread(lambda: [pop.destroy()])
    ).pack()


def post_file():
    r = RequestDataFrame()

    protocol = 95475
    cte_file = 'Minuta.pdf'
    cte = 123456789
    data_emissao = '18/10/2023'

    cte_csv = pd.DataFrame({
        'Protocolo': [protocol],
        'Arquivo PDF': [cte_file],
    })

    upload_cte_csv = pd.DataFrame({
        'Protocolo': [protocol],
        'CTE Loglife': [cte],
        'Data Emissão CTE': [data_emissao]
    })

    cte_csv.to_csv('AssociarPDF.csv', index=False, encoding='utf-8')
    upload_cte_csv.to_csv('UploadCTE.csv', index=False,  encoding='utf-8')

    cte_upload = r.post_file('https://transportebiologico.com.br/api/uploads/cte-loglife', 'UploadCTE.csv')

    pdf_upload = r.post_file("https://transportebiologico.com.br/api/pdf",
                             cte_file,
                             upload_type="CTE LOGLIFE",
                             file_format="application/pdf",
                             file_type="pdf_files")

    associate_pdf = r.post_file('https://transportebiologico.com.br/api/pdf/associate',
                                'AssociarPDF.csv',
                                upload_type="CTE LOGLIFE")

    print(pdf_upload.text, pdf_upload)
    print(associate_pdf.text, associate_pdf)
    print(cte_upload.text, cte_upload)


if __name__ == "__main__":
    post_file()
