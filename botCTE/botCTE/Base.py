"""
CTe LogLife - Main Application Interface
Modern redesigned UI for CTe management system
"""

# Standard library imports
import csv
import ctypes.wintypes
import datetime as dt
import threading
import time
from json import loads
from tkinter import *
from tkinter import filedialog as fd
from tkinter import Toplevel
from tkinter import ttk

# Third-party imports
import numpy as np
import pandas as pd
import requests
from tkcalendar import DateEntry
from ttkthemes import ThemedStyle

# Local imports
from functions import *
try:
    from emissions import *
except ImportError:
    pass  # Handle missing emissions module gracefully
import bot


# =============================================================================
# CONSTANTS AND CONFIGURATION
# =============================================================================

# Get the directory where this script is located
_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

APP_TITLE = 'CTe LogLife'
APP_ICON = os.path.join(_SCRIPT_DIR, "my_icon.ico")
WINDOW_SIZE = ""
THEME_NAME = 'breeze'

# UI Colors
PRIMARY_COLOR = '#00a5e7'
BACKGROUND_COLOR = 'white'
TEXT_COLOR = 'black'
DISABLED_BG = '#E8E8E8'

# Padding constants for consistent spacing
PADDING_SMALL = 5
PADDING_MEDIUM = 10
PADDING_LARGE = 20
PADDING_XL = 30

# File paths for configuration
CONFIG_FILES = {
    'cte_folder': 'filename.txt',
    'folderpath': 'folderpath.txt',
    'folderpath2': 'folderpath2.txt',
    'relatorio_bsoft': 'relatorio_bsoft.txt',
    'relatorio_target': 'relatorio_target.txt'
}

# Default messages
DEFAULT_MESSAGES = {
    'cte_folder': 'Pasta onde os arquivos CTe estão.',
    'folderpath': 'Pasta para CTe normal',
    'folderpath2': 'Pasta para CTe complementar',
    'relatorio_bsoft': 'Relatório B-soft (.xlsx/.xls)',
    'relatorio_target': 'Relatório Target (.xlsx/.xls)'
}


# =============================================================================
# HELPER FUNCTIONS
# =============================================================================

def read_config_file(filename, default_value):
    """Read configuration file and return content or default value."""
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            text = f.read()
        lines = text.split('\n')
        return lines[0] if lines else default_value
    except FileNotFoundError:
        return default_value


def get_calendar_config(dia, mes, ano):
    """Return calendar configuration dictionary."""
    return {
        'selectmode': 'day',
        'day': dia,
        'month': mes,
        'year': ano,
        'locale': 'pt_BR',
        'firstweekday': 'sunday',
        'showweeknumbers': False,
        'bordercolor': BACKGROUND_COLOR,
        'background': BACKGROUND_COLOR,
        'disabledbackground': BACKGROUND_COLOR,
        'headersbackground': BACKGROUND_COLOR,
        'normalbackground': BACKGROUND_COLOR,
        'normalforeground': TEXT_COLOR,
        'headersforeground': TEXT_COLOR,
        'selectbackground': PRIMARY_COLOR,
        'selectforeground': BACKGROUND_COLOR,
        'weekendbackground': BACKGROUND_COLOR,
        'weekendforeground': TEXT_COLOR,
        'othermonthforeground': TEXT_COLOR,
        'othermonthbackground': DISABLED_BG,
        'othermonthweforeground': TEXT_COLOR,
        'othermonthwebackground': DISABLED_BG,
        'foreground': TEXT_COLOR
    }


# =============================================================================
# APPLICATION SETUP
# =============================================================================

# Create main window
root = Tk()
root.title(APP_TITLE)
root.geometry(WINDOW_SIZE)
root.resizable(False, False)
try:
    root.iconbitmap(APP_ICON)
except:
    pass  # Icon file not found, continue without it

# Initialize threading objects
thread_0 = Start(root)
thread_1 = Start(root)
thread_2 = Start(root)

# =============================================================================
# NOTEBOOK TABS SETUP
# =============================================================================

# Create main notebook
tabs = ttk.Notebook(root)

