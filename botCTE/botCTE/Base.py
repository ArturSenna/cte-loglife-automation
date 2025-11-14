from functions import *
from emissions import *
from tkinter import *
from tkinter import filedialog as fd
from tkinter import Toplevel
from tkinter import ttk
from ttkthemes import ThemedStyle
from tkcalendar import DateEntry
import ctypes.wintypes
import threading
from json import loads
import csv
import pandas as pd
import numpy as np
import requests
import time
import bot

# Create Object
root = Tk()
root.title('CTe LogLife')
root.geometry("")
root.resizable(False, False)
root.iconbitmap("my_icon.ico")
thread_0 = Start(root)
thread_1 = Start(root)
thread_2 = Start(root)

# Setting tabs

tabs = ttk.Notebook(root)
tab1 = ttk.Frame(tabs)
tab2 = ttk.Frame(tabs)
tab2_1 = ttk.Frame(tabs)
tab3 = ttk.Frame(tabs)
tab4 = ttk.Frame(tabs)

tabs.add(tab1, text='CTe')
tabs.add(tab2, text='CTe complementar')
tabs.add(tab2_1, text='CTe por UF')
tabs.add(tab4, text='Pastas')
tabs.add(tab3, text='Utilitários')

tabs.pack(expand=1, fill="both")

tab2_frame = Frame(tab2, pady=22)
tab2_frame.pack()

tab2_frame2 = Frame(tab2)
tab2_frame2.pack()

tab4_frame = Frame(tab4)
tab4_frame.pack()
#################################################################################################################################################
# ============================
# ABA DE CANCELAMENTO DE CTEs
# ============================

from tkinter import ttk, Frame, font
from tkcalendar import DateEntry

# Criação da aba e frame principal
tab5 = ttk.Frame(tabs)
tabs.add(tab5, text='Cancelamento de CTE')
tab5_frame = Frame(tab5, pady=22)
tab5_frame.pack()

# Fonte personalizada para consistência visual
custom_font = font.nametofont("TkDefaultFont")
custom_font.configure(size=10)



# Campo: Data inicial
ttk.Label(tab5_frame, text="Data inicial:", font=custom_font).grid(row=0, column=0, sticky='W', padx=10, pady=5)
data_inicial_cancelamento = DateEntry(
    tab5_frame,
    width=18,
    background='white',
    foreground='black',
    borderwidth=2,
    date_pattern='dd/mm/yyyy',
    locale='pt_BR',
    font=custom_font,
    justify='center',
    selectbackground='darkblue'
)
data_inicial_cancelamento.grid(row=0, column=1, padx=10, pady=5)

# Campo: Data final
ttk.Label(tab5_frame, text="Data final:", font=custom_font).grid(row=1, column=0, sticky='W', padx=10, pady=5)
data_final_cancelamento = DateEntry(
    tab5_frame,
    width=18,
    background='white',
    foreground='black',
    borderwidth=2,
    date_pattern='dd/mm/yyyy',
    locale='pt_BR',
    font=custom_font,
    justify='center',
    selectbackground='darkblue'
)
data_final_cancelamento.grid(row=1, column=1, padx=10, pady=5)

# Botão: Cancelar lista de CTe
btn_cancelar_lista = ttk.Button(tab5_frame, text="Cancelar lista de CTe",
    command=lambda: thread_2.start_thread(
        lambda *args: Bot().action(args[0], args[1]),
        progressbar5,
        arguments=[
            datetime.strptime(data_inicial_cancelamento.get(), "%d/%m/%Y").strftime("%Y-%m-%d"),
            datetime.strptime(data_final_cancelamento.get(), "%d/%m/%Y").strftime("%Y-%m-%d"),
            root  # será ignorado
        ]
    )
)
btn_cancelar_lista.grid(row=0, column=2, rowspan=2, padx=10, pady=5, sticky='ns')
# Campo: CTe avulso
ttk.Label(tab5_frame, text="CTe avulso:", font=custom_font).grid(row=2, column=0, sticky='W', padx=10, pady=5)
entry_cte_avulso = ttk.Entry(tab5_frame, width=30, font=custom_font)
entry_cte_avulso.grid(row=2, column=1, padx=10, pady=5)

