# CTE Complementary Implementation Guide

## Overview

This document describes the implementation of complementary CTE (CT-e Complementar) functionality across the backend API and Python automation scripts.

## Backend API Changes

### 1. Controllers Updated

#### GetCancelledAndUnsuccessfulServicesController

**File**: `src/controllers/service/GetCancelledAndUnsuccessfulServicesController.js`

**Changes**:

- Added `cte_complementary` and `cte_complementary_emission_date` to query attributes
- Changed date filtering from `collect_date` to `cte_loglife_emission_date` OR `cte_complementary_emission_date`
- Now filters by emission dates in Brazilian format (dd/MM/yyyy) instead of ISO collect dates
- Returns both CTE types in response

**Query Logic**:

```javascript
where: {
  [Op.or]: [
    {
      cte_loglife: { [Op.and]: [{ [Op.ne]: null }, { [Op.ne]: "-" }, { [Op.ne]: "nan" }] },
      cte_loglife_emission_date: { [Op.ne]: null }
    },
    {
      cte_complementary: { [Op.and]: [{ [Op.ne]: null }, { [Op.ne]: "-" }, { [Op.ne]: "nan" }] },
      cte_complementary_emission_date: { [Op.ne]: null }
    }
  ]
}
```

**Response Format**:

```json
{
  "total": 10,
  "services": [
    {
      "protocol": 12345,
      "trading_name": "Customer Name",
      "collect_date": "2025-11-14T00:00:00.000Z",
      "step": "cancelledService",
      "cte_loglife": "123456789",
      "cte_loglife_emission_date": "14/11/2025",
      "cte_complementary": "987654321",
      "cte_complementary_emission_date": "15/11/2025",
      "billing_status": "pending"
    }
  ]
}
```

#### GetServicesWithCteController

**File**: `src/controllers/service/GetServicesWithCteController.js`

**Changes**:

- Added `cte_complementary` and `cte_complementary_emission_date` to query attributes
- Queries for services with EITHER `cte_loglife` OR `cte_complementary`
- Filters by emission dates for both CTE types
- Sorts by most recent emission date (comparing both types)

**Response Format**:

```json
{
  "total": 25,
  "services": [
    {
      "protocol": 12345,
      "trading_name": "Customer Name",
      "cte_loglife_emission_date": "14/11/2025",
      "step": "deliveredService",
      "cte_loglife": "123456789",
      "cte_complementary": "987654321",
      "cte_complementary_emission_date": "15/11/2025"
    }
  ]
}
```

### 2. Upload Controllers

#### CTEComplementaryController

**File**: `src/controllers/uploads/CTEComplementaryController.js`

**New Method Added**: `uploadJson`

Accepts JSON payload to bulk update complementary CTE data:

**Request**:

```http
POST /api/uploads/cte-complementary/json
Content-Type: application/json
Authorization: Bearer <token>

{
  "data": [
    {
      "protocol": 12345,
      "cte_complementary": "987654321",
      "cte_complementary_emission_date": "15/11/2025"
    },
    {
      "protocol": 12346,
      "cte_complementary": null,
      "cte_complementary_emission_date": null
    }
  ]
}
```

**Response**:

```json
{
  "message": "Dados processados com sucesso",
  "recordsProcessed": 2
}
```

**Features**:

- Validates each record has required fields (`protocol`, `cte_complementary`, `cte_complementary_emission_date`)
- Checks if protocol exists in database
- Allows null values for cancellations
- Returns count of processed records

### 3. Routes Added

**File**: `src/routes.js`

```javascript
routes.post(
  "/api/uploads/cte-complementary/json",
  authMiddleware,
  CTEComplementaryController.uploadJson,
);
```

## Python Script Implementation Examples

### Example 1: cte_cancel_batch with Complementary CTE Support