# Create tab frames with better organization
tab1 = ttk.Frame(tabs, padding=PADDING_MEDIUM)  # CTe
tab2 = ttk.Frame(tabs, padding=PADDING_MEDIUM)  # CTe complementar
tab2_1 = ttk.Frame(tabs, padding=PADDING_MEDIUM)  # CTe por UF
tab3 = ttk.Frame(tabs, padding=PADDING_MEDIUM)  # Utilitários
tab4 = ttk.Frame(tabs, padding=PADDING_MEDIUM)  # Arquivos
tab5 = ttk.Frame(tabs, padding=PADDING_MEDIUM)  # Cancelamento

# Add tabs in logical order
tabs.add(tab1, text='📋 CTe')
tabs.add(tab2, text='➕ CTe Complementar')
tabs.add(tab2_1, text='🗺️ CTe por UF')
tabs.add(tab4, text='📁 Arquivos')
tabs.add(tab3, text='🔧 Utilitários')
tabs.add(tab5, text='❌ Cancelamento')

tabs.pack(expand=1, fill="both", padx=PADDING_SMALL, pady=PADDING_SMALL)

# Create sub-frames for tab2 (CTe Complementar)
tab2_frame = Frame(tab2, pady=PADDING_LARGE)
tab2_frame.pack(fill='x', padx=PADDING_MEDIUM)

tab2_frame2 = Frame(tab2)
tab2_frame2.pack(fill='x', padx=PADDING_MEDIUM, pady=PADDING_MEDIUM)

# Frame for tab4 (Arquivos)
tab4_frame = Frame(tab4, pady=PADDING_MEDIUM)
tab4_frame.pack(fill='both', expand=True)

# =============================================================================
# STYLING AND THEME
# =============================================================================

# Apply modern theme
style = ThemedStyle(root)
style.theme_use(THEME_NAME)

# Get current date for calendar defaults
today = dt.datetime.today()
dia, mes, ano = today.day, today.month, today.year

# Get calendar configuration
cal_config = get_calendar_config(dia, mes, ano)


# =============================================================================
# DATE PICKERS (CALENDARS)
# =============================================================================

# Tab1 - CTe
cal = DateEntry(tab1, **cal_config)
cal_f = DateEntry(tab1, **cal_config)

# Tab2_1 - CTe por UF
cal1_uf = DateEntry(tab2_1, **cal_config)
cal2_uf = DateEntry(tab2_1, **cal_config)

# Tab2 - CTe Complementar
cal1 = DateEntry(tab2_frame, **cal_config)
cal2 = DateEntry(tab2_frame, **cal_config)

# Tab5 - Cancelamento
cal1_cancel = DateEntry(tab5, **cal_config)
cal2_cancel = DateEntry(tab5, **cal_config)

# Position calendars with improved spacing
cal.grid(column=1, row=0, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky="EW")
cal_f.grid(column=1, row=1, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky="EW")
cal1_uf.grid(column=1, row=0, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky="EW")
cal2_uf.grid(column=1, row=1, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky="EW")
cal1.grid(column=1, row=0, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky="EW")
cal2.grid(column=1, row=1, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky="EW")
cal1_cancel.grid(column=1, row=0, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky="EW")
cal2_cancel.grid(column=1, row=1, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky="EW")

# =============================================================================
# CONFIGURATION VARIABLES AND LABELS
# =============================================================================

# Initialize StringVars and read configuration files
cte_folder = StringVar()
cte_folder.set(read_config_file(CONFIG_FILES['cte_folder'], DEFAULT_MESSAGES['cte_folder']))

folderpath = StringVar()
folderpath.set(read_config_file(CONFIG_FILES['folderpath'], DEFAULT_MESSAGES['folderpath']))

folderpath2 = StringVar()
folderpath2.set(read_config_file(CONFIG_FILES['folderpath2'], DEFAULT_MESSAGES['folderpath2']))

relatorio_bsoft = StringVar()
relatorio_bsoft.set(read_config_file(CONFIG_FILES['relatorio_bsoft'], DEFAULT_MESSAGES['relatorio_bsoft']))

relatorio_target = StringVar()
relatorio_target.set(read_config_file(CONFIG_FILES['relatorio_target'], DEFAULT_MESSAGES['relatorio_target']))