# Campo: Protocolo avulso
ttk.Label(tab5_frame, text="Protocolo avulso:", font=custom_font).grid(row=3, column=0, sticky='W', padx=10, pady=5)
entry_protocolo_avulso = ttk.Entry(tab5_frame, width=30, font=custom_font)
entry_protocolo_avulso.grid(row=3, column=1, padx=10, pady=5)

# Botão: Cancelar avulso (à direita do campo CT-e)
btn_cancelar_avulso = ttk.Button(tab5_frame, text="Cancelar",
    command=lambda: thread_2.start_thread(
        cancelar_avulso_cte, progressbar5,
        arguments=[
            entry_cte_avulso.get(),
            entry_protocolo_avulso.get(),
            root
        ]
    )
)
btn_cancelar_avulso.grid(row=2, column=2, rowspan=2, padx=10, pady=5)

# Barra de progresso da aba
progressbar5 = ttk.Progressbar(tab5, mode='indeterminate')
progressbar5.pack(side='bottom', fill='x')

# Autoajuste de layout da aba
tab5.rowconfigure(0, weight=1)
tab5.columnconfigure(0, weight=1)
tab5.columnconfigure(1, weight=1)
tab5.columnconfigure(2, weight=1)

########################################################################################################################################################
# Set calendar

today = dt.datetime.today()

dia = today.day
mes = today.month
ano = today.year

style = ThemedStyle(root)
style.theme_use('breeze')

cal_config = {'selectmode': 'day',
              'day': dia,
              'month': mes,
              'year': ano,
              'locale': 'pt_BR',
              'firstweekday': 'sunday',
              'showweeknumbers': False,
              'bordercolor': "white",
              'background': "white",
              'disabledbackground': "white",
              'headersbackground': "white",
              'normalbackground': "white",
              'normalforeground': 'black',
              'headersforeground': 'black',
              'selectbackground': '#00a5e7',
              'selectforeground': 'white',
              'weekendbackground': 'white',
              'weekendforeground': 'black',
              'othermonthforeground': 'black',
              'othermonthbackground': '#E8E8E8',
              'othermonthweforeground': 'black',
              'othermonthwebackground': '#E8E8E8',
              'foreground': "black"}

cal = DateEntry(tab1, **cal_config)
cal_f = DateEntry(tab1, **cal_config)
cal1_uf = DateEntry(tab2_1, **cal_config)
cal2_uf = DateEntry(tab2_1, **cal_config)
cal1 = DateEntry(tab2_frame, **cal_config)
cal2 = DateEntry(tab2_frame, **cal_config)

cal.grid(column=1, row=0, padx=30, pady=10, sticky="E, W")
cal_f.grid(column=1, row=1, padx=30, pady=10, sticky="E, W")
cal1_uf.grid(column=1, row=0, padx=30, pady=10, sticky="E, W")
cal2_uf.grid(column=1, row=1, padx=30, pady=10, sticky="E, W")
cal1.grid(column=1, row=0, padx=30, pady=10, sticky="E, W")
cal2.grid(column=1, row=1, padx=30, pady=10, sticky="E, W")

# Read file name

cte_folder = StringVar()

try:
    with open('filename.txt') as m:
        text = m.read()
    lines = text.split('\n')
    cte_folder.set(lines[0])
except FileNotFoundError:
    cte_folder.set('Pasta onde os arquivos CTe estão.')

cte_Folder_Label = ttk.Label(tab4_frame, width=20, text=cte_folder.get(), wraplength=140)
cte_Folder_Label.grid(column=1, row=2, sticky="E, W", padx=20, pady=10)

# Read folder path

folderpath = StringVar()

try:
    with open('folderpath.txt') as m:
        text = m.read()
    lines = text.split('\n')
    folderpath.set(lines[0])
except FileNotFoundError:
    folderpath.set('Pasta para CTe normal')

folder_Label = ttk.Label(tab4_frame, width=20, text=folderpath.get(), wraplength=140)
folder_Label.grid(column=1, row=0, sticky="E, W", padx=20, pady=10)

folderpath2 = StringVar()