```python
def cte_cancel_batch(start_date, final_date, root):
    """
    Cancel CTe documents (both loglife and complementary) in batch based on date range.

    Args:
        start_date: Initial date (string or datetime)
        final_date: Final date (string or datetime)
        root: Tkinter root window for popup messages
    """
    def formatar_data(data):
        """Format date to YYYY-MM-DD string."""
        if isinstance(data, str):
            for formato in ("%d/%m/%Y", "%Y-%m-%d"):
                try:
                    return datetime.strptime(data, formato).strftime("%Y-%m-%d")
                except ValueError:
                    continue
            print(f"❌ Data inválida: {data}")
            return None
        elif hasattr(data, "strftime"):
            return data.strftime("%Y-%m-%d")
        return str(data)

    # Format dates
    di_formatted = formatar_data(start_date)
    df_formatted = formatar_data(final_date)

    if not di_formatted or not df_formatted:
        confirmation_pop_up(root, "❌ Datas inválidas. Cancelando consulta.")
        return

    print(f"📅 Consultando cancelamentos entre {di_formatted} e {df_formatted}")

    try:
        # Query API for cancelled/unsuccessful services
        df = r.request_public(
            link=f'https://transportebiologico.com.br/api/public/service/cancelled-unsuccessful?initialDate={di_formatted}&finalDate={df_formatted}'
        )

        if "services" not in df.columns or df.empty:
            confirmation_pop_up(root, "⚠️ Nenhum serviço encontrado para o período.")
            return

        lista_servicos = df.iloc[0]["services"]
        if not isinstance(lista_servicos, list):
            confirmation_pop_up(root, "⚠️ Estrutura de retorno da API inesperada.")
            return

        print(f"\n📋 {len(lista_servicos)} serviços encontrados na API.")

        # Separate CTes by type
        ctes_loglife = []
        ctes_complementary = []

        for item in lista_servicos:
            protocolo = str(item["protocol"])

            # Check for Loglife CTe
            if "cte_loglife" in item and item["cte_loglife"]:
                numero_cte = str(item["cte_loglife"]).strip()
                if numero_cte not in ["-", "", "nan"] and numero_cte.isdigit():
                    ctes_loglife.append({
                        "numero": numero_cte,
                        "protocolo": protocolo,
                        "tipo": "loglife"
                    })

            # Check for Complementary CTe
            if "cte_complementary" in item and item["cte_complementary"]:
                numero_cte = str(item["cte_complementary"]).strip()
                if numero_cte not in ["-", "", "nan"] and numero_cte.isdigit():
                    ctes_complementary.append({
                        "numero": numero_cte,
                        "protocolo": protocolo,
                        "tipo": "complementary"
                    })

        total_ctes = len(ctes_loglife) + len(ctes_complementary)
        print(f"✅ {len(ctes_loglife)} CT-es Loglife para cancelar")
        print(f"✅ {len(ctes_complementary)} CT-es Complementares para cancelar")
        print(f"📊 Total: {total_ctes} CT-es")

        if total_ctes == 0:
            confirmation_pop_up(root, "⚠️ Nenhum CT-e válido encontrado para cancelamento.")
            return

        # Initialize bot
        bot_cte = bot.Bot()

        stats = {
            "loglife_cancelados": 0,
            "loglife_erros": 0,
            "complementary_cancelados": 0,
            "complementary_erros": 0,
            "api_loglife_success": 0,
            "api_loglife_errors": 0,
            "api_complementary_success": 0,
            "api_complementary_errors": 0
        }

        # Cancel Loglife CTes
        print(f"\n🔁 Processando {len(ctes_loglife)} CT-es Loglife...")
        for cte_data in ctes_loglife:
            numero_cte = cte_data["numero"]
            protocolo = cte_data["protocolo"]

            try:
                # Cancel CTe via UI automation
                bot_cte.cancel_cte(numero_cte)
                stats["loglife_cancelados"] += 1
                print(f"✅ CT-e Loglife {numero_cte} cancelado")

                # Update API
                try:
                    payload = {
                        "data": [{
                            "protocol": protocolo,
                            "cte_loglife": None,
                            "cte_loglife_emission_date": None
                        }]
                    }
                    r.request_private(
                        link="https://transportebiologico.com.br/api/uploads/cte-loglife/json",
                        request_type="post",
                        payload=payload,
                        json=True
                    )
                    stats["api_loglife_success"] += 1
                    print(f"📤 Protocolo {protocolo} atualizado (Loglife anulado)")
                except Exception as api_error:
                    print(f"❌ Erro API Loglife - Protocolo {protocolo}: {api_error}")
                    stats["api_loglife_errors"] += 1

            except Exception as e:
                print(f"⚠️ Erro ao cancelar CT-e Loglife {numero_cte}: {str(e)}")
                stats["loglife_erros"] += 1

        # Cancel Complementary CTes
        print(f"\n🔁 Processando {len(ctes_complementary)} CT-es Complementares...")
        for cte_data in ctes_complementary:
            numero_cte = cte_data["numero"]
            protocolo = cte_data["protocolo"]

            try:
                # Cancel CTe via UI automation
                bot_cte.cancel_cte(numero_cte)
                stats["complementary_cancelados"] += 1
                print(f"✅ CT-e Complementar {numero_cte} cancelado")

                # Update API
                try:
                    payload = {
                        "data": [{
                            "protocol": protocolo,
                            "cte_complementary": None,
                            "cte_complementary_emission_date": None
                        }]
                    }
                    r.request_private(
                        link="https://transportebiologico.com.br/api/uploads/cte-complementary/json",
                        request_type="post",
                        payload=payload,
                        json=True
                    )
                    stats["api_complementary_success"] += 1
                    print(f"📤 Protocolo {protocolo} atualizado (Complementar anulado)")
                except Exception as api_error:
                    print(f"❌ Erro API Complementar - Protocolo {protocolo}: {api_error}")
                    stats["api_complementary_errors"] += 1

            except Exception as e:
                print(f"⚠️ Erro ao cancelar CT-e Complementar {numero_cte}: {str(e)}")
                stats["complementary_erros"] += 1

        # Show results
        print(f"\n📊 CANCELAMENTO CONCLUÍDO")
        print(f"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
        print(f"CT-es Loglife:")
        print(f"  ✅ Cancelados: {stats['loglife_cancelados']}")
        print(f"  ⚠️ Erros: {stats['loglife_erros']}")
        print(f"  📤 API Sucesso: {stats['api_loglife_success']}")
        print(f"  ❌ API Erros: {stats['api_loglife_errors']}")
        print(f"\nCT-es Complementares:")
        print(f"  ✅ Cancelados: {stats['complementary_cancelados']}")
        print(f"  ⚠️ Erros: {stats['complementary_erros']}")
        print(f"  📤 API Sucesso: {stats['api_complementary_success']}")
        print(f"  ❌ API Erros: {stats['api_complementary_errors']}")
        print(f"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")

        total_cancelados = stats['loglife_cancelados'] + stats['complementary_cancelados']
        total_erros = stats['loglife_erros'] + stats['complementary_erros']

        result_message = (
            f"Cancelamento concluído!\n\n"
            f"CT-es Loglife:\n"
            f"  Cancelados: {stats['loglife_cancelados']}\n"
            f"  Erros: {stats['loglife_erros']}\n"
            f"  API: {stats['api_loglife_success']} OK, {stats['api_loglife_errors']} erro\n\n"
            f"CT-es Complementares:\n"
            f"  Cancelados: {stats['complementary_cancelados']}\n"
            f"  Erros: {stats['complementary_erros']}\n"
            f"  API: {stats['api_complementary_success']} OK, {stats['api_complementary_errors']} erro\n\n"
            f"Total: {total_cancelados} cancelados, {total_erros} erros"
        )
        confirmation_pop_up(root, result_message)

    except Exception as e:
        print(f"❌ Erro ao consultar API: {e}")
        traceback.print_exc()
        confirmation_pop_up(root, f"❌ Erro ao consultar API: {e}")
```

