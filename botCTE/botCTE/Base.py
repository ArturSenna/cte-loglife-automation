"""
CTe LogLife - Main Application Interface
Modern redesigned UI for CTe management system
"""

# Standard library imports
import csv
import ctypes.wintypes
import datetime as dt
import os
import sys
import threading
import time
from json import loads
from tkinter import *
from tkinter import filedialog as fd
from tkinter import Toplevel
from tkinter import ttk
from tkinter import messagebox

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

# Update system imports
try:
    from version_checker import VersionChecker
    from auto_updater import AutoUpdater, UpdateManager
except ImportError:
    VersionChecker = None
    AutoUpdater = None
    UpdateManager = None


# =============================================================================
# CONSTANTS AND CONFIGURATION
# =============================================================================

# Get the directory where this script is located
# For PyInstaller bundles, use _MEIPASS to access bundled resources
if getattr(sys, 'frozen', False):
    # Running as compiled executable with PyInstaller
    _SCRIPT_DIR = getattr(sys, '_MEIPASS', os.path.dirname(sys.executable))
else:
    # Running as script
    _SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

# Read application version
# Support both development and PyInstaller bundled environments
def get_app_version():
    """Read version from VERSION file, supporting both dev and bundled environments."""
    version_locations = []
    
    # When running as PyInstaller bundle, try _MEIPASS first
    if getattr(sys, 'frozen', False):
        # Running as compiled executable
        bundle_dir = getattr(sys, '_MEIPASS', os.path.dirname(sys.executable))
        version_locations.append(os.path.join(bundle_dir, 'VERSION'))
        version_locations.append(os.path.join(os.path.dirname(sys.executable), 'VERSION'))
    
    # Development environment locations
    version_locations.append(os.path.join(os.path.dirname(_SCRIPT_DIR), 'VERSION'))
    version_locations.append(os.path.join(_SCRIPT_DIR, 'VERSION'))
    
    # Try each location
    for version_file in version_locations:
        if os.path.exists(version_file):
            try:
                # Try UTF-8 first (standard encoding)
                with open(version_file, 'r', encoding='utf-8') as f:
                    version = f.read().strip()
                    if version:
                        print(f"Version loaded from: {version_file}")
                        return version
            except (UnicodeError, UnicodeDecodeError):
                # Fallback to UTF-16
                try:
                    with open(version_file, 'r', encoding='utf-16') as f:
                        version = f.read().strip()
                        if version:
                            print(f"Version loaded from: {version_file}")
                            return version
                except Exception:
                    pass
            except Exception as e:
                print(f"Could not read VERSION from {version_file}: {e}")
                continue
    
    print("Warning: VERSION file not found in any expected location")
    return '1.0.0'

APP_VERSION = get_app_version()

APP_TITLE = 'CTe LogLife'
APP_ICON = os.path.join(_SCRIPT_DIR, "my_icon.ico")
WINDOW_SIZE = ""
THEME_NAME = 'breeze'

# Update configuration
GITHUB_REPO_OWNER = 'ArturSenna'
GITHUB_REPO_NAME = 'cte-loglife-automation'

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
        # Try UTF-8 first
        with open(filename, 'r', encoding='utf-8') as f:
            text = f.read()
        lines = text.split('\n')
        return lines[0] if lines else default_value
    except UnicodeDecodeError:
        # If UTF-8 fails, try Windows-1252 (common in Windows)
        try:
            with open(filename, 'r', encoding='windows-1252') as f:
                text = f.read()
            lines = text.split('\n')
            return lines[0] if lines else default_value
        except Exception:
            return default_value
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


def check_for_updates_ui(parent_window, show_up_to_date=True):
    """
    Check for updates and show dialog.
    
    Args:
        parent_window: Parent Tkinter window
        show_up_to_date: Whether to show message when already up to date
    """
    if not VersionChecker:
        messagebox.showinfo("Atualização", "Sistema de atualização não disponível.")
        return
    
    try:
        # Create version checker
        checker = VersionChecker(
            repo_owner=GITHUB_REPO_OWNER,
            repo_name=GITHUB_REPO_NAME,
            current_version=APP_VERSION
        )
        
        # Check for updates
        update_info = checker.check_for_updates()
        
        if update_info.get('error'):
            messagebox.showerror(
                "Erro ao Verificar Atualização",
                f"Não foi possível verificar atualizações:\n{update_info['error']}"
            )
            return
        
        if update_info['update_available']:
            show_update_dialog(parent_window, update_info)
        else:
            if show_up_to_date:
                messagebox.showinfo(
                    "Atualização",
                    f"Você está usando a versão mais recente!\n\nVersão atual: {APP_VERSION}"
                )
    except Exception as e:
        messagebox.showerror(
            "Erro",
            f"Erro ao verificar atualizações:\n{str(e)}"
        )


