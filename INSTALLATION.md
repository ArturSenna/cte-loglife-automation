# Installation Guide

## Prerequisites

- Python 3.10
- Git

## Fresh Installation

Follow these steps when cloning the repository for the first time:

### 1. Clone the Repository

```bash
git clone https://github.com/ArturSenna/cte-loglife-automation.git
cd cte-loglife-automation
```

### 2. Create Virtual Environment

**Windows (PowerShell):**

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
```

**Windows (Command Prompt):**

```cmd
python -m venv .venv
.venv\Scripts\activate.bat
```

**Linux/Mac:**

```bash
python -m venv .venv
source .venv/bin/activate
```

### 3. Upgrade Core Build Tools

Before installing dependencies, upgrade pip, setuptools, and wheel:

```bash
python -m pip install --upgrade pip setuptools wheel
```

**Why?** This prevents build errors with packages that need modern build tools.

### 4. Install Dependencies

```bash
pip install -r requirements.txt
```

## Troubleshooting

### Error: "Cannot import 'setuptools.build_meta'"

**Solution:**

```bash
python -m pip install --upgrade setuptools wheel
pip install -r requirements.txt
```

### Error: Dependency conflict with pyinstaller-hooks-contrib

**Solution:** The requirements.txt has been updated to use compatible versions. If you still encounter issues:

```bash
pip install --upgrade pip
pip install -r requirements.txt --upgrade
```

### Error: "ResolutionImpossible" or dependency conflicts

**Solution:**

1. Delete the virtual environment: `rm -rf .venv` (or manually delete the folder)
2. Create a fresh virtual environment (see step 2 above)
3. Follow steps 3-4 again

## Running the Application

After successful installation:

```bash
python botCTE/botCTE/Base.py
```

Or from the botCTE directory:

```bash
cd botCTE/botCTE
python Base.py
```

## Building Executable

To create a standalone executable:

```bash
# Install build dependencies
pip install -r requirements-build.txt

# Build the executable
python build_exe.ps1
```

## Development Setup

For development, you may want to install additional tools:

```bash
pip install pytest black flake8
```

## Notes

- **Virtual Environment**: Always use a virtual environment to avoid conflicts with system Python packages
- **Python Version**: This project requires Python 3.10+. Check with `python --version`
- **Build Tools**: The `setuptools>=65.0.0` and `wheel>=0.38.0` at the top of requirements.txt ensure compatibility
- **Dependency Resolution**: If you encounter dependency conflicts, try installing with `--upgrade` flag

## Getting Help

If you encounter issues not covered here:

1. Check existing GitHub issues
2. Review the error message carefully
3. Try creating a fresh virtual environment
4. Ensure you're using Python 3.10 or higher