### Example 2: validar_e_cancelar_ctes with Complementary CTE Support

```python
def validar_e_cancelar_ctes(relatorio_bsoft_path, root):
    """
    Validates CTes (both loglife and complementary) from B-Soft report against API
    and cancels missing ones.

    Args:
        relatorio_bsoft_path: Path to B-Soft Excel report (.xlsx/.xls)
        root: Tkinter root window for popup messages
    """
    try:
        print("\n📋 Iniciando validação de CT-es do Relatório B-soft")

        # Read B-Soft report
        if not relatorio_bsoft_path or relatorio_bsoft_path == 'Relatório B-soft (.xlsx/.xls)':
            confirmation_pop_up(root, "❌ Por favor, selecione o arquivo Relatório B-soft.")
            return

        print(f"📂 Lendo arquivo: {relatorio_bsoft_path}")

        # Try reading with different engines for compatibility
        try:
            df_bsoft = pd.read_excel(relatorio_bsoft_path, engine='openpyxl')
        except:
            df_bsoft = pd.read_excel(relatorio_bsoft_path, engine='xlrd')

        # Verify 'Nº CT-e' or 'Número' column exists
        cte_column = None
        if 'Nº CT-e' in df_bsoft.columns:
            cte_column = 'Nº CT-e'
        elif 'Número' in df_bsoft.columns:
            cte_column = 'Número'
        else:
            confirmation_pop_up(root, f"❌ Coluna 'Nº CT-e' ou 'Número' não encontrada.\nColunas: {', '.join(df_bsoft.columns)}")
            return

        print(f"📊 Usando coluna: {cte_column}")

        # Filter out rows where 'Total' column equals 5
        if 'Total' in df_bsoft.columns:
            df_bsoft = df_bsoft[df_bsoft['Total'] != 5]
            print(f"📊 Filtradas linhas com Total = 5")

        # Get CTes from B-Soft report
        ctes_bsoft = set()
        for cte in df_bsoft[cte_column].dropna():
            try:
                cte_int = int(float(str(cte).strip()))
                ctes_bsoft.add(str(cte_int))
            except (ValueError, TypeError):
                cte_str = str(cte).strip()
                if cte_str and cte_str.lower() not in ['nan', '', '-']:
                    ctes_bsoft.add(cte_str)

        print(f"✅ {len(ctes_bsoft)} CT-es encontrados no Relatório B-soft")

        # Set date range for API query (last 90 days)
        end_date = dt.datetime.now()
        start_date = end_date - dt.timedelta(days=90)

        di_formatted = start_date.strftime("%Y-%m-%d")
        df_formatted = end_date.strftime("%Y-%m-%d")

        # Request CTes from API
        print(f"🔍 Consultando CT-es na API (período: {di_formatted} a {df_formatted})...")
        df_api = r.request_public(
            f'https://transportebiologico.com.br/api/public/service/with-cte?initialDate={di_formatted}&finalDate={df_formatted}'
        )

        if "services" not in df_api.columns or df_api.empty:
            confirmation_pop_up(root, "⚠️ Estrutura de retorno da API inesperada.")
            return

        lista_servicos = df_api.iloc[0]["services"]
        if not isinstance(lista_servicos, list):
            confirmation_pop_up(root, "⚠️ 'services' não contém uma lista.")
            return

        print(f"\n📋 {len(lista_servicos)} serviços encontrados na API.")

        # Extract CTes from services list (both types)
        ctes_api_loglife = set()
        ctes_api_complementary = set()
        ctes_api_all = set()

        for item in lista_servicos:
            # Loglife CTes
            if "cte_loglife" in item and item["cte_loglife"]:
                cte_num = str(item["cte_loglife"]).strip()
                if cte_num and cte_num not in ["-", "", "nan"]:
                    ctes_api_loglife.add(cte_num)
                    ctes_api_all.add(cte_num)

            # Complementary CTes
            if "cte_complementary" in item and item["cte_complementary"]:
                cte_num = str(item["cte_complementary"]).strip()
                if cte_num and cte_num not in ["-", "", "nan"]:
                    ctes_api_complementary.add(cte_num)
                    ctes_api_all.add(cte_num)

        print(f"✅ {len(ctes_api_loglife)} CT-es Loglife na API")
        print(f"✅ {len(ctes_api_complementary)} CT-es Complementares na API")
        print(f"✅ {len(ctes_api_all)} CT-es únicos no total")

        # Find CTes in B-Soft but NOT in API
        ctes_para_cancelar = ctes_bsoft - ctes_api_all

        if not ctes_para_cancelar:
            print("✅ Todos os CT-es do B-soft foram encontrados na API.")
            confirmation_pop_up(root, "✅ Todos os CT-es estão sincronizados!\nNenhum cancelamento necessário.")
            return

        print(f"\n⚠️ {len(ctes_para_cancelar)} CT-es do B-soft NÃO estão na API:")
        for cte in sorted(ctes_para_cancelar):
            print(f"  - {cte}")

        # Initialize bot for cancellation
        print("\n🤖 Iniciando processo de cancelamento...")
        bot_cte = bot.Bot()

        ctes_cancelados = 0
        ctes_com_erro = 0

        # Cancel each CTe
        # NOTE: We don't know if it's Loglife or Complementary from B-Soft report
        # The bot will attempt to cancel it regardless
        for numero_cte in sorted(ctes_para_cancelar):
            if not numero_cte.isdigit():
                print(f"⚠️ CT-e ignorado (não numérico): {numero_cte}")
                ctes_com_erro += 1
                continue

            try:
                bot_cte.cancel_cte(numero_cte)
                ctes_cancelados += 1
                print(f"✅ CT-e {numero_cte} cancelado")
            except Exception as e:
                print(f"⚠️ Erro ao cancelar CT-e {numero_cte}: {str(e)}")
                ctes_com_erro += 1

        # Show results
        print(f"\n📊 VALIDAÇÃO E CANCELAMENTO CONCLUÍDOS")
        print(f"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
        print(f"CT-es no B-soft: {len(ctes_bsoft)}")
        print(f"CT-es na API:")
        print(f"  - Loglife: {len(ctes_api_loglife)}")
        print(f"  - Complementares: {len(ctes_api_complementary)}")
        print(f"  - Total único: {len(ctes_api_all)}")
        print(f"\nCancelamentos:")
        print(f"  ✅ Sucesso: {ctes_cancelados}")
        print(f"  ⚠️ Erros: {ctes_com_erro}")
        print(f"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")

        result_message = (
            f"Validação concluída!\n\n"
            f"CT-es no B-soft: {len(ctes_bsoft)}\n"
            f"CT-es na API:\n"
            f"  Loglife: {len(ctes_api_loglife)}\n"
            f"  Complementares: {len(ctes_api_complementary)}\n"
            f"  Total: {len(ctes_api_all)}\n\n"
            f"Cancelados: {ctes_cancelados}\n"
            f"Erros: {ctes_com_erro}"
        )
        confirmation_pop_up(root, result_message)

    except Exception as e:
        print(f"❌ Erro na validação de CT-es: {e}")
        import traceback
        traceback.print_exc()
        confirmation_pop_up(root, f"❌ Erro na validação: {str(e)}")
```

