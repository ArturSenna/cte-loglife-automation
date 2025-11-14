# CTe LogLife - Plano de Refatoração e Modernização

**Versão:** 1.0  
**Data:** 14 de Novembro de 2025  
**Status:** Em Progresso

---

## 📋 Índice

- [Resumo Executivo](#resumo-executivo)
- [Progresso Atual](#progresso-atual)
- [Próximos Passos](#próximos-passos)
  - [Curto Prazo (Semanas 2-3)](#curto-prazo-semanas-2-3)
  - [Médio Prazo (Mês 1)](#médio-prazo-mês-1)
  - [Longo Prazo (Mês 2+)](#longo-prazo-mês-2)
- [Guias Detalhados](#guias-detalhados)
- [Checklist de Implementação](#checklist-de-implementação)

---

## 📊 Resumo Executivo

Este documento descreve o plano completo de refatoração do projeto CTe LogLife para modernizá-lo seguindo as melhores práticas de desenvolvimento Python. O objetivo é tornar o código mais manutenível, testável, seguro e escalável.

### Objetivos Principais

1. ✅ **Organização de Projeto** - Estrutura de diretórios clara e lógica
2. ✅ **Gestão de Dependências** - Dependências consolidadas e documentadas
3. ✅ **Segurança** - Credenciais em variáveis de ambiente
4. 🔄 **Modularização** - Código dividido em módulos coesos e reutilizáveis
5. 🔄 **Testes** - Cobertura de testes adequada
6. 🔄 **Qualidade de Código** - Formatação, linting e type hints
7. 🔄 **Documentação** - Código e APIs bem documentados

---

## ✅ Progresso Atual

### Concluído (Semana 1)

#### 1. Atualização do `.gitignore`

**Status:** ✅ Concluído

**O que foi feito:**

- Reorganizado em seções lógicas (Python, Virtual Environments, Distribution, IDEs, etc.)
- Adicionado exclusão de arquivos de configuração temporários (`.txt`)
- Corrigido typo `__pychache` → `__pycache__`
- Adicionado exclusão de logs, screenshots, e relatórios

**Impacto:**

- Repositório mais limpo
- Prevenção de commit de arquivos sensíveis
- Melhor colaboração em equipe

---

#### 2. Consolidação de `requirements.txt`

**Status:** ✅ Concluído (com ajustes de compatibilidade)

**O que foi feito:**

- Mescladas as duas versões de `requirements.txt` (raiz e `botCTE/`)
- Resolvidos conflitos de versões entre dependências
- Organizado em seções com comentários
- Ajustados para compatibilidade:
  - `PyScreeze==0.1.27` (compatível com botcity-framework-core)
  - `google-api-python-client==1.6.7` (compatível com df2gspread)
  - `uritemplate==3.0.1` (compatível com google-api-python-client)

**Dependências Principais:**

```
botcity-framework-core==0.3.0
pandas==1.5.0
numpy==1.23.3
selenium==4.4.3
python-dotenv==0.21.0
```

**Impacto:**

- Uma única fonte de verdade para dependências
- Instalação mais rápida e confiável
- Menos conflitos de versão

---

#### 3. Criação do `pyproject.toml`

**Status:** ✅ Concluído

**O que foi feito:**

- Arquivo de configuração moderna seguindo PEP 621
- Metadados do projeto (nome, versão, autor, licença)
- Dependências organizadas com ranges de versão
- Dependências opcionais separadas:
  - `[dev]` - ferramentas de desenvolvimento
  - `[build]` - ferramentas de build
- Configuração de ferramentas de qualidade:
  - Black (formatador)
  - isort (organizador de imports)
  - pytest (framework de testes)
  - mypy (type checker)

**Impacto:**

- Instalação moderna: `pip install -e .`
- Configuração centralizada de todas as ferramentas
- Pronto para publicação no PyPI (futuro)

---

#### 4. README.md Abrangente

**Status:** ✅ Concluído

**O que foi feito:**

- Documentação completa com 9 seções principais
- Guia de instalação passo a passo
- Instruções de configuração detalhadas
- Exemplos de uso (GUI e programático)
- Estrutura do projeto explicada
- Guia de desenvolvimento e contribuição
- Instruções de build com PyInstaller

**Impacto:**

- Onboarding mais rápido para novos desenvolvedores
- Documentação profissional
- Redução de perguntas sobre instalação/uso

---

#### 5. Template `.env.example`

**Status:** ✅ Concluído

**O que foi feito:**

- Template completo de variáveis de ambiente
- Organizado em seções:
  - Caminhos de pastas
  - Relatórios
  - Configuração BSoft
  - Google Sheets
  - Configurações da aplicação
  - Configurações de automação
- Comentários explicativos para cada variável
- Avisos de segurança

**Impacto:**

- Configuração mais fácil para novos usuários
- Separação clara entre config e código
- Segurança melhorada (sem credenciais hardcoded)

---

#### 6. Refatoração de Credenciais

**Status:** ✅ Concluído

**O que foi feito:**

- Removidas credenciais hardcoded de `functions.py`
- Implementado carregamento de `.env` com `python-dotenv`
- Validação de variáveis de ambiente obrigatórias
- Mensagens de erro claras se credenciais ausentes

**Arquivo modificado:** `botCTE/botCTE/functions.py`

**Antes:**

```python
self.headers = {"xtoken": "myqhF6Nbzx"}
details = {"email": "ARTURSENNA@...", "password": "..."}
```

**Depois:**

```python
xtoken = os.getenv("LOGLIFE_XTOKEN")
email = os.getenv("LOGLIFE_USER")
password = os.getenv("LOGLIFE_PASSWORD")

if not all([xtoken, email, password]):
    raise ValueError("Missing required environment variables...")
```

**Impacto:**

- ✅ Credenciais seguras
- ✅ Fácil rotação de senhas
- ✅ Configuração por ambiente (dev/prod)

---

## 🔄 Próximos Passos

### Curto Prazo (Semanas 2-3)

#### 1. Reestruturação de Diretórios

**Prioridade:** 🔴 Alta  
**Tempo Estimado:** 4-6 horas  
**Status:** ⏳ Pendente

**Objetivo:** Reorganizar a estrutura do projeto para seguir convenções Python modernas.

**Estrutura Atual (Problemática):**

```
botCityCTE/
├── botCTE/
│   └── botCTE/  # ← Aninhamento desnecessário
│       ├── Base.py  # ← Nome não segue PEP 8
│       ├── emissions.py  # ← 2,671 linhas!
│       └── ...
```

**Estrutura Proposta:**

```
botCityCTE/
├── src/
│   └── botcte/              # Nome em lowercase
│       ├── __init__.py
│       ├── __main__.py
│       │
│       ├── core/            # Lógica de negócio
│       │   ├── __init__.py
│       │   ├── bot.py
│       │   └── emissions/
│       │       ├── __init__.py
│       │       ├── normal.py        # cte_list()
│       │       ├── complementary.py # cte_complimentary()
│       │       ├── address.py       # Funções get_address_*
│       │       └── validators.py
│       │
│       ├── ui/              # Interface gráfica
│       │   ├── __init__.py
│       │   ├── main_window.py  # Base.py refatorado
│       │   ├── components.py
│       │   ├── styles.py
│       │   └── dialogs.py
│       │
│       ├── utils/           # Utilitários
│       │   ├── __init__.py
│       │   ├── file_browser.py  # Browse class
│       │   ├── threading.py     # Start class
│       │   └── api_client.py    # RequestDataFrame
│       │
│       ├── config/          # Configuração
│       │   ├── __init__.py
│       │   └── settings.py
│       │
│       └── data/            # Dados estáticos
│           ├── Alíquota.xlsx
│           └── Complementares.xlsx
│
├── tests/                   # Testes unitários
│   ├── __init__.py
│   ├── conftest.py
│   ├── test_emissions.py
│   └── test_api_client.py
│
├── docs/                    # Documentação
│   ├── api.md
│   ├── setup.md
│   └── architecture.md
│
├── config/                  # Arquivos de config runtime
├── logs/                    # Logs da aplicação
├── outputs/                 # Relatórios gerados
└── scripts/                 # Scripts auxiliares
```

**Passos de Implementação:**

1. **Criar nova estrutura de diretórios:**

   ```powershell
   mkdir src\botcte\core\emissions
   mkdir src\botcte\ui
   mkdir src\botcte\utils
   mkdir src\botcte\config
   mkdir src\botcte\data
   mkdir tests
   mkdir docs
   mkdir logs
   mkdir outputs
   mkdir scripts
   ```

2. **Mover arquivos existentes:**

   ```powershell
   # Mover bot.py
   mv botCTE\botCTE\bot.py src\botcte\core\

   # Mover Base.py (renomear)
   mv botCTE\botCTE\Base.py src\botcte\ui\main_window.py

   # Mover dados
   mv botCTE\botCTE\*.xlsx src\botcte\data\
   mv botCTE\botCTE\resources src\botcte\
   ```

3. **Atualizar imports em todos os arquivos**

4. **Atualizar `pyproject.toml`:**
   ```toml
   [tool.setuptools]
   packages = ["botcte"]
   package-dir = {"" = "src"}
   ```

**Impacto:**

- ✅ Estrutura clara e profissional
- ✅ Fácil navegação no código
- ✅ Separação de responsabilidades
- ✅ Pronto para crescimento

---

#### 2. Divisão do `emissions.py` (2,671 linhas)

**Prioridade:** 🔴 Alta  
**Tempo Estimado:** 8-10 horas  
**Status:** ⏳ Pendente

**Problema:** Arquivo monolítico impossível de manter e testar.

**Solução:** Dividir em módulos por funcionalidade.

**Módulos Propostos:**

##### A. `core/emissions/normal.py`

**Responsabilidade:** Emissão de CTe normais

**Funções principais:**

- `cte_list()` - Função principal
- Funções auxiliares extraídas das nested functions

**Tamanho estimado:** ~800 linhas

---

##### B. `core/emissions/complementary.py`

**Responsabilidade:** Emissão de CTe complementares

**Funções principais:**

- `cte_complimentary()` - Função principal
- `get_cte_number()`
- `hiae_additional_cost()`
- `hiae_dry_ice_cost()`

**Tamanho estimado:** ~900 linhas

---

##### C. `core/emissions/address.py`

**Responsabilidade:** Manipulação de dados de endereço

**Funções principais:**

```python
def get_address_name(address_id: Union[int, List[int]]) -> str:
    """Retorna nome de trading da empresa dado o ID."""

def get_address_cnpj(address_list: List[int]) -> str:
    """Retorna CNPJ formatado de endereços."""

def get_address_city(address_id: int) -> str:
    """Retorna cidade do endereço."""

def get_address_city_listed(address_list: List[int]) -> str:
    """Retorna cidades de múltiplos endereços."""
```

**Tamanho estimado:** ~300 linhas

---

##### D. `core/emissions/validators.py`

**Responsabilidade:** Validação de dados

**Funções principais:**

```python
def validate_cnpj(cnpj: str) -> bool:
    """Valida formato de CNPJ."""

def validate_date_range(start: str, end: str) -> bool:
    """Valida range de datas."""

def validate_protocol(protocol: str) -> bool:
    """Valida número de protocolo."""
```

**Tamanho estimado:** ~200 linhas

---

##### E. `core/emissions/__init__.py`

**Responsabilidade:** Exports públicos

```python
"""
Módulo de emissão de CTe.

Funções principais:
- cte_list: Emissão de CTe normais
- cte_complimentary: Emissão de CTe complementares
"""

from .normal import cte_list
from .complementary import cte_complimentary
from .address import (
    get_address_name,
    get_address_cnpj,
    get_address_city,
)
from .validators import validate_cnpj, validate_date_range

__all__ = [
    "cte_list",
    "cte_complimentary",
    "get_address_name",
    "get_address_cnpj",
    "get_address_city",
    "validate_cnpj",
    "validate_date_range",
]
```

**Passos de Implementação:**

1. Criar arquivos vazios
2. Copiar funções para módulos apropriados
3. Extrair nested functions para funções de módulo
4. Adicionar type hints
5. Adicionar docstrings
6. Atualizar imports
7. Testar cada módulo

**Impacto:**

- ✅ Código testável
- ✅ Fácil manutenção
- ✅ Reutilização de código
- ✅ Menor complexidade

---

#### 3. Refatoração do `Base.py` → `ui/main_window.py`

**Prioridade:** 🟡 Média  
**Tempo Estimado:** 6-8 horas  
**Status:** ⏳ Pendente

**Problema:** Arquivo de 588 linhas misturando UI, lógica de negócio e I/O.

**Solução:** Separar em múltiplos arquivos por responsabilidade.

**Estrutura Proposta:**

##### A. `ui/main_window.py`

**Responsabilidade:** Janela principal da aplicação

```python
"""Janela principal do CTe LogLife."""

from tkinter import Tk
from .components import create_date_picker, create_file_browser
from .styles import apply_theme
from ..core.emissions import cte_list, cte_complimentary


class MainWindow:
    """Janela principal da aplicação CTe LogLife."""

    def __init__(self):
        self.root = Tk()
        self.root.title("CTe LogLife")
        apply_theme(self.root)
        self._setup_ui()

    def _setup_ui(self):
        """Configura componentes da UI."""
        # Setup dos widgets
        pass

    def run(self):
        """Inicia o loop principal."""
        self.root.mainloop()
```

---

##### B. `ui/components.py`

**Responsabilidade:** Componentes reutilizáveis de UI

```python
"""Componentes de UI reutilizáveis."""

from tkinter import ttk
from tkcalendar import DateEntry


def create_date_picker(parent, **config):
    """Cria um DateEntry com configuração padrão."""
    return DateEntry(parent, **config)


def create_file_browser(parent, browse_func, **config):
    """Cria um browser de arquivos com label e botão."""
    frame = ttk.Frame(parent)
    # ... implementação
    return frame


def create_progress_bar(parent, **config):
    """Cria uma barra de progresso."""
    return ttk.Progressbar(parent, **config)
```

---

##### C. `ui/styles.py`

**Responsabilidade:** Estilos e temas

```python
"""Configuração de estilos e temas da UI."""

from ttkthemes import ThemedStyle

# Cores
PRIMARY_COLOR = '#00a5e7'
BACKGROUND_COLOR = 'white'
TEXT_COLOR = 'black'

# Padding
PADDING_SMALL = 5
PADDING_MEDIUM = 10
PADDING_LARGE = 20


def apply_theme(root):
    """Aplica tema breeze ao root window."""
    style = ThemedStyle(root)
    style.set_theme('breeze')
    # Configurações adicionais
```

---

##### D. `ui/dialogs.py`

**Responsabilidade:** Diálogos e pop-ups

```python
"""Diálogos e janelas pop-up."""

from tkinter import Toplevel, messagebox


def show_confirmation(parent, message):
    """Mostra diálogo de confirmação."""
    # ... implementação


def show_error(parent, error_message):
    """Mostra diálogo de erro."""
    messagebox.showerror("Erro", error_message)


def show_progress_dialog(parent, task):
    """Mostra diálogo com barra de progresso."""
    # ... implementação
```

**Impacto:**

- ✅ UI mais organizada
- ✅ Componentes reutilizáveis
- ✅ Fácil manutenção de estilos
- ✅ Testes de UI possíveis

---

#### 4. Refatoração do `functions.py` → `utils/`

**Prioridade:** 🟡 Média  
**Tempo Estimado:** 3-4 horas  
**Status:** ⏳ Pendente

**Solução:** Dividir classes em arquivos separados.

##### A. `utils/threading.py`

```python
"""Utilitários para threading."""

import threading
from typing import Callable, Tuple, Optional


class ThreadManager:
    """Gerencia threads da aplicação."""

    def __init__(self, root_master):
        self.master = root_master
        self.thread = None

    def start_thread(
        self,
        target: Callable,
        progress_bar=None,
        args: Tuple = ()
    ):
        """Inicia uma thread daemon."""
        # ... implementação
```

---

##### B. `utils/file_browser.py`

```python
"""Utilitários para seleção de arquivos e pastas."""

from tkinter import filedialog
from typing import Optional


class FileBrowser:
    """Gerencia seleção de arquivos e pastas."""

    def browse_file(
        self,
        title: str = "Selecione o arquivo",
        filetypes: tuple = None
    ) -> str:
        """Abre diálogo de seleção de arquivo."""
        # ... implementação

    def browse_folder(self, title: str = "Selecione a pasta") -> str:
        """Abre diálogo de seleção de pasta."""
        # ... implementação
```

---

##### C. `utils/api_client.py`

```python
"""Cliente para API do Transporte Biológico."""

import os
import requests
import pandas as pd
from typing import Optional, Dict
from dotenv import load_dotenv

load_dotenv()


class APIClient:
    """Cliente para API do Transporte Biológico."""

    def __init__(self):
        """Inicializa cliente com credenciais do .env."""
        self.xtoken = os.getenv("LOGLIFE_XTOKEN")
        self.email = os.getenv("LOGLIFE_USER")
        self.password = os.getenv("LOGLIFE_PASSWORD")

        self._validate_credentials()
        self._authenticate()

    def _validate_credentials(self):
        """Valida se todas as credenciais estão presentes."""
        if not all([self.xtoken, self.email, self.password]):
            raise ValueError(
                "Missing API credentials. Check .env file."
            )

    def _authenticate(self):
        """Autentica e obtém token."""
        # ... implementação

    def get(self, endpoint: str) -> pd.DataFrame:
        """GET request que retorna DataFrame."""
        # ... implementação

    def post(self, endpoint: str, data: Dict) -> pd.DataFrame:
        """POST request que retorna DataFrame."""
        # ... implementação
```

**Impacto:**

- ✅ Código mais organizado
- ✅ Classes com responsabilidade única
- ✅ Fácil testar isoladamente

---

#### 5. Sistema de Configuração Centralizado

**Prioridade:** 🟡 Média  
**Tempo Estimado:** 2-3 horas  
**Status:** ⏳ Pendente

**Objetivo:** Centralizar toda configuração em um único lugar.

##### `config/settings.py`

```python
"""Configurações centralizadas da aplicação."""

import os
from pathlib import Path
from typing import Optional
from dotenv import load_dotenv

# Carrega variáveis de ambiente
load_dotenv()


class Settings:
    """Configurações da aplicação."""

    # Paths
    CTE_FOLDER: Path = Path(os.getenv("CTE_FOLDER", ""))
    FOLDERPATH_NORMAL: Path = Path(os.getenv("FOLDERPATH_NORMAL", ""))
    FOLDERPATH_COMPLEMENTAR: Path = Path(os.getenv("FOLDERPATH_COMPLEMENTAR", ""))

    # Relatórios
    RELATORIO_BSOFT: Path = Path(os.getenv("RELATORIO_BSOFT", ""))
    RELATORIO_TARGET: Path = Path(os.getenv("RELATORIO_TARGET", ""))
    OUTPUT_FOLDER: Path = Path(os.getenv("OUTPUT_FOLDER", "outputs"))

    # BSoft
    BSOFT_PATH: Optional[Path] = Path(os.getenv("BSOFT_PATH", "")) if os.getenv("BSOFT_PATH") else None
    BSOFT_USER: Optional[str] = os.getenv("BSOFT_USER")
    BSOFT_PASSWORD: Optional[str] = os.getenv("BSOFT_PASSWORD")

    # API LogLife
    LOGLIFE_USER: str = os.getenv("LOGLIFE_USER", "")
    LOGLIFE_PASSWORD: str = os.getenv("LOGLIFE_PASSWORD", "")
    LOGLIFE_XTOKEN: str = os.getenv("LOGLIFE_XTOKEN", "")

    # Google Sheets
    GOOGLE_SHEETS_ENABLED: bool = os.getenv("GOOGLE_SHEETS_ENABLED", "false").lower() == "true"
    GOOGLE_CREDENTIALS_PATH: Optional[Path] = Path(os.getenv("GOOGLE_CREDENTIALS_PATH", "credentials.json"))
    GOOGLE_SHEET_ID: Optional[str] = os.getenv("GOOGLE_SHEET_ID")

    # Logging
    LOG_LEVEL: str = os.getenv("LOG_LEVEL", "INFO")
    LOG_FILE: Path = Path(os.getenv("LOG_FILE", "logs/cte_loglife.log"))

    # Automação
    DEFAULT_WAIT_TIME: int = int(os.getenv("DEFAULT_WAIT_TIME", "500"))
    MAX_RETRIES: int = int(os.getenv("MAX_RETRIES", "3"))
    SCREENSHOT_ON_ERROR: bool = os.getenv("SCREENSHOT_ON_ERROR", "true").lower() == "true"
    SCREENSHOTS_FOLDER: Path = Path(os.getenv("SCREENSHOTS_FOLDER", "screenshots"))

    @classmethod
    def validate(cls):
        """Valida configurações obrigatórias."""
        errors = []

        if not cls.LOGLIFE_USER:
            errors.append("LOGLIFE_USER não configurado")
        if not cls.LOGLIFE_PASSWORD:
            errors.append("LOGLIFE_PASSWORD não configurado")
        if not cls.LOGLIFE_XTOKEN:
            errors.append("LOGLIFE_XTOKEN não configurado")

        if errors:
            raise ValueError(f"Configuração inválida:\n" + "\n".join(errors))

    @classmethod
    def create_folders(cls):
        """Cria pastas necessárias se não existirem."""
        cls.OUTPUT_FOLDER.mkdir(parents=True, exist_ok=True)
        cls.LOG_FILE.parent.mkdir(parents=True, exist_ok=True)
        cls.SCREENSHOTS_FOLDER.mkdir(parents=True, exist_ok=True)


# Instância global
settings = Settings()
```

**Uso:**

```python
from botcte.config.settings import settings

# Acessar configurações
print(settings.CTE_FOLDER)
print(settings.LOG_LEVEL)

# Validar antes de iniciar
settings.validate()
settings.create_folders()
```

**Impacto:**

- ✅ Configuração centralizada
- ✅ Validação automática
- ✅ Type hints para IDE
- ✅ Fácil modificar defaults

---

#### 6. Sistema de Logging

**Prioridade:** 🟡 Média  
**Tempo Estimado:** 2 horas  
**Status:** ⏳ Pendente

**Objetivo:** Substituir `print()` por logging profissional.

##### `utils/logger.py`

```python
"""Sistema de logging da aplicação."""

import logging
import sys
from pathlib import Path
from typing import Optional

from ..config.settings import settings


def setup_logger(
    name: str = "botcte",
    log_file: Optional[Path] = None,
    level: Optional[str] = None
) -> logging.Logger:
    """
    Configura e retorna um logger.

    Args:
        name: Nome do logger
        log_file: Caminho do arquivo de log (opcional)
        level: Nível de log (DEBUG, INFO, WARNING, ERROR, CRITICAL)

    Returns:
        Logger configurado
    """
    logger = logging.getLogger(name)

    # Define nível
    log_level = level or settings.LOG_LEVEL
    logger.setLevel(getattr(logging, log_level.upper()))

    # Formato
    formatter = logging.Formatter(
        '%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )

    # Handler para console
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)

    # Handler para arquivo
    if log_file or settings.LOG_FILE:
        file_path = log_file or settings.LOG_FILE
        file_path.parent.mkdir(parents=True, exist_ok=True)

        file_handler = logging.FileHandler(file_path, encoding='utf-8')
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)

    return logger


# Logger global
logger = setup_logger()
```

**Uso:**

```python
from botcte.utils.logger import logger

# Substituir prints
# print("Processando CTe...")  # ❌ Antes
logger.info("Processando CTe...")  # ✅ Depois

# Diferentes níveis
logger.debug("Detalhes de debug")
logger.info("Informação geral")
logger.warning("Aviso importante")
logger.error("Erro ocorreu", exc_info=True)
logger.critical("Erro crítico!")
```

**Impacto:**

- ✅ Logs estruturados e profissionais
- ✅ Níveis de log configuráveis
- ✅ Logs em arquivo para auditoria
- ✅ Melhor debugging em produção

---

### Médio Prazo (Mês 1)

#### 7. Implementação de Testes

**Prioridade:** 🔴 Alta  
**Tempo Estimado:** 12-16 horas  
**Status:** ⏳ Pendente

**Objetivo:** Adicionar testes unitários e de integração.

##### Estrutura de Testes

```
tests/
├── __init__.py
├── conftest.py              # Fixtures compartilhadas
├── unit/
│   ├── __init__.py
│   ├── test_validators.py
│   ├── test_address.py
│   └── test_api_client.py
├── integration/
│   ├── __init__.py
│   ├── test_emissions.py
│   └── test_ui.py
└── fixtures/
    ├── sample_data.csv
    └── mock_responses.json
```

##### A. `tests/conftest.py`

```python
"""Fixtures compartilhadas para testes."""

import pytest
import pandas as pd
from pathlib import Path


@pytest.fixture
def sample_address_data():
    """Retorna dados de endereço de exemplo."""
    return pd.DataFrame({
        'id': [1, 2, 3],
        'trading_name': ['Empresa A', 'Empresa B', 'Empresa C'],
        'cnpj': ['12.345.678/0001-90', '98.765.432/0001-10', '11.222.333/0001-44'],
        'city': ['São Paulo', 'Rio de Janeiro', 'Belo Horizonte'],
    })


@pytest.fixture
def mock_api_client(monkeypatch):
    """Mock do APIClient para testes."""
    class MockAPIClient:
        def __init__(self):
            self.authenticated = True

        def get(self, endpoint):
            return pd.DataFrame({'result': ['success']})

    return MockAPIClient()


@pytest.fixture
def temp_env_file(tmp_path):
    """Cria arquivo .env temporário para testes."""
    env_file = tmp_path / ".env"
    env_file.write_text("""
LOGLIFE_USER=test@example.com
LOGLIFE_PASSWORD=testpass
LOGLIFE_XTOKEN=testtoken
CTE_FOLDER=/tmp/cte
    """)
    return env_file
```

##### B. `tests/unit/test_validators.py`

```python
"""Testes para módulo de validação."""

import pytest
from botcte.core.emissions.validators import (
    validate_cnpj,
    validate_date_range,
    validate_protocol
)


class TestCNPJValidation:
    """Testes de validação de CNPJ."""

    def test_valid_cnpj_with_mask(self):
        """Testa CNPJ válido com máscara."""
        assert validate_cnpj("12.345.678/0001-90") == True

    def test_valid_cnpj_without_mask(self):
        """Testa CNPJ válido sem máscara."""
        assert validate_cnpj("12345678000190") == True

    def test_invalid_cnpj_wrong_length(self):
        """Testa CNPJ inválido (tamanho incorreto)."""
        assert validate_cnpj("123456") == False

    def test_invalid_cnpj_wrong_checksum(self):
        """Testa CNPJ inválido (checksum incorreto)."""
        assert validate_cnpj("12.345.678/0001-99") == False

    def test_empty_cnpj(self):
        """Testa CNPJ vazio."""
        assert validate_cnpj("") == False

    @pytest.mark.parametrize("invalid_cnpj", [
        "00.000.000/0000-00",
        "11.111.111/1111-11",
        None,
    ])
    def test_known_invalid_cnpjs(self, invalid_cnpj):
        """Testa CNPJs conhecidos como inválidos."""
        assert validate_cnpj(invalid_cnpj) == False


class TestDateValidation:
    """Testes de validação de datas."""

    def test_valid_date_range(self):
        """Testa range de datas válido."""
        assert validate_date_range("2025-01-01", "2025-01-31") == True

    def test_invalid_date_range_reversed(self):
        """Testa range com data final antes da inicial."""
        assert validate_date_range("2025-01-31", "2025-01-01") == False

    def test_same_date_range(self):
        """Testa range com mesma data inicial e final."""
        assert validate_date_range("2025-01-15", "2025-01-15") == True
```

##### C. `tests/unit/test_api_client.py`

```python
"""Testes para cliente de API."""

import pytest
import responses
from botcte.utils.api_client import APIClient


class TestAPIClient:
    """Testes do cliente de API."""

    @responses.activate
    def test_authentication_success(self, monkeypatch):
        """Testa autenticação bem-sucedida."""
        # Mock environment variables
        monkeypatch.setenv("LOGLIFE_USER", "test@example.com")
        monkeypatch.setenv("LOGLIFE_PASSWORD", "testpass")
        monkeypatch.setenv("LOGLIFE_XTOKEN", "testtoken")

        # Mock API response
        responses.add(
            responses.POST,
            "https://transportebiologico.com.br/api/sessions",
            json={"token": "abc123"},
            status=200
        )

        client = APIClient()
        assert client.auth["authorization"] == "abc123"

    def test_missing_credentials_raises_error(self, monkeypatch):
        """Testa que credenciais ausentes lançam erro."""
        monkeypatch.delenv("LOGLIFE_USER", raising=False)

        with pytest.raises(ValueError, match="Missing API credentials"):
            APIClient()
```

##### D. `tests/integration/test_emissions.py`

```python
"""Testes de integração para emissões."""

import pytest
from unittest.mock import Mock, patch
from botcte.core.emissions import cte_list


class TestCTEEmission:
    """Testes de integração para emissão de CTe."""

    @patch('botcte.core.emissions.normal.requests.get')
    def test_cte_list_basic_flow(self, mock_get, sample_address_data):
        """Testa fluxo básico de emissão de CTe."""
        # Setup mock
        mock_response = Mock()
        mock_response.json.return_value = sample_address_data.to_dict('records')
        mock_get.return_value = mock_response

        # Execute
        result = cte_list(
            start_date="2025-01-01",
            final_date="2025-01-31",
            folderpath="/tmp/cte",
            cte_folder="/tmp/xml",
            root=None
        )

        # Verify
        assert result is not None
        # Adicionar mais asserções conforme necessário
```

**Executar Testes:**

```powershell
# Todos os testes
pytest

# Com cobertura
pytest --cov=botcte --cov-report=html

# Apenas testes unitários
pytest tests/unit/

# Com verbose
pytest -v

# Teste específico
pytest tests/unit/test_validators.py::TestCNPJValidation::test_valid_cnpj
```

**Meta de Cobertura:** 70%+

**Impacto:**

- ✅ Confiança em mudanças
- ✅ Documentação viva do código
- ✅ Prevenção de regressões
- ✅ Código mais robusto

---

#### 8. Type Hints e Type Checking

**Prioridade:** 🟡 Média  
**Tempo Estimado:** 8-10 horas  
**Status:** ⏳ Pendente

**Objetivo:** Adicionar type hints em todo o código.

**Antes:**

```python
def get_address_name(address_id):
    """Retorna nome do endereço."""
    return address.loc[address['id'] == address_id, 'trading_name'].values.item()
```

**Depois:**

```python
from typing import Union, List

def get_address_name(address_id: Union[int, List[int]]) -> str:
    """
    Retorna nome de trading do endereço.

    Args:
        address_id: ID único ou lista de IDs de endereços

    Returns:
        Nome de trading formatado

    Raises:
        ValueError: Se address_id não for encontrado
    """
    if isinstance(address_id, int):
        return address.loc[
            address['id'] == address_id,
            'trading_name'
        ].values.item()
    # ... resto da implementação
```

**Configuração mypy** (já em `pyproject.toml`):

```toml
[tool.mypy]
python_version = "3.9"
warn_return_any = true
warn_unused_configs = true
check_untyped_defs = true
no_implicit_optional = true
```

**Executar type checking:**

```powershell
mypy src/botcte
```

**Impacto:**

- ✅ Autocomplete melhorado no IDE
- ✅ Erros detectados antes da execução
- ✅ Código autodocumentado
- ✅ Refatorações mais seguras

---

#### 9. Ferramentas de Qualidade de Código

**Prioridade:** 🟡 Média  
**Tempo Estimado:** 4 horas  
**Status:** ⏳ Pendente

**Objetivo:** Configurar e aplicar ferramentas de formatação e linting.

##### A. Instalar Ferramentas

```powershell
pip install -e ".[dev]"
```

##### B. Configurar Pre-commit

**`.pre-commit-config.yaml`:**

```yaml
repos:
  - repo: https://github.com/psf/black
    rev: 23.11.0
    hooks:
      - id: black
        language_version: python3.9

  - repo: https://github.com/pycqa/isort
    rev: 5.12.0
    hooks:
      - id: isort
        args: ["--profile", "black"]

  - repo: https://github.com/pycqa/flake8
    rev: 6.1.0
    hooks:
      - id: flake8
        args: ["--max-line-length=100", "--extend-ignore=E203"]

  - repo: https://github.com/pre-commit/pre-commit-hooks
    rev: v4.5.0
    hooks:
      - id: trailing-whitespace
      - id: end-of-file-fixer
      - id: check-yaml
      - id: check-added-large-files
      - id: check-json
      - id: check-toml
      - id: mixed-line-ending

  - repo: https://github.com/pre-commit/mirrors-mypy
    rev: v1.7.0
    hooks:
      - id: mypy
        additional_dependencies: [types-requests, pandas-stubs]
```

**Instalar hooks:**

```powershell
pre-commit install
```

##### C. Usar Ferramentas

**Formatar código:**

```powershell
# Black (formatador)
black src/botcte/

# isort (organizar imports)
isort src/botcte/

# Ambos de uma vez
black src/ && isort src/
```

**Linting:**

```powershell
# flake8
flake8 src/botcte/

# pylint (opcional, mais rigoroso)
pylint src/botcte/
```

**Executar todos os checks:**

```powershell
pre-commit run --all-files
```

##### D. Configuração do Editor (VS Code)

**`.vscode/settings.json`:**

```json
{
  "python.formatting.provider": "black",
  "python.linting.enabled": true,
  "python.linting.flake8Enabled": true,
  "python.linting.mypyEnabled": true,
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.organizeImports": true
  },
  "[python]": {
    "editor.defaultFormatter": "ms-python.black-formatter",
    "editor.formatOnSave": true,
    "editor.rulers": [100]
  }
}
```

**Impacto:**

- ✅ Código consistente
- ✅ Formatação automática
- ✅ Problemas detectados cedo
- ✅ Code review mais rápido

---

#### 10. Documentação de Código

**Prioridade:** 🟢 Baixa  
**Tempo Estimado:** 6-8 horas  
**Status:** ⏳ Pendente

**Objetivo:** Adicionar docstrings em todas as funções e classes.

**Formato:** Google Style (mais legível)

**Exemplo:**

```python
def cte_list(
    start_date: str,
    final_date: str,
    folderpath: str,
    cte_folder: str,
    root: Optional[Any] = None
) -> pd.DataFrame:
    """
    Processa e emite CTe's normais para o período especificado.

    Esta função busca protocolos no sistema, valida dados de endereços,
    e gera os arquivos CTe correspondentes.

    Args:
        start_date: Data inicial no formato 'YYYY-MM-DD'
        final_date: Data final no formato 'YYYY-MM-DD'
        folderpath: Caminho da pasta para salvar CTe's
        cte_folder: Caminho da pasta com arquivos XML de CTe
        root: Widget Tkinter root (opcional, para UI)

    Returns:
        DataFrame contendo os CTe's processados com as colunas:
        - protocol: Número do protocolo
        - cte_number: Número do CTe emitido
        - status: Status da emissão

    Raises:
        ValueError: Se as datas forem inválidas
        FileNotFoundError: Se os caminhos não existirem
        APIError: Se houver erro na comunicação com a API

    Example:
        >>> result = cte_list(
        ...     start_date="2025-01-01",
        ...     final_date="2025-01-31",
        ...     folderpath="C:/CTe",
        ...     cte_folder="C:/XML"
        ... )
        >>> print(result.head())

    Note:
        - Requer conexão com internet para API
        - Arquivos Excel devem estar no formato correto
        - Processo pode demorar para grandes volumes

    See Also:
        cte_complimentary: Para emissão de CTe complementares
    """
    # Implementação
    pass
```

**Gerar documentação HTML (Sphinx):**

```powershell
# Instalar
pip install sphinx sphinx-rtd-theme

# Inicializar
cd docs
sphinx-quickstart

# Configurar autodoc
# Em docs/conf.py adicionar:
# extensions = ['sphinx.ext.autodoc', 'sphinx.ext.napoleon']

# Gerar docs
sphinx-apidoc -o source/ ../src/botcte
make html
```

**Impacto:**

- ✅ Código autodocumentado
- ✅ Onboarding mais rápido
- ✅ Melhor autocomplete
- ✅ Documentação sempre atualizada

---

### Longo Prazo (Mês 2+)

#### 11. CI/CD Pipeline

**Prioridade:** 🟢 Baixa  
**Tempo Estimado:** 4-6 horas  
**Status:** ⏳ Pendente

**Objetivo:** Automatizar testes e builds.

##### `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: windows-latest
    strategy:
      matrix:
        python-version: ["3.9", "3.10", "3.11"]

    steps:
      - uses: actions/checkout@v3

      - name: Set up Python ${{ matrix.python-version }}
        uses: actions/setup-python@v4
        with:
          python-version: ${{ matrix.python-version }}

      - name: Install dependencies
        run: |
          python -m pip install --upgrade pip
          pip install -r requirements.txt
          pip install -e ".[dev]"

      - name: Lint with flake8
        run: |
          flake8 src/botcte --count --select=E9,F63,F7,F82 --show-source --statistics
          flake8 src/botcte --count --max-line-length=100 --statistics

      - name: Type check with mypy
        run: mypy src/botcte

      - name: Test with pytest
        run: |
          pytest --cov=botcte --cov-report=xml --cov-report=html

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v3
        with:
          file: ./coverage.xml
          flags: unittests
          name: codecov-umbrella

  build:
    needs: test
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v3

      - name: Set up Python
        uses: actions/setup-python@v4
        with:
          python-version: "3.9"

      - name: Install dependencies
        run: |
          pip install -r requirements.txt
          pip install -e ".[build]"

      - name: Build executable
        run: pyinstaller "CTe LogLife 3.0.spec"

      - name: Upload artifact
        uses: actions/upload-artifact@v3
        with:
          name: cte-loglife-windows
          path: dist/
```

**Impacto:**

- ✅ Testes automáticos em cada commit
- ✅ Builds automáticos
- ✅ Detecção precoce de problemas
- ✅ Qualidade garantida

---

#### 12. Migração de Dados para Banco

**Prioridade:** 🟢 Baixa  
**Tempo Estimado:** 12-16 horas  
**Status:** ⏳ Pendente

**Objetivo:** Substituir Excel por SQLite/PostgreSQL.

**Benefícios:**

- Performance melhor
- Queries mais complexas
- Histórico de mudanças
- Concurrent access

**Exemplo com SQLAlchemy:**

```python
from sqlalchemy import create_engine, Column, Integer, String, Float
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker

Base = declarative_base()

class Aliquota(Base):
    __tablename__ = 'aliquotas'

    id = Column(Integer, primary_key=True)
    uf_origem = Column(String(2))
    uf_destino = Column(String(2))
    percentual = Column(Float)

# Criar tabelas
engine = create_engine('sqlite:///cte_loglife.db')
Base.metadata.create_all(engine)
```

---

## 📚 Guias Detalhados

### Guia 1: Como Extrair Função de Código Aninhado

**Problema:** Funções dentro de funções não podem ser testadas.

**Antes:**

```python
def cte_list(...):
    def get_address_name(address_id):
        # 50 linhas de código
        return result

    # Usa get_address_name
    name = get_address_name(123)
```

**Passos:**

1. **Identificar dependências externas:**

   - Quais variáveis do escopo externo são usadas?
   - Quais imports são necessários?

2. **Mover para módulo:**

   ```python
   # Em address.py
   def get_address_name(address_id: int, address_df: pd.DataFrame) -> str:
       """Docstring..."""
       # Código movido
       return result
   ```

3. **Atualizar chamadas:**

   ```python
   # Em normal.py
   from .address import get_address_name

   def cte_list(...):
       name = get_address_name(123, address)
   ```

4. **Adicionar testes:**
   ```python
   def test_get_address_name():
       df = pd.DataFrame(...)
       result = get_address_name(1, df)
       assert result == "Expected Name"
   ```

---

### Guia 2: Como Adicionar Type Hints Gradualmente

**Ordem recomendada:**

1. **Funções públicas primeiro**
2. **Classes e métodos**
3. **Funções internas**
4. **Variáveis locais complexas**

**Ferramentas úteis:**

```powershell
# Gerar stubs automaticamente
stubgen -p botcte -o stubs/

# Verificar progressão
mypy --show-stats src/botcte
```

---

### Guia 3: Como Migrar Configuração de .txt para .env

**Para cada arquivo .txt:**

1. **Identificar variável:**

   ```python
   # Antes
   with open('filename.txt', 'r') as f:
       path = f.read()
   ```

2. **Adicionar ao .env:**

   ```env
   CTE_FOLDER=C:\Path\To\Folder
   ```

3. **Atualizar código:**

   ```python
   # Depois
   from botcte.config.settings import settings
   path = settings.CTE_FOLDER
   ```

4. **Adicionar ao .gitignore:**
   ```gitignore
   filename.txt
   ```

---

## ✅ Checklist de Implementação

### Curto Prazo (Concluir até Semana 3)

- [ ] **Reestruturar diretórios**

  - [ ] Criar estrutura `src/botcte/`
  - [ ] Mover arquivos para novos locais
  - [ ] Atualizar todos os imports
  - [ ] Atualizar `pyproject.toml`
  - [ ] Testar que aplicação ainda funciona

- [ ] **Dividir `emissions.py`**

  - [ ] Criar `core/emissions/normal.py`
  - [ ] Criar `core/emissions/complementary.py`
  - [ ] Criar `core/emissions/address.py`
  - [ ] Criar `core/emissions/validators.py`
  - [ ] Criar `core/emissions/__init__.py`
  - [ ] Extrair funções aninhadas
  - [ ] Atualizar imports em `Base.py`
  - [ ] Testar cada módulo isoladamente

- [ ] **Refatorar `Base.py`**

  - [ ] Criar `ui/main_window.py`
  - [ ] Criar `ui/components.py`
  - [ ] Criar `ui/styles.py`
  - [ ] Criar `ui/dialogs.py`
  - [ ] Mover código para módulos apropriados
  - [ ] Testar interface gráfica

- [ ] **Refatorar `functions.py`**

  - [ ] Criar `utils/threading.py`
  - [ ] Criar `utils/file_browser.py`
  - [ ] Criar `utils/api_client.py`
  - [ ] Mover classes para arquivos separados
  - [ ] Atualizar imports

- [ ] **Sistema de configuração**

  - [ ] Criar `config/settings.py`
  - [ ] Adicionar todas as variáveis de ambiente
  - [ ] Adicionar validação
  - [ ] Substituir leitura de .txt por settings
  - [ ] Testar com diferentes configurações

- [ ] **Sistema de logging**
  - [ ] Criar `utils/logger.py`
  - [ ] Configurar handlers (console + file)
  - [ ] Substituir `print()` por `logger.*()`
  - [ ] Adicionar logs em pontos críticos
  - [ ] Testar rotação de logs

### Médio Prazo (Concluir até Mês 1)

- [ ] **Testes**

  - [ ] Configurar pytest
  - [ ] Criar `tests/conftest.py`
  - [ ] Adicionar testes unitários para validators
  - [ ] Adicionar testes unitários para address
  - [ ] Adicionar testes unitários para api_client
  - [ ] Adicionar testes de integração
  - [ ] Atingir 70% de cobertura
  - [ ] Configurar pytest no CI

- [ ] **Type hints**

  - [ ] Adicionar types em funções públicas
  - [ ] Adicionar types em classes
  - [ ] Adicionar types em métodos
  - [ ] Configurar mypy
  - [ ] Resolver todos os erros de tipo
  - [ ] Adicionar mypy ao CI

- [ ] **Qualidade de código**

  - [ ] Instalar ferramentas dev
  - [ ] Configurar pre-commit
  - [ ] Formatar com Black
  - [ ] Organizar imports com isort
  - [ ] Resolver warnings do flake8
  - [ ] Configurar VS Code
  - [ ] Documentar processo

- [ ] **Documentação**
  - [ ] Adicionar docstrings em funções públicas
  - [ ] Adicionar docstrings em classes
  - [ ] Configurar Sphinx
  - [ ] Gerar documentação HTML
  - [ ] Criar guias de uso
  - [ ] Publicar docs (GitHub Pages)

### Longo Prazo (Concluir até Mês 2+)

- [ ] **CI/CD**

  - [ ] Criar workflow GitHub Actions
  - [ ] Configurar testes automáticos
  - [ ] Configurar build automático
  - [ ] Configurar deploy de releases
  - [ ] Adicionar badges ao README

- [ ] **Banco de dados**

  - [ ] Avaliar necessidade
  - [ ] Escolher tecnologia (SQLite/PostgreSQL)
  - [ ] Criar modelos SQLAlchemy
  - [ ] Migrar dados do Excel
  - [ ] Atualizar queries
  - [ ] Testar performance

- [ ] **Melhorias arquiteturais**
  - [ ] Implementar dependency injection
  - [ ] Adicionar padrões de design apropriados
  - [ ] Refatorar código legado restante
  - [ ] Otimizar performance
  - [ ] Adicionar cache quando apropriado

---

## 📈 Métricas de Sucesso

### Cobertura de Código

- **Meta:** 70%+
- **Ferramenta:** pytest-cov
- **Comando:** `pytest --cov=botcte --cov-report=html`

### Complexidade Ciclomática

- **Meta:** <10 por função
- **Ferramenta:** radon
- **Comando:** `radon cc src/botcte -a`

### Duplicação de Código

- **Meta:** <5%
- **Ferramenta:** radon
- **Comando:** `radon raw src/botcte`

### Type Coverage

- **Meta:** 90%+
- **Ferramenta:** mypy
- **Comando:** `mypy --strict src/botcte`

### Tamanho de Arquivos

- **Meta:** <500 linhas por arquivo
- **Ferramenta:** Manual
- **Comando:** `Get-ChildItem -Recurse *.py | ForEach-Object { (Get-Content $_).Count }`

---

## 🎯 Priorização

**Legenda:**

- 🔴 Alta - Fazer imediatamente
- 🟡 Média - Fazer em breve
- 🟢 Baixa - Fazer quando possível

**Ordem sugerida de execução:**

1. 🔴 Reestruturação de diretórios
2. 🔴 Divisão do emissions.py
3. 🟡 Refatoração do Base.py
4. 🟡 Refatoração do functions.py
5. 🟡 Sistema de configuração
6. 🟡 Sistema de logging
7. 🔴 Testes unitários
8. 🟡 Type hints
9. 🟡 Qualidade de código
10. 🟢 Documentação
11. 🟢 CI/CD
12. 🟢 Banco de dados

---

## 📞 Suporte

Para dúvidas sobre este plano de refatoração:

- Abra uma issue no GitHub
- Consulte a documentação inline
- Revise exemplos de código nas seções de guias

---

**Última atualização:** 14 de Novembro de 2025  
**Próxima revisão:** Após completar tarefas de curto prazo