# Create labels for file paths with better styling
label_style = {'width': 25, 'wraplength': 180, 'relief': 'sunken', 'padding': PADDING_SMALL}

folder_Label = ttk.Label(tab4_frame, text=folderpath.get(), **label_style)
folder_Label.grid(column=1, row=0, sticky="EW", padx=PADDING_LARGE, pady=PADDING_MEDIUM)

folder_Label2 = ttk.Label(tab4_frame, text=folderpath2.get(), **label_style)
folder_Label2.grid(column=1, row=1, sticky="EW", padx=PADDING_LARGE, pady=PADDING_MEDIUM)

cte_Folder_Label = ttk.Label(tab4_frame, text=cte_folder.get(), **label_style)
cte_Folder_Label.grid(column=1, row=2, sticky="EW", padx=PADDING_LARGE, pady=PADDING_MEDIUM)

relatorio_bsoft_Label = ttk.Label(tab4_frame, text=relatorio_bsoft.get(), **label_style)
relatorio_bsoft_Label.grid(column=1, row=3, sticky="EW", padx=PADDING_LARGE, pady=PADDING_MEDIUM)

relatorio_target_Label = ttk.Label(tab4_frame, text=relatorio_target.get(), **label_style)
relatorio_target_Label.grid(column=1, row=4, sticky="EW", padx=PADDING_LARGE, pady=PADDING_MEDIUM)

# Initialize Browse objects
browse1 = Browse(cte_Folder_Label)
browse2 = Browse(folder_Label)
browse3 = Browse(folder_Label2)
browse4 = Browse(relatorio_bsoft_Label)
browse5 = Browse(relatorio_target_Label)

# =============================================================================
# ACTION BUTTONS
# =============================================================================

# Button style configuration
button_style = {'width': 20}

# --- TAB 1: CTe Buttons ---
ttk.Button(tab1,
           text="📋 Emitir lista de CTe",
           command=lambda: thread_0.start_thread(
               cte_list, progressbar, arguments=[cal.get_date(),
                                                 cal_f.get_date(),
                                                 folderpath.get(),
                                                 cte_folder.get(),
                                                 root]
           )).grid(column=2, row=0, rowspan=2, padx=PADDING_MEDIUM, pady=15, ipady=15, sticky="EW")

# --- TAB 3: Utilitários Buttons ---
ttk.Button(tab3,
           text="🧹 Limpar CTe simbólico",
           command=lambda: thread_0.start_thread(
               clear_cte_number, progressbar3, arguments=[cal.get_date(),
                                                          cal_f.get_date(),
                                                          folderpath.get(),
                                                          root]
           )).pack(pady=PADDING_LARGE, padx=PADDING_XL, fill='x', ipady=PADDING_SMALL)

ttk.Button(tab3,
           text="🔖 Emitir CTe simbólico",
           command=lambda: thread_0.start_thread(
               cte_symbolic, progressbar3, arguments=[cal.get_date(),
                                                      cal_f.get_date(),
                                                      folderpath.get(),
                                                      cte_folder.get(),
                                                      root]
           )).pack(pady=PADDING_LARGE, padx=PADDING_XL, fill='x', ipady=PADDING_SMALL)

ttk.Button(tab3,
           text="✅ Validar e Cancelar CTes (B-soft)",
           command=lambda: thread_0.start_thread(
               validar_e_cancelar_ctes, progressbar3, arguments=[relatorio_bsoft.get(),
                                                                  root]
           )).pack(pady=PADDING_LARGE, padx=PADDING_XL, fill='x', ipady=PADDING_SMALL)

ttk.Button(tab3,
           text="📊 Comparar GNRE (B-soft vs Target)",
           command=lambda: thread_0.start_thread(
               comparar_gnre_target, progressbar3, arguments=[relatorio_bsoft.get(),
                                                               relatorio_target.get(),
                                                               root]
           )).pack(pady=PADDING_LARGE, padx=PADDING_XL, fill='x', ipady=PADDING_SMALL)

ttk.Button(tab3,
           text="💰 Processar GNRE Target",
           command=lambda: thread_0.start_thread(
               processar_gnre_target, progressbar3, arguments=[relatorio_target.get(),
                                                                root]
           )).pack(pady=PADDING_LARGE, padx=PADDING_XL, fill='x', ipady=PADDING_SMALL)