## API Endpoints Reference

### Get Services with CTE

```
GET /api/public/service/with-cte?initialDate=YYYY-MM-DD&finalDate=YYYY-MM-DD
```

Returns services with either `cte_loglife` or `cte_complementary` within the emission date range.

### Get Cancelled/Unsuccessful Services

```
GET /api/public/service/cancelled-unsuccessful?initialDate=YYYY-MM-DD&finalDate=YYYY-MM-DD
```

Returns cancelled/unsuccessful services with CTE data, filtered by emission dates.

### Upload CTE Loglife (JSON)

```
POST /api/uploads/cte-loglife/json
Content-Type: application/json

{
  "data": [
    {
      "protocol": 12345,
      "cte_loglife": "123456789",
      "cte_loglife_emission_date": "14/11/2025"
    }
  ]
}
```

### Upload CTE Complementary (JSON)

```
POST /api/uploads/cte-complementary/json
Content-Type: application/json

{
  "data": [
    {
      "protocol": 12345,
      "cte_complementary": "987654321",
      "cte_complementary_emission_date": "15/11/2025"
    }
  ]
}
```

## Key Implementation Notes

1. **Date Format**: Emission dates are stored in Brazilian format `dd/MM/yyyy` in the database
2. **API Query Format**: Use ISO format `YYYY-MM-DD` for API query parameters
3. **Null Values**: Both `cte_loglife` and `cte_complementary` can be null (for cancellations)
4. **Filtering**: Services are excluded if CTE value is `null`, `"-"`, or `"nan"`
5. **Billing Filter**: `GetCancelledAndUnsuccessfulServicesController` excludes services with billing status `"done"` or `"awaiting-receive"`
6. **Sorting**: Services are sorted by most recent emission date (comparing both CTE types)

## Migration Considerations

- No database schema changes required (fields already exist in Service model)
- Controllers are backward compatible (existing functionality preserved)
- Python scripts can now handle both CTE types independently
- API responses include both CTE types for complete visibility

## Testing Checklist

- [ ] Test `GetCancelledAndUnsuccessfulServicesController` with services having only `cte_loglife`
- [ ] Test `GetCancelledAndUnsuccessfulServicesController` with services having only `cte_complementary`
- [ ] Test `GetCancelledAndUnsuccessfulServicesController` with services having both CTEs
- [ ] Test `GetServicesWithCteController` date filtering with both CTE types
- [ ] Test `CTEComplementaryController.uploadJson` with valid data
- [ ] Test `CTEComplementaryController.uploadJson` with null values (cancellation)
- [ ] Test Python `cte_cancel_batch` with both CTE types
- [ ] Test Python `validar_e_cancelar_ctes` with mixed CTE data