try:
    with open('folderpath2.txt') as m:
        text = m.read()
    lines = text.split('\n')
    folderpath2.set(lines[0])
except FileNotFoundError:
    folderpath2.set('Pasta para CTe complementar')

folder_Label2 = ttk.Label(tab4_frame, width=20, text=folderpath2.get(), wraplength=140)
folder_Label2.grid(column=1, row=1, sticky="E, W", padx=20, pady=10)

browse1 = Browse(cte_Folder_Label)
browse2 = Browse(folder_Label)
browse3 = Browse(folder_Label2)

# Add Buttons
ttk.Button(tab1,
           text="Emitir lista de CTe",
           command=lambda: thread_0.start_thread(
               cte_list, progressbar, arguments=[cal.get_date(),
                                                 cal_f.get_date(),
                                                 folderpath.get(),
                                                 cte_folder.get(),
                                                 root]
           )).grid(column=2, row=0, rowspan=2, padx=10, pady=15, ipady=15, sticky=" E, W")

ttk.Button(tab3,
           text="Limpar CTe simbólico",
           command=lambda: thread_0.start_thread(
               clear_cte_number, progressbar3, arguments=[cal.get_date(),
                                                          cal_f.get_date(),
                                                          folderpath.get(),
                                                          root]
           )).pack(pady=30)

ttk.Button(tab3,
           text="Emitir CTe simbólico",
           command=lambda: thread_0.start_thread(
               cte_symbolic, progressbar3, arguments=[cal.get_date(),
                                                      cal_f.get_date(),
                                                      folderpath.get(),
                                                      cte_folder.get(),
                                                      root]
           )).pack(pady=30)

ttk.Button(tab1,
           text="Emitir",
           command=lambda: thread_0.start_thread(
               cte_unique, progressbar, arguments=[cal.get_date(),
                                                   folderpath.get(),
                                                   cte_folder.get(),
                                                   cte_type.get(),
                                                   cte_s.get(),
                                                   volumes.get(),
                                                   root]
           )).grid(column=2, row=2, padx=10, pady=10, sticky="N, S, E, W")

ttk.Button(tab2_frame,
           text="Emitir",
           command=lambda: thread_1.start_thread(
               cte_complimentary, progressbar2, arguments=[cal1.get_date(),
                                                           cal2.get_date(),
                                                           folderpath2.get(),
                                                           cte_folder.get(),
                                                           root]
           )).grid(column=2, row=0, rowspan=2, padx=10, pady=15, ipady=15, sticky=" E, W")

ttk.Button(tab2_frame2,
           text="Emitir",
           command=lambda: thread_1.start_thread(
               cte_complimentary, progressbar2, arguments=[cal1.get_date(),
                                                           cal2.get_date(),
                                                           folderpath2.get(),
                                                           cte_folder.get(),
                                                           root,
                                                           True,
                                                           cte_cs.get()]
           )).grid(column=2, row=0, padx=10, pady=10, sticky="N, S, E, W")

ttk.Button(tab2_1,
           text="Emitir CTes por UF",
           command=lambda: thread_2.start_thread(
               cte_list_grouped, progressbar4, arguments=[cal1_uf.get_date(),
                                                            cal2_uf.get_date(),
                                                            folderpath.get(),
                                                            cte_folder.get(),
                                                            root]
           )).grid(column=2, row=0, rowspan=2, padx=10, pady=15, ipady=15, sticky=" E, W")

ttk.Button(tab4_frame,
           text="Procurar",
           command=lambda: browse1.browse_folder(cte_folder, 'filename.txt', master=tab4_frame,
                                                 label_config={'wraplength': 140, 'width': 20},
                                                 grid_config={'column': 1, 'row': 0, 'sticky': "E, W", 'padx': 20,
                                                              'pady': 10})
           ).grid(column=2, row=2, padx=5, pady=5, sticky="E, W")

ttk.Button(tab4_frame,
           text="Procurar",
           command=lambda: browse2.browse_folder(folderpath, 'folderpath.txt', master=tab4_frame,
                                                 label_config={'wraplength': 140, 'width': 20},
                                                 grid_config={'column': 1, 'row': 0, 'sticky': "E, W", 'padx': 20,
                                                              'pady': 10})
           ).grid(column=2, row=0, padx=5, pady=5, sticky="E, W")