ttk.Button(tab1,
           text="✉️ Emitir",
           command=lambda: thread_0.start_thread(
               cte_unique, progressbar, arguments=[cal.get_date(),
                                                   folderpath.get(),
                                                   cte_folder.get(),
                                                   cte_type.get(),
                                                   cte_s.get(),
                                                   volumes.get(),
                                                   root]
           )).grid(column=2, row=2, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky="NSEW")

ttk.Button(tab2_frame,
           text="✉️ Emitir",
           command=lambda: thread_1.start_thread(
               cte_complimentary, progressbar2, arguments=[cal1.get_date(),
                                                           cal2.get_date(),
                                                           folderpath2.get(),
                                                           cte_folder.get(),
                                                           root]
           )).grid(column=2, row=0, rowspan=2, padx=PADDING_MEDIUM, pady=15, ipady=15, sticky="EW")

ttk.Button(tab2_frame2,
           text="✉️ Emitir Avulso",
           command=lambda: thread_1.start_thread(
               cte_complimentary, progressbar2, arguments=[cal1.get_date(),
                                                           cal2.get_date(),
                                                           folderpath2.get(),
                                                           cte_folder.get(),
                                                           root,
                                                           True,
                                                           cte_cs.get()]
           )).grid(column=2, row=0, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky="NSEW")

ttk.Button(tab2_1,
           text="🗺️ Emitir CTes por UF",
           command=lambda: thread_2.start_thread(
               cte_list_grouped, progressbar4, arguments=[cal1_uf.get_date(),
                                                            cal2_uf.get_date(),
                                                            folderpath.get(),
                                                            cte_folder.get(),
                                                            root]
           )).grid(column=2, row=0, rowspan=2, padx=PADDING_MEDIUM, pady=15, ipady=15, sticky="EW")

ttk.Button(tab5,
           text="❌ Cancelar lista de CTe",
           command=lambda: thread_2.start_thread(
               cte_cancel_batch, progressbar5, arguments=[cal1_cancel.get_date(),
                                                          cal2_cancel.get_date(),
                                                          root]
           )).grid(column=2, row=0, rowspan=2, padx=PADDING_MEDIUM, pady=15, ipady=15, sticky="EW")

ttk.Button(tab5,
           text="❌ Cancelar Avulso",
           command=lambda: thread_2.start_thread(
               cancelar_avulso_cte, progressbar5, arguments=[cte_cancel.get(),
                                                             protocolo_cancel.get(),
                                                             root]
           )).grid(column=2, row=2, rowspan=2, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky="NSEW")

# --- TAB 4: Arquivos Buttons ---
ttk.Button(tab4_frame,
           text="📂 Procurar",
           command=lambda: browse2.browse_folder(folderpath, 'folderpath.txt', master=tab4_frame,
                                                 label_config={'wraplength': 180, 'width': 25},
                                                 grid_config={'column': 1, 'row': 0, 'sticky': "EW", 'padx': PADDING_LARGE,
                                                              'pady': PADDING_MEDIUM})
           ).grid(column=2, row=0, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky="EW")

ttk.Button(tab4_frame,
           text="📂 Procurar",
           command=lambda: browse3.browse_folder(folderpath2, 'folderpath2.txt', master=tab4_frame,
                                                 label_config={'wraplength': 180, 'width': 25},
                                                 grid_config={'column': 1, 'row': 1, 'sticky': "EW", 'padx': PADDING_LARGE,
                                                              'pady': PADDING_MEDIUM})
           ).grid(column=2, row=1, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky="EW")

ttk.Button(tab4_frame,
           text="📂 Procurar",
           command=lambda: browse1.browse_folder(cte_folder, 'filename.txt', master=tab4_frame,
                                                 label_config={'wraplength': 180, 'width': 25},
                                                 grid_config={'column': 1, 'row': 2, 'sticky': "EW", 'padx': PADDING_LARGE,
                                                              'pady': PADDING_MEDIUM})
           ).grid(column=2, row=2, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky="EW")