def show_update_dialog(parent_window, update_info):
    """
    Show update available dialog with options to download and install.
    
    Args:
        parent_window: Parent Tkinter window
        update_info: Update information dictionary
    """
    dialog = Toplevel(parent_window)
    dialog.title("Atualização Disponível")
    dialog.transient(parent_window)
    dialog.grab_set()
    
    # Use grid geometry manager for better control over layout
    dialog.columnconfigure(0, weight=1)
    dialog.rowconfigure(1, weight=1)  # Content row expands
    
    # Header frame (row 0)
    header_frame = Frame(dialog, bg=PRIMARY_COLOR, height=80)
    header_frame.grid(row=0, column=0, sticky='ew')
    header_frame.pack_propagate(False)
    
    Label(
        header_frame,
        text="🎉 Nova Versão Disponível!",
        font=('Segoe UI', 16, 'bold'),
        bg=PRIMARY_COLOR,
        fg='white'
    ).pack(pady=20)
    
    # Content frame (row 1 - expandable)
    content_frame = Frame(dialog, bg='white', padx=20, pady=20)
    content_frame.grid(row=1, column=0, sticky='nsew')
    content_frame.columnconfigure(0, weight=1)
    content_frame.rowconfigure(2, weight=1)  # Notes frame row expands
    
    # Version info
    version_frame = Frame(content_frame, bg='white')
    version_frame.grid(row=0, column=0, sticky='ew', pady=10)
    
    Label(
        version_frame,
        text=f"Versão Atual: {update_info['current_version']}",
        font=('Segoe UI', 10),
        bg='white'
    ).pack(anchor='w')
    
    Label(
        version_frame,
        text=f"Nova Versão: {update_info['latest_version']}",
        font=('Segoe UI', 10, 'bold'),
        bg='white',
        fg=PRIMARY_COLOR
    ).pack(anchor='w')
    
    # Release notes label
    Label(
        content_frame,
        text="Novidades:",
        font=('Segoe UI', 10, 'bold'),
        bg='white'
    ).grid(row=1, column=0, sticky='w', pady=(10, 5))
    
    # Scrollable text for release notes
    notes_frame = Frame(content_frame, bg='white')
    notes_frame.grid(row=2, column=0, sticky='nsew', pady=5)
    notes_frame.columnconfigure(0, weight=1)
    notes_frame.rowconfigure(0, weight=1)
    
    scrollbar = Scrollbar(notes_frame)
    scrollbar.grid(row=0, column=1, sticky='ns')
    
    notes_text = Text(
        notes_frame,
        wrap=WORD,
        height=8,
        font=('Segoe UI', 9),
        yscrollcommand=scrollbar.set,
        relief='sunken',
        borderwidth=1
    )
    notes_text.grid(row=0, column=0, sticky='nsew')
    scrollbar.config(command=notes_text.yview)
    
    notes_text.insert('1.0', update_info.get('release_notes', 'Sem notas de versão disponíveis.'))
    notes_text.config(state='disabled')
    
    # Progress frame (row 3 - for progress bar and status, inside content)
    progress_frame = Frame(content_frame, bg='white')
    progress_frame.grid(row=3, column=0, sticky='ew', pady=(10, 0))
    progress_frame.columnconfigure(0, weight=1)
    
    progress_var = DoubleVar(value=0)
    progress_bar = ttk.Progressbar(
        progress_frame,
        mode='determinate',
        variable=progress_var,
        maximum=100
    )
    # Progress bar will be packed when needed
    
    status_label = Label(
        progress_frame,
        text="",
        font=('Segoe UI', 9),
        bg='white',
        fg='gray'
    )
    # Status label will be packed when needed
    
    # Button frame (row 2 - fixed at bottom)
    button_frame = Frame(dialog, bg='white', pady=15)
    button_frame.grid(row=2, column=0, sticky='ew')
    
    def start_update():
        """Start the update download and installation."""
        if not update_info.get('download_url'):
            messagebox.showerror(
                "Erro",
                "URL de download não disponível.\nPor favor, visite o site para baixar manualmente."
            )
            return
        
        # Disable buttons and show progress
        download_btn.config(state='disabled')
        later_btn.config(state='disabled')
        progress_bar.grid(row=0, column=0, sticky='ew', pady=(0, 5))
        status_label.grid(row=1, column=0, sticky='ew')
        status_label.config(text="Baixando atualização...")
        
        def progress_callback(progress):
            """Update progress bar."""
            progress_var.set(progress)
            status_label.config(text=f"Baixando... {progress}%")
            dialog.update_idletasks()
        
        def download_complete(installer_path):
            """Handle download completion."""
            if installer_path:
                status_label.config(text="Download concluído! Iniciando instalação...")
                dialog.update_idletasks()
                
                # Small delay to show completion
                dialog.after(1000, lambda: install_update(installer_path))
            else:
                status_label.config(text="Erro no download!")
                messagebox.showerror(
                    "Erro",
                    "Falha ao baixar a atualização.\nTente novamente mais tarde."
                )
                download_btn.config(state='normal')
                later_btn.config(state='normal')
        
        def install_update(installer_path):
            """Install the update."""
            dialog.destroy()
            
            response = messagebox.askyesno(
                "Instalar Atualização",
                "A atualização será instalada agora.\n\n"
                "O aplicativo será fechado durante a instalação.\n\n"
                "Deseja continuar?"
            )
            
            if response:
                updater = AutoUpdater(download_url=update_info['download_url'])
                updater.installer_path = installer_path
                success = updater.install_update(silent=False)
                
                if success:
                    # Exit application
                    import sys
                    sys.exit(0)
        
        # Start download in background
        updater = AutoUpdater(download_url=update_info['download_url'])
        updater.set_download_callback(progress_callback)
        updater.download_in_background(download_complete)
    
    # Buttons
    download_btn = Button(
        button_frame,
        text="⬇️ Baixar e Instalar",
        command=start_update,
        bg=PRIMARY_COLOR,
        fg='white',
        font=('Segoe UI', 10, 'bold'),
        relief='flat',
        padx=20,
        pady=8,
        cursor='hand2'
    )
    download_btn.pack(side=RIGHT, padx=(5, 20))
    
    later_btn = Button(
        button_frame,
        text="Mais Tarde",
        command=dialog.destroy,
        bg='#E0E0E0',
        fg='black',
        font=('Segoe UI', 10),
        relief='flat',
        padx=20,
        pady=8,
        cursor='hand2'
    )
    later_btn.pack(side=RIGHT, padx=5)
    
    # Set minimum size and center dialog after all widgets are created
    dialog.update_idletasks()
    
    # Calculate required size based on content
    min_width = 500
    min_height = 420
    
    # Get the actual required size
    req_width = max(dialog.winfo_reqwidth(), min_width)
    req_height = max(dialog.winfo_reqheight(), min_height)
    
    # Set geometry with minimum size
    dialog.minsize(min_width, min_height)
    dialog.geometry(f"{req_width}x{req_height}")
    
    # Center dialog on screen
    dialog.update_idletasks()
    x = (dialog.winfo_screenwidth() // 2) - (req_width // 2)
    y = (dialog.winfo_screenheight() // 2) - (req_height // 2)
    dialog.geometry(f"{req_width}x{req_height}+{x}+{y}")


def check_for_updates_on_startup(parent_window):
    """
    Check for updates on application startup (non-intrusive).
    
    Args:
        parent_window: Parent Tkinter window
    """
    def check_updates():
        try:
            if not VersionChecker:
                return
            
            checker = VersionChecker(
                repo_owner=GITHUB_REPO_OWNER,
                repo_name=GITHUB_REPO_NAME,
                current_version=APP_VERSION
            )
            
            update_info = checker.check_for_updates()
            
            if update_info.get('update_available') and not update_info.get('error'):
                # Schedule UI update on main thread
                parent_window.after(0, lambda: show_update_dialog(parent_window, update_info))
        except:
            pass  # Silently fail on startup check
    
    # Run check in background thread
    thread = threading.Thread(target=check_updates, daemon=True)
    thread.start()


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

# Create menu bar
menubar = Menu(root)
root.config(menu=menubar)

# Help menu
help_menu = Menu(menubar, tearoff=0)
menubar.add_cascade(label="Ajuda", menu=help_menu)
help_menu.add_command(
    label="Verificar Atualizações",
    command=lambda: check_for_updates_ui(root, show_up_to_date=True)
)
help_menu.add_separator()
help_menu.add_command(
    label=f"Sobre {APP_TITLE}",
    command=lambda: messagebox.showinfo(
        f"Sobre {APP_TITLE}",
        f"{APP_TITLE}\nVersão {APP_VERSION}\n\n"
        "Automação de emissão de CTe's\n"
        "Desenvolvido com BotCity Framework"
    )
)

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
           )).grid(column=2, row=0, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, ipady=10, sticky="EW")

ttk.Button(tab1,
           text="🔗 Emitir CTe Unificado",
           command=lambda: thread_0.start_thread(
               cte_list_unified, progressbar, arguments=[cal.get_date(),
                                                         cal_f.get_date(),
                                                         folderpath.get(),
                                                         cte_folder.get(),
                                                         root]
           )).grid(column=2, row=1, padx=PADDING_MEDIUM, pady=PADDING_MEDIUM, ipady=10, sticky="EW")

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
           text="💰 Lançar guias de ICMS",
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
    
    # Check for updates on startup (non-intrusive, runs in background)
    check_for_updates_on_startup(root)
    
    # Start the GUI event loop
    root.mainloop()
