# CTe LogLife Automation

Automação do processo de emissão de CTe (Conhecimento de Transporte Eletrônico) com integração no sistema BSoft, desenvolvido com BotCity Framework.

[![Python Version](https://img.shields.io/badge/python-3.9%2B-blue.svg)](https://www.python.org/downloads/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## Versão 2 em C# (em validação)

A reescrita em C#/.NET fica em `src/` e `tests/`. A versão em Python (`botCTE/`) continua no repositório,
intacta, até a versão nova ser validada com o Bsoft em produção.

```powershell
dotnet test CteLogLife.sln            # testes (não tocam no Bsoft nem na API)
dotnet run --project src\CteLogLife.App
.\build_dotnet.ps1                    # testes + pasta pronta em publish\CTe LogLife
.\build_installer_dotnet.ps1          # testes + instalador para o GitHub Releases (Inno Setup 6)
```

**Instalador e release.** `build_installer_dotnet.ps1` gera `installer_output\CTe_LogLife_Setup_v<versão>.exe`,
com a versão lida de `<Version>` em `Directory.Build.props`. Para publicar: suba a versão nesse arquivo,
rode o script, crie no GitHub um release com a tag `v<versão>` e anexe o instalador (o script mostra o
link no final). As cópias instaladas consultam o release mais recente, comparam a tag com a própria versão
e baixam o `.exe` que tem "setup" no nome. Um release marcado como **pre-release** não é oferecido a elas:
serve para testar o instalador em uma máquina antes de liberar para todas.

O instalador substitui a versão em Python no mesmo lugar (`Program Files (x86)\CTe LogLife`): apaga a
pasta `_internal`, mantém os `.txt` de pastas e o login que ela guardava em `_internal\.env`, e o programa
novo importa tudo na primeira abertura. Antes de gerar o instalador, o script instala uma cópia de teste
em uma pasta temporária preparada como uma instalação antiga e confere o resultado arquivo por arquivo;
ela tem identificação própria, não cria atalhos nem entradas no registro e não pede administrador. O
script publica em uma pasta separada (`obj\installer`), então pode rodar com o programa aberto.

| Projeto | Conteúdo |
|---|---|
| `CteLogLife.Core` | API do sistema LogLife, tabelas de ICMS, diário de emissões, fila com pausa, e os fluxos (unificado, avulso, simbólico, complementar, por UF, cancelamentos, GNRE, conferência de tomador) |
| `CteLogLife.Bsoft` | Captura de tela, busca por imagem, teclado/mouse, área de transferência e a sequência de telas do Bsoft |
| `CteLogLife.App` | Interface WPF e o painel de execução na borda da tela |

O que muda para quem opera:

- **Emissão unificada** é o botão principal; a antiga "lista de CTe" foi removida.
- Durante a execução aparece um **painel na borda da tela** com o CT-e atual e os próximos.
  `Pause` (ou `Ctrl+Alt+P`) pausa e continua; `Ctrl+Alt+Q` para.
- **Pausado**, o painel permite reordenar a fila, **buscar** um item (por protocolo, cliente ou cidade;
  `Enter` manda o primeiro resultado para a frente) e **incluir um protocolo** que não estava na lista: ele é
  buscado de novo no sistema e entra como o próximo. A inclusão existe nas emissões unificada, simbólica,
  avulsa e complementar; nas filas de cancelamento e de guias há só busca e reordenação.
- **Tela desconhecida**: quando um elemento esperado não aparece no Bsoft, o robô captura a tela, para e
  espera. Você resolve a tela no Bsoft e clica em Continuar. O que foi feito (cliques e as teclas Enter,
  Esc, Tab, Espaço, setas e Alt+letra) fica gravado junto com a imagem da tela, e da próxima vez que ela
  aparecer o robô repete sozinho. Texto digitado nunca é gravado: nesse caso, ou quando a tela não pode ser
  reconhecida com segurança, o ocorrido fica só no registro de atividade, com a captura. As telas aprendidas
  ficam em `%LOCALAPPDATA%\CTe LogLife\telas-aprendidas` e podem ser apagadas em Configurações.
- Cada emissão é gravada em um diário. Se o registro no sistema falhar depois de o CT-e ser autorizado,
  ela fica em **Pendências** para ser reenviada, e o protocolo não é emitido de novo enquanto isso.
- Pastas, relatórios e o login do sistema ficam em `%LOCALAPPDATA%\CTe LogLife` (o login criptografado
  pela conta do Windows). Na primeira abertura eles são importados dos arquivos `.txt` e do `.env` antigos.
  O x-token das rotas públicas é um só para todos e já vem no programa (`LogLifeApi.PublicToken`).

As imagens do Bsoft (`botCTE/botCTE/resources`) e as planilhas `Alíquota.xlsx` e `Complementares.xlsx`
continuam sendo as mesmas: as duas versões leem dali. O build está em .NET 9; para mudar de versão, altere
`NetVersion` em `Directory.Build.props`.

`"CTe LogLife.exe" --selftest relatorio.txt` confere, na máquina onde o robô roda, se o painel fica fora
das capturas de tela, se os atalhos estão livres, se a caixa de busca recebe o teclado e se o ciclo de tela
desconhecida (parar, aprender com um clique, repetir sozinho) funciona. Ele usa duas janelinhas do próprio
programa no lugar do Bsoft, move o ponteiro e dá dois cliques nelas; leva uns dez segundos.

---

## 📋 Índice

- [Sobre](#sobre)
- [Funcionalidades](#funcionalidades)
- [Requisitos](#requisitos)
- [Instalação](#instalação)
- [Configuração](#configuração)
- [Uso](#uso)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Desenvolvimento](#desenvolvimento)
- [Build](#build)
- [Contribuindo](#contribuindo)
- [Licença](#licença)

## 📖 Sobre

O CTe LogLife é uma ferramenta de automação RPA (Robotic Process Automation) que facilita a emissão de CTe's através da integração com o sistema BSoft. O projeto utiliza o BotCity Framework para automatizar tarefas repetitivas, reduzindo erros manuais e aumentando a eficiência operacional.

### Principais Benefícios

- ⚡ **Automatização completa** do processo de emissão de CTe
- 🎯 **Redução de erros** através da automação de tarefas manuais
- 📊 **Integração com relatórios** Excel e Google Sheets
- 🖥️ **Interface gráfica intuitiva** desenvolvida em Tkinter
- 📦 **Geração de executável** standalone via PyInstaller

## ✨ Funcionalidades

- ✅ Emissão automática de CTe normais
- ✅ Emissão de CTe complementares
- ✅ Integração com sistema BSoft
- ✅ Processamento de relatórios Excel (.xlsx/.xls)
- ✅ Sincronização com Google Sheets
- ✅ Interface gráfica moderna com tema Breeze
- ✅ Seleção de datas via calendário integrado
- ✅ Geração de relatórios CSV de associação
- ✅ Validação de CNPJ e dados de endereço
- ✅ Suporte a múltiplos remetentes e destinatários
- ✅ **Sistema de atualização automática** (novo! 🎉)
  - Verificação de atualizações no início
  - Atualização com um clique
  - Integração com GitHub Releases

## 🔧 Requisitos

### Requisitos de Sistema

- **Sistema Operacional**: Windows 7 ou superior
- **Python**: 3.9 ou superior
- **Memória RAM**: Mínimo 4 GB (recomendado 8 GB)
- **Espaço em disco**: 500 MB para instalação

### Dependências Principais

- BotCity Framework (Core e Base)
- Pandas e NumPy (processamento de dados)
- Selenium (automação web)
- OpenCV (processamento de imagem)
- Tkinter/tkcalendar (interface gráfica)
- Google APIs (integração com Sheets)

Para a lista completa de dependências, consulte [`requirements.txt`](requirements.txt).

## 📦 Instalação

### 1. Clone o Repositório

```powershell
git clone https://github.com/ArturSenna/cte-loglife-automation.git
cd cte-loglife-automation
```

### 2. Crie um Ambiente Virtual

```powershell
python -m venv venv
.\venv\Scripts\Activate.ps1
```

### 3. Instale as Dependências

```powershell
pip install -r requirements.txt
```

### 4. Instale o Pacote em Modo Desenvolvimento (Opcional)

```powershell
pip install -e .
```

## ⚙️ Configuração

### 1. Variáveis de Ambiente

Crie um arquivo `.env` na raiz do projeto com as seguintes configurações:

```env
# Caminhos de pastas
CTE_FOLDER=C:\Caminho\Para\Arquivos\CTE
FOLDERPATH_NORMAL=C:\Caminho\Para\CTE\Normal
FOLDERPATH_COMPLEMENTAR=C:\Caminho\Para\CTE\Complementar

# Relatórios
RELATORIO_BSOFT=C:\Caminho\Para\Relatorio\BSoft.xlsx
RELATORIO_TARGET=C:\Caminho\Para\Relatorio\Target.xlsx

# Configurações do BSoft (se aplicável)
BSOFT_PATH=C:\Caminho\Para\BSoft.exe
BSOFT_USER=seu_usuario
BSOFT_PASSWORD=sua_senha

# Google Sheets (opcional)
GOOGLE_SHEETS_ENABLED=false
GOOGLE_CREDENTIALS_PATH=credentials.json
```

**⚠️ Importante**: Nunca commit o arquivo `.env` com credenciais reais. Use `.env.example` como template.

### 2. Arquivos de Dados

Os seguintes arquivos Excel devem estar presentes em `botCTE/botCTE/`:

- `Alíquota.xlsx` - Tabela de alíquotas por estado
- `Complementares.xlsx` - Dados de CTe's complementares

### 3. Google Sheets (Opcional)

Se você deseja usar a integração com Google Sheets:

1. Crie um projeto no [Google Cloud Console](https://console.cloud.google.com/)
2. Ative a API do Google Sheets
3. Crie credenciais OAuth 2.0
4. Baixe o arquivo de credenciais como `credentials.json`
5. Coloque o arquivo na raiz do projeto

## 🚀 Uso

### Executar a Aplicação

#### Via Python

```powershell
python -m botCTE.botCTE
```

ou navegue até a pasta do bot:

```powershell
cd botCTE\botCTE
python __main__.py
```

#### Via Executável

Se você já gerou o executável:

```powershell
.\dist\CTe_LogLife_3.0\CTe_LogLife_3.0.exe
```

### Interface Gráfica

1. **Selecione as datas** de início e fim para emissão
2. **Configure os caminhos** dos relatórios e pastas
3. **Escolha o tipo de CTe** (Normal ou Complementar)
4. **Clique em Processar** para iniciar a automação

### Linha de Comando (Desenvolvimento)

Para executar funções específicas via código:

```python
from botCTE.botCTE import emissions

# Exemplo: processar CTe's normais
emissions.cte_list(
    start_date="2025-01-01",
    final_date="2025-01-31",
    folderpath="C:\\Caminho\\Para\\CTE",
    cte_folder="C:\\Caminho\\Para\\Arquivos",
    root=None
)
```

## 📁 Estrutura do Projeto

```
botCityCTE/
├── .env.example              # Template de variáveis de ambiente
├── .gitignore               # Arquivos ignorados pelo Git
├── pyproject.toml           # Configuração moderna do projeto
├── requirements.txt         # Dependências Python
├── README.md               # Este arquivo
├── LICENSE                 # Licença do projeto
│
├── botCTE/                 # Pacote principal
│   ├── setup.py           # Configuração de instalação (legado)
│   ├── requirements.txt   # Referência para dependências (deprecated)
│   ├── VERSION            # Versão do projeto
│   ├── README.md          # Documentação do pacote
│   │
│   └── botCTE/            # Código fonte
│       ├── __init__.py
│       ├── __main__.py    # Entry point da aplicação
│       ├── bot.py         # Classe Bot principal
│       ├── Base.py        # Interface gráfica
│       ├── emissions.py   # Lógica de emissão de CTe
│       ├── functions.py   # Funções auxiliares
│       ├── manifest.xml   # Manifesto do BotCity
│       ├── resources/     # Recursos (imagens, ícones)
│       ├── Alíquota.xlsx  # Dados de alíquotas
│       └── Complementares.xlsx
│
├── build/                  # Builds gerados (não versionado)
├── dist/                   # Distribuições (não versionado)
├── Relatórios/            # Relatórios gerados (não versionado)
└── Serviços/              # Arquivos de serviço (não versionado)
```

## 🛠️ Desenvolvimento

### Instalação para Desenvolvimento

```powershell
# Instalar dependências de desenvolvimento
pip install -e ".[dev]"

# Instalar pre-commit hooks
pre-commit install
```

### Ferramentas de Qualidade de Código

O projeto está configurado para usar as seguintes ferramentas:

#### Black (Formatação)

```powershell
black botCTE/
```

#### isort (Organização de imports)

```powershell
isort botCTE/
```

#### flake8 (Linting)

```powershell
flake8 botCTE/
```

#### mypy (Type checking)

```powershell
mypy botCTE/
```

### Executar Todos os Checkers

```powershell
black --check botCTE/
isort --check-only botCTE/
flake8 botCTE/
mypy botCTE/
```

### Testes (Em Desenvolvimento)

```powershell
pytest tests/
```

## 📦 Build

### Gerar Executável com PyInstaller

```powershell
# Instalar dependências de build
pip install -e ".[build]"

# Gerar executável
pyinstaller "CTe LogLife 3.0.spec"
```

O executável será gerado em `dist/CTe LogLife 3.0/`.

### Build Personalizado

Para criar um novo spec file:

```powershell
pyi-makespec --onedir --windowed --name "CTe LogLife 3.0" --icon=botCTE/botCTE/my_icon.ico botCTE/botCTE/__main__.py
```

## 🤝 Contribuindo

Contribuições são bem-vindas! Para contribuir:

1. **Fork** o projeto
2. Crie uma **branch** para sua feature (`git checkout -b feature/MinhaFeature`)
3. **Commit** suas mudanças (`git commit -m 'Adiciona MinhaFeature'`)
4. **Push** para a branch (`git push origin feature/MinhaFeature`)
5. Abra um **Pull Request**

### Diretrizes de Contribuição

- Siga o estilo de código do projeto (use Black e isort)
- Adicione testes para novas funcionalidades
- Atualize a documentação quando necessário
- Mantenha commits pequenos e focados

## 📄 Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE](LICENSE) para detalhes.

## 📞 Suporte

Para reportar bugs ou solicitar funcionalidades, abra uma [issue](https://github.com/ArturSenna/cte-loglife-automation/issues) no GitHub.

## 🙏 Agradecimentos

- [BotCity](https://www.botcity.dev/) - Framework de automação RPA
- Comunidade Python Brasil
- Todos os contribuidores do projeto

---

**Desenvolvido com ❤️ por [Artur Senna](https://github.com/ArturSenna)**