ttk.Button(tab4_frame,
           text="📄 Procurar",
           command=lambda: browse4.browse_files(relatorio_bsoft, 'relatorio_bsoft.txt', master=tab4_frame,
                                                label_config={'wraplength': 180, 'width': 25},
                                                grid_config={'column': 1, 'row': 3, 'sticky': "EW", 'padx': PADDING_LARGE,
                                                             'pady': PADDING_MEDIUM})
           ).grid(column=2, row=3, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky="EW")

ttk.Button(tab4_frame,
           text="📄 Procurar",
           command=lambda: browse5.browse_files(relatorio_target, 'relatorio_target.txt', master=tab4_frame,
                                                label_config={'wraplength': 180, 'width': 25},
                                                grid_config={'column': 1, 'row': 4, 'sticky': "EW", 'padx': PADDING_LARGE,
                                                             'pady': PADDING_MEDIUM})
           ).grid(column=2, row=4, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky="EW")

# =============================================================================
# LABELS
# =============================================================================

# Tab 1 - CTe Labels
ttk.Label(tab1, text="📅 Data inicial:", font=('Segoe UI', 9)).grid(
    column=0, row=0, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')
ttk.Label(tab1, text="📅 Data final:", font=('Segoe UI', 9)).grid(
    column=0, row=1, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')
ttk.Label(tab1, text="📦 CTe avulso:", font=('Segoe UI', 9)).grid(
    column=0, row=2, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')
ttk.Label(tab1, text="📦 Volumes:", font=('Segoe UI', 9)).grid(
    column=2, row=5, padx=PADDING_SMALL, pady=PADDING_MEDIUM, sticky='WE')

# Tab 2 - CTe Complementar Labels
ttk.Label(tab2_frame, text="📅 Data Inicial:", font=('Segoe UI', 9)).grid(
    column=0, row=0, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')
ttk.Label(tab2_frame, text="📅 Data Final:", font=('Segoe UI', 9)).grid(
    column=0, row=1, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')

ttk.Label(tab2_frame2, text="📦 CTe avulso:", font=('Segoe UI', 9)).grid(
    column=0, row=0, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')

# Tab 2_1 - CTe por UF Labels
ttk.Label(tab2_1, text="📅 Data inicial:", font=('Segoe UI', 9)).grid(
    column=0, row=0, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')
ttk.Label(tab2_1, text="📅 Data final:", font=('Segoe UI', 9)).grid(
    column=0, row=1, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')

# Tab 5 - Cancelamento Labels
ttk.Label(tab5, text="📅 Data inicial:", font=('Segoe UI', 9)).grid(
    column=0, row=0, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')
ttk.Label(tab5, text="📅 Data final:", font=('Segoe UI', 9)).grid(
    column=0, row=1, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')
ttk.Label(tab5, text="📦 CTe avulso:", font=('Segoe UI', 9)).grid(
    column=0, row=2, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')
ttk.Label(tab5, text="📝 Protocolo:", font=('Segoe UI', 9)).grid(
    column=0, row=3, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='WE')

# Tab 4 - Arquivos Labels
ttk.Label(tab4_frame, text="📁 CTe normal:", font=('Segoe UI', 9, 'bold')).grid(
    column=0, row=0, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky='W')
ttk.Label(tab4_frame, text="📁 CTe complementar:", font=('Segoe UI', 9, 'bold')).grid(
    column=0, row=1, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky='W')
ttk.Label(tab4_frame, text="📂 Pasta CTE:", font=('Segoe UI', 9, 'bold')).grid(
    column=0, row=2, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky='W')
ttk.Label(tab4_frame, text="📊 Relatório B-soft:", font=('Segoe UI', 9, 'bold')).grid(
    column=0, row=3, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky='W')
ttk.Label(tab4_frame, text="📊 Relatório Target:", font=('Segoe UI', 9, 'bold')).grid(
    column=0, row=4, padx=PADDING_SMALL, pady=PADDING_SMALL, sticky='W')

# =============================================================================
# ENTRY FIELDS
# =============================================================================

# Initialize string variables
cte_s = StringVar()
cte_cs = StringVar()
cte_cancel = StringVar()
protocolo_cancel = StringVar()
volumes = StringVar()
volumes.set('1')  # Default value

# Tab 1 - CTe Entry
ttk.Entry(tab1, textvariable=cte_s, width=16, font=('Segoe UI', 9)).grid(
    column=1, row=2, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky='EW')

ttk.Entry(tab1, textvariable=volumes, width=8, font=('Segoe UI', 9)).grid(
    column=2, row=5, pady=PADDING_SMALL, padx=PADDING_SMALL, sticky='W')

# Tab 2 - CTe Complementar Entry
ttk.Entry(tab2_frame2, textvariable=cte_cs, width=16, font=('Segoe UI', 9)).grid(
    column=1, row=0, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky='EW')

# Tab 5 - Cancelamento Entries
ttk.Entry(tab5, textvariable=cte_cancel, width=16, font=('Segoe UI', 9)).grid(
    column=1, row=2, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky='EW')

ttk.Entry(tab5, textvariable=protocolo_cancel, width=16, font=('Segoe UI', 9)).grid(
    column=1, row=3, padx=PADDING_XL, pady=PADDING_MEDIUM, sticky='EW')

# =============================================================================
# RADIO BUTTONS
# =============================================================================

cte_type = IntVar()
cte_type.set(0)  # Default to Normal

ttk.Radiobutton(tab1, text="📦 Normal", value=0, variable=cte_type).grid(
    column=0, row=5, padx=PADDING_LARGE, pady=PADDING_MEDIUM, sticky='W')
ttk.Radiobutton(tab1, text="🔖 Simbólico", value=1, variable=cte_type).grid(
    column=1, row=5, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, sticky='W')

# =============================================================================
# PROGRESS BARS
# =============================================================================

# Create progress bars with consistent styling
progressbar = ttk.Progressbar(tab1, mode='indeterminate', length=300)
progressbar.grid(column=0, row=6, sticky='WE', columnspan=3, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM)

progressbar2 = ttk.Progressbar(tab2, mode='indeterminate', length=300)
progressbar2.pack(side=BOTTOM, fill='x', padx=PADDING_MEDIUM, pady=PADDING_MEDIUM)

progressbar3 = ttk.Progressbar(tab3, mode='indeterminate', length=300)
progressbar3.pack(side=BOTTOM, fill='x', padx=PADDING_MEDIUM, pady=PADDING_MEDIUM)

progressbar4 = ttk.Progressbar(tab2_1, mode='indeterminate', length=300)
progressbar4.grid(column=0, row=6, sticky='WE', columnspan=3, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM)

progressbar5 = ttk.Progressbar(tab5, mode='indeterminate', length=300)
progressbar5.grid(column=0, row=6, sticky='WE', columnspan=3, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM)


# =============================================================================
# GRID CONFIGURATION FOR RESPONSIVE LAYOUT
# =============================================================================

# Configure Tab 1 (CTe) - Auto resize
tab1.rowconfigure(2, weight=2)
tab1.rowconfigure(6, weight=1)
for col in range(3):
    tab1.columnconfigure(col, weight=1)

# Configure Tab 2_1 (CTe por UF) - Auto resize
tab2_1.rowconfigure(2, weight=2)
tab2_1.rowconfigure(6, weight=1)
for col in range(3):
    tab2_1.columnconfigure(col, weight=1)

# Configure Tab 3 (Utilitários) - Auto resize
for row in range(5):
    tab3.rowconfigure(row, weight=1)
tab3.columnconfigure(0, weight=1)

# Configure Tab 4 (Arquivos) - Auto resize
for row in range(5):
    tab4_frame.rowconfigure(row, weight=1)
for col in range(3):
    tab4_frame.columnconfigure(col, weight=1)

# Configure Tab 5 (Cancelamento) - Auto resize
tab5.rowconfigure(2, weight=2)
tab5.rowconfigure(6, weight=1)
for col in range(3):
    tab5.columnconfigure(col, weight=1)


# =============================================================================
# START APPLICATION
# =============================================================================

if __name__ == '__main__':
    # Center window on screen
    root.update_idletasks()
    width = root.winfo_width()
    height = root.winfo_height()
    x = (root.winfo_screenwidth() // 2) - (width // 2)
    y = (root.winfo_screenheight() // 2) - (height // 2)
    root.geometry(f'+{x}+{y}')
    
    # Start the GUI event loop
    root.mainloop()