ttk.Button(tab4_frame,
           text="Procurar",
           command=lambda: browse3.browse_folder(folderpath2, 'folderpath2.txt', master=tab4_frame,
                                                 label_config={'wraplength': 140, 'width': 20},
                                                 grid_config={'column': 1, 'row': 1, 'sticky': "E, W", 'padx': 20,
                                                              'pady': 10})
           ).grid(column=2, row=1, padx=5, pady=5, sticky="E, W")

# Labels

ttk.Label(tab1, text="Data inicial:").grid(column=0, row=0, padx=10, pady=10, sticky='W, E')
ttk.Label(tab1, text="Data final:").grid(column=0, row=1, padx=10, pady=10, sticky='W, E')
ttk.Label(tab1, text="CTe avulso:").grid(column=0, row=2, padx=10, pady=10, sticky='W, E')
ttk.Label(tab1, text="Vols:").grid(column=2, row=5, padx=5, pady=10, sticky='W, E')

ttk.Label(tab2_frame, text="Data Inicial:").grid(column=0, row=0, padx=10, pady=10, sticky='W, E')
ttk.Label(tab2_frame, text="Data Final:").grid(column=0, row=1, padx=10, pady=10, sticky='W, E')

ttk.Label(tab2_frame2, text="CTe avulso:").grid(column=0, row=0, padx=10, pady=10, sticky='W, E')

ttk.Label(tab2_1, text="Data inicial:").grid(column=0, row=0, padx=10, pady=10, sticky='W, E')
ttk.Label(tab2_1, text="Data final:").grid(column=0, row=1, padx=10, pady=10, sticky='W, E')

ttk.Label(tab4_frame, text="CTe normal:").grid(column=0, row=0, padx=5, pady=5)
ttk.Label(tab4_frame, text="CTe complementar:").grid(column=0, row=1, padx=5, pady=5)
ttk.Label(tab4_frame, text="Pasta CTE:").grid(column=0, row=2, padx=5, pady=5)

# Adding Entry texts

cte_s = StringVar()

ttk.Entry(tab1,
          textvariable=cte_s, width=14).grid(column=1, row=2, padx=10, pady=10)

cte_cs = StringVar()

ttk.Entry(tab2_frame2,
          textvariable=cte_cs, width=14).grid(column=1, row=0, padx=10, pady=10)

volumes = StringVar()
ttk.Entry(tab1,
          textvariable=volumes, width=8).grid(column=2, row=5, pady=5, ipadx=1)
volumes.set('1')

# Radio Buttons

cte_type = IntVar()

ttk.Radiobutton(tab1, text="Normal", value=0, variable=cte_type).grid(column=0, row=5, ipadx=18)
ttk.Radiobutton(tab1, text="Simbólico", value=1, variable=cte_type).grid(column=1, row=5, ipadx=10)

# Progress Bar

progressbar = ttk.Progressbar(tab1, mode='indeterminate')
progressbar.grid(column=0, row=6, sticky='W, E', columnspan=3)

progressbar2 = ttk.Progressbar(tab2, mode='indeterminate')
progressbar2.pack(side=BOTTOM, fill='x')

progressbar3 = ttk.Progressbar(tab3, mode='indeterminate')
progressbar3.pack(side=BOTTOM, fill='x')

progressbar4 = ttk.Progressbar(tab2_1, mode='indeterminate')
progressbar4.grid(column=0, row=6, sticky='W, E', columnspan=3)


# Auto resize tabs

tab1.rowconfigure(2, weight=2)
tab1.columnconfigure(0, weight=1)
tab1.columnconfigure(1, weight=1)
tab1.columnconfigure(2, weight=1)

tab2_1.rowconfigure(2, weight=2)
tab2_1.columnconfigure(0, weight=1)
tab2_1.columnconfigure(1, weight=1)
tab2_1.columnconfigure(2, weight=1)

tab3.rowconfigure(0, weight=1)
tab3.columnconfigure(0, weight=1)
tab3.columnconfigure(1, weight=1)
tab3.columnconfigure(2, weight=1)

# Execute Tkinter
root.mainloop()
