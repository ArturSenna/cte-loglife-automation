import datetime as dt
import os
import sys
import time
import traceback
from datetime import datetime
import numpy as np
import bot
from functions import *

r = RequestDataFrame()

state_capitals = {
    'AC':    'Rio Branco',     'ACRE':                    'Rio Branco',
    'AL':    'Maceió',         'ALAGOAS':                 'Maceió',
    'AP':    'Macapá',         'AMAPÁ':                   'Macapá',
    'AM':    'Manaus',         'AMAZONAS':                'Manaus',
    'BA':    'Salvador',       'BAHIA':                   'Salvador',
    'CE':    'Fortaleza',      'CEARÁ':                   'Fortaleza',
    'DF':    'Brasília',       'DISTRITO FEDERAL':        'Brasília',
    'ES':    'Vitória',        'ESPÍRITO SANTO':          'Vitória',
    'GO':    'Goiânia',        'GOIÁS':                   'Goiânia',
    'MA':    'São Luís',       'MARANHÃO':                'São Luís',
    'MT':    'Cuiabá',         'MATO GROSSO':             'Cuiabá',
    'MS':    'Campo Grande',   'MATO GROSSO DO SUL':      'Campo Grande',
    'MG':    'Belo Horizonte', 'MINAS GERAIS':            'Belo Horizonte',
    'PA':    'Belém',          'PARÁ':                    'Belém',
    'PB':    'João Pessoa',    'PARAÍBA':                 'João Pessoa',
    'PR':    'Curitiba',       'PARANÁ':                  'Curitiba',
    'PE':    'Recife',         'PERNAMBUCO':              'Recife',
    'PI':    'Teresina',       'PIAUÍ':                   'Teresina',
    'RJ':    'Rio de Janeiro', 'RIO DE JANEIRO':          'Rio de Janeiro',
    'RN':    'Natal',          'RIO GRANDE DO NORTE':     'Natal',
    'RS':    'Porto Alegre',   'RIO GRANDE DO SUL':       'Porto Alegre',
    'RO':    'Porto Velho',    'RONDÔNIA':                'Porto Velho',
    'RR':    'Boa Vista',      'RORAIMA':                 'Boa Vista',
    'SC':    'Florianópolis',  'SANTA CATARINA':          'Florianópolis',
    'SP':    'São Paulo',      'SÃO PAULO':               'São Paulo',
    'SE':    'Aracaju',        'SERGIPE':                 'Aracaju',
    'TO':    'Palmas',         'TOCANTINS':               'Palmas',
}

state_full_name = {
    'AC':    '3154', # ESTADO DO ACRE
    'AL':    '523', # ESTADO DE ALAGOAS
    'AP':    '3256', # ESTADO DO AMAPA
    'AM':    '1946', # ESTADO DO AMAZONAS
    'BA':    '1924', # ESTADO DA BAHIA
    'CE':    '1702', # ESTADO DO CEARA
    'DF':    '1617', # ESTADO DO DISTRITO FEDERAL (BRASILIA)
    'ES':    '1891', # ESTADO DO ESPÍRITO SANTO
    'GO':    '1620', # ESTADO DE GOIAS
    'MA':    '1973', # ESTADO DO MARANHÃO
    'MT':    '1783', # ESTADO DO MATO GROSSO
    'MS':    '2053', # ESTADO DO MATO GROSSO DO SUL
    'MG':    '80', # ESTADO DE MINAS GERAIS
    'PA':    '1890', # ESTADO DO PARÁ
    'PB':    '1925', # ESTADO DA PARAIBA
    'PR':    '1784', # ESTADO DO PARANA
    'PE':    '1388', # ESTADO DE PERNAMBUCO
    'PI':    '1535', # ESTADO DO PIAUI
    'RJ':    '1389', # ESTADO DO RIO DE JANEIRO
    'RN':    '1391', # ESTADO DO RIO GRANDE DO NORTE
    'RS':    '1409', # ESTADO DO RIO GRANDE DO SUL
    'RO':    '3782', # ESTADO DE RONDONIA
    'RR':    '1926', # ESTADO DE RORAIMA
    'SC':    '1569', # ESTADO DE SANTA CATARINA
    'SP':    '1390', # ESTADO DE SAO PAULO
    'SE':    '1927', # ESTADO DE SERGIPE
    'TO':    '2496', # ESTADO DO TOCANTINS
}

# Get the directory where this script is located
# For PyInstaller bundles, use _MEIPASS to access bundled resources
if getattr(sys, 'frozen', False):
    # Running as compiled executable with PyInstaller
    _SCRIPT_DIR = getattr(sys, '_MEIPASS', os.path.dirname(sys.executable))
else:
    # Running as script
    _SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

uf_base = pd.read_excel(os.path.join(_SCRIPT_DIR, 'Complementares.xlsx'), sheet_name='Plan1')
aliquota_base = pd.read_excel(os.path.join(_SCRIPT_DIR, 'Alíquota.xlsx'), sheet_name='Planilha1')


def cte_list(start_date, final_date, folderpath, cte_folder, root):
    def get_address_name(address_id):
        add_lenght = len(address_id)
        count = 0
        add_total = ''
        for i in range(add_lenght):
            add_name = address.loc[address['id'] == address_id[i], 'trading_name'].values.item()
            add_branch = address.loc[address['id'] == address_id[i], 'branch'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = str(add_name)
            else:
                listing = str(count) + '. ' + str(add_name)
            add_total += listing + ' - ' + add_branch + space
        return add_total

    def get_address_meta(add):
        add_lenght = len(add)
        count = 0
        add_total = ''
        for i in range(add_lenght):
            add_street = address.loc[address['id'] == add[i], 'street'].values.item()
            add_num = address.loc[address['id'] == add[i], 'number'].values.item()
            add_dist = address.loc[address['id'] == add[i], 'neighborhood'].values.item()
            add_city = address.loc[address['id'] == add[i], 'cityIDAddress.name'].values.item()
            add_state = address.loc[address['id'] == add[i], 'state'].values.item()
            add_cep = address.loc[address['id'] == add[i], 'cep'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = ''
            else:
                listing = str(count) + '. '
            add_total += listing + add_street.title() + ', ' + add_num + ' - ' + add_dist.title() + ', ' \
                         + add_city.title() + ' - ' + add_state + ', ' + add_cep + space
        return add_total

    def get_address_cnpj_listed(add_list):
        add_lenght = len(add_list)
        count = 0
        add_total = ''
        for add in add_list:
            cnpj = address.loc[address['id'] == add, 'cnpj_cpf'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = ''
            else:
                listing = str(count) + '. '
            add_total += listing + cnpj + space
        return add_total

    def get_address_cnpj(add_list):
        _cnpj_list = []
        for add in add_list:
            cnpj = address.loc[address['id'] == add, 'cnpj_cpf'].values.item()
            _cnpj_list.append(cnpj)
        return _cnpj_list

    def get_address_city(add):
        add_lenght = len(add)
        add_total = []
        count = 0
        cities = ''
        for i in range(add_lenght):
            add_city = address.loc[address['id'] == add[i], 'cityIDAddress.name'].values.item()
            add_total.append(add_city)
        add_total = np.unique(add_total)
        for p in add_total:
            if count == 0:
                cities = p
            elif count == len(add_total) - 1:
                cities += ', ' + p
            else:
                cities += ', ' + p
            count += 1
        return cities

    def get_address_city_listed(add_list):
        _cities_list = []
        for add in add_list:
            add_city = address.loc[address['id'] == add, 'cityIDAddress.name'].values.item()
            _cities_list.append(add_city)
        cities_list = np.unique(_cities_list)
        return cities_list

    def get_collector(col):
        col_name = collector.loc[collector['id'] == col, 'trading_name'].values.item()
        return col_name

    # def get_transp(id_t):
    #     try:
    #         if id_t is not None:
    #             transp = bases.loc[bases['id'] == id_t, 'shippingIDBranch.company_name'].values.item()
    #         else:
    #             transp = ""
    #     except ValueError:
    #         transp = ""
    #     return transp

    now = dt.datetime.now()
    now_date = dt.datetime.strftime(start_date, '%d-%m-%Y')
    now = dt.datetime.strftime(now, "%H-%M")

    di = dt.datetime.strftime(start_date, '%d/%m/%Y')
    df = dt.datetime.strftime(final_date, '%d/%m/%Y')

    di_dt = dt.datetime.strptime(di, '%d/%m/%Y')
    df_dt = dt.datetime.strptime(df, '%d/%m/%Y')

    di_temp = di_dt - dt.timedelta(days=5)
    df_temp = df_dt + dt.timedelta(days=5)

    di = dt.datetime.strftime(di_temp, '%d/%m/%Y')
    df = dt.datetime.strftime(df_temp, '%d/%m/%Y')

    address = r.request_public('https://transportebiologico.com.br/api/public/address')
    collector = r.request_public('https://transportebiologico.com.br/api/public/collector')
    services_ongoing = r.request_public('https://transportebiologico.com.br/api/public/service')
    services_finalized = r.request_public(
        f'https://transportebiologico.com.br/api/public/service/finalized/?startFilter={di}&endFilter={df}',
        'post'
    )

    sv = pd.concat([services_ongoing, services_finalized], ignore_index=True)
    sv = sv.loc[sv['is_business'] == False]
    sv.drop(sv[sv['step'] == 'toValidateCancelRequest'].index, inplace=True)

    df_dt += dt.timedelta(days=1)

    sv['collectDateTime'] = pd.to_datetime(sv['serviceIDRequested.collect_date']) - dt.timedelta(hours=3)
    sv['collectDateTime'] = sv['collectDateTime'].dt.tz_localize(None)

    sv.drop(sv[sv['collectDateTime'] < di_dt].index, inplace=True)
    sv.drop(sv[sv['collectDateTime'] > df_dt].index, inplace=True)
    sv.to_excel('ServicesAPI.xlsx', index=True)

    sv = pd.concat([sv[sv['cte_loglife'].isnull()],
                    sv[sv['cte_loglife'] == 'nan'],
                    sv[sv['cte_loglife'] == '-']], ignore_index=True)

    sv.drop(sv[sv['customerIDService.emission_type'] == 'NF'].index, inplace=True)
    sv.drop(sv[sv['customerIDService.trading_firstname'] == 'LOGLIFE'].index, inplace=True)
    sv.drop(sv[sv['serviceIDRequested.budgetIDService.price'] == 0].index, inplace=True)

    sv['origCityList'] = sv['serviceIDRequested.source_address_id'].map(get_address_city_listed)
    sv['destCityList'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city_listed)
    sv['origCity'] = sv['serviceIDRequested.source_address_id'].map(get_address_city)
    sv['destCity'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city)

    sv.drop(sv[
                (sv['origCityList'].str.len() == 1) &
                (sv['destCityList'].str.len() == 1) &
                (sv['origCity'] == sv['destCity'])
                ].index, inplace=True)

    sv.sort_values(
        by="protocol", axis=0, ascending=True, inplace=True, kind='quicksort', na_position='last'
    )

    sv.to_excel('ServicesAPI.xlsx', index=False)

    report = pd.DataFrame(columns=[])

    report['PROTOCOLO'] = sv['protocol']
    report['CLIENTE'] = sv['customerIDService.trading_firstname']
    report['ETAPA'] = np.select(
        condlist=[
            sv['step'] == 'availableService',
            sv['step'] == 'toAllocateService',
            sv['step'] == 'toDeliveryService',
            sv['step'] == 'deliveringService',
            sv['step'] == 'toLandingService',
            sv['step'] == 'landingService',
            sv['step'] == 'toBoardValidate',
            sv['step'] == 'toCollectService',
            sv['step'] == 'collectingService',
            sv['step'] == 'toBoardService',
            sv['step'] == 'boardingService',
            sv['step'] == 'finishedService'],
        choicelist=[
            'AGUARDANDO DISPONIBILIZAÇÃO', 'AGUARDANDO ALOCAÇÃO', 'EM ROTA DE ENTREGA', 'ENTREGANDO',
            'DISPONÍVEL PARA RETIRADA', 'DESEMBARCANDO', 'VALIDAR EMBARQUE', 'AGENDADO', 'COLETANDO',
            'EM ROTA DE EMBARQUE', 'EMBARCANDO SERVIÇO', 'FINALIZADO'],
        default=''
    )
    report['DATA COLETA'] = sv['collectDateTime'].dt.strftime(date_format='%d/%m/%Y')
    report['PREÇO TRANSPORTE'] = sv['serviceIDRequested.budgetIDService.price']
    report['PREÇO KG EXTRA'] = sv['serviceIDRequested.budgetIDService.price_kg_extra']
    report['NOME REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_name)
    report['CIDADE ORIGEM'] = sv['origCity']
    report['ENDEREÇO REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_meta)
    report['CNPJ/CPF REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_cnpj_listed)
    report['COLETADOR ORIGEM'] = sv['serviceIDRequested.source_collector_id'].map(get_collector)
    report['NOME DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_name)
    report['CIDADE DESTINO'] = sv['destCity']
    report['ENDEREÇO DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_meta)
    report['CNPJ/CPF DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_cnpj_listed)
    report['COLETADOR DESTINO'] = sv['serviceIDRequested.destination_collector_id'].map(get_collector)

    cte_path = folderpath
    cte_path = cte_path.replace('/', '\\')

    excel_file = f'{cte_path}\\Lista CTE-{now_date}-{now}.xlsx'
    csv_file = f'{cte_path}\\Upload-{now_date}-{now}.csv'
    csv_associate = f'{cte_path}\\Associar-{now_date}-{now}.csv'

    cte_folder_path = cte_folder.replace('/', '\\')

    report.to_excel(excel_file, index=False)

    print("Relatório exportado!")

    bot_cte = bot.Bot()

    # bot_cte.open_bsoft(path=filename.get(), login=login.get(), password=password.get())
    current_row = 0

    for protocol in sv['protocol']:

        report_date = dt.datetime.strftime(dt.datetime.now(), '%d/%m/%Y')

        tomador_cnpj = sv.loc[sv['protocol'] == protocol, 'customerIDService.cnpj_cpf'].values.item()
        source_add = sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.source_address_id'].values.item()
        destination_add = sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.destination_address_id'].values.item()
        valor = str(sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.price'].values.item())
        uf1 = address.loc[address['id'] == source_add[0], 'state'].values.item()
        uf2 = address.loc[address['id'] == destination_add[0], 'state'].values.item()
        cnpj_remetente = get_address_cnpj(source_add)
        cnpj_destinatario = get_address_cnpj(destination_add)
        uf_rem = uf_base.loc[uf_base['Estado'] == uf1, 'UF'].values.item()
        uf_dest = uf_base.loc[uf_base['Estado'] == uf2, 'UF'].values.item()
        icms_obs = uf_base.loc[uf_base['Estado'] == uf_rem, 'Info'].values.item()
        aliq = aliquota_base.loc[aliquota_base['UF'] == uf_rem, uf_dest].values.item()
        obs_text = f'Protocolo {protocol} - {icms_obs}'
        aliq_text = float(aliq) * float(valor) * 0.008
        aliq_text = "{:0.2f}".format(aliq_text)

        if uf_rem != "MG":

            obs_text = obs_text.replace('#', aliq_text)
            aliq = "0"

        if tomador_cnpj in cnpj_remetente:
            tomador = "Remetente"
            cnpj_remetente = [tomador_cnpj]
        elif tomador_cnpj in cnpj_destinatario:
            tomador = "Destinatário"
            cnpj_destinatario = [tomador_cnpj]
        else:
            tomador = "Outro"

        bot_cte.action(cnpj_sender=cnpj_remetente,
                       cnpj_receiver=cnpj_destinatario,
                       payer=tomador,
                       payer_cnpj=tomador_cnpj)
        bot_cte.part3_normal()
        bot_cte.part4(
            tax=str(aliq),
            tp_info=aliq_text,
            uf=uf_rem,
            icms_text=obs_text,
            price=valor
        )

        cte_llm = int(bot_cte.get_clipboard())

        report.at[report.index[current_row], 'CTE LOGLIFE'] = cte_llm
        
        payload = {
            "data": [
                {
                    "protocol": str(protocol),
                    "cte_loglife": cte_llm,
                    "cte_loglife_emission_date": report_date
                }
            ]
        }
        
        r.request_private(
            link="https://transportebiologico.com.br/api/uploads/cte-loglife/json",
            request_type="post",
            payload=payload,
            json=True
        )
        
        cte_file = f'{str(cte_llm).zfill(8)}.pdf'

        cte_csv = pd.DataFrame({
            'Protocolo': [int(protocol)],
            'Arquivo PDF': [cte_file],
        })

        report.to_excel(
            excel_file,
            index=False
        )

        cte_csv.to_csv(csv_associate, index=False, encoding='utf-8')

        while True:
            try:
                r.post_file("https://transportebiologico.com.br/api/pdf",
                            f'{cte_folder_path}\\{cte_file}',
                            upload_type="CTE LOGLIFE",
                            file_format="application/pdf",
                            file_type="pdf_files")
                break
            except FileNotFoundError:
                time.sleep(0.5)
                continue

        r.post_file('https://transportebiologico.com.br/api/pdf/associate',
                    csv_associate,
                    upload_type="CTE LOGLIFE")

        os.remove(csv_associate)

        current_row += 1

    confirmation_pop_up(root, "Emissões realizadas com sucesso!")


def cte_complimentary(start_date, final_date, cte_comp_path, cte_folder, root, unique=False, cte_cs=""):
    def get_address_name(add):
        add_lenght = len(add)
        count = 0
        add_total = ''
        for i in range(add_lenght):
            add_name = address.loc[address['id'] == add[i], 'trading_name'].values.item()
            add_branch = address.loc[address['id'] == add[i], 'branch'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = str(add_name)
            else:
                listing = str(count) + '. ' + str(add_name)
            add_total += listing + ' - ' + add_branch + space
        return add_total

    def get_cte_number(protocol_number):

        cte_number = sv.loc[sv['protocol'] == protocol_number, 'cte_loglife'].values.item()

        return cte_number

    def hiae_additional_cost(prot_number):
        client = report.loc[report['PROTOCOLO'] == prot_number, 'CLIENTE'].values.item()
        total_price = services_billing.loc[
            services_billing['protocol'] == prot_number, 'totalAdditionalCollects'
        ].values.item()

        if client != "HIAE - HOSPITAL ALBERT EINSTEIN":
            return total_price
        else:
            service_collect_qty = services_billing.loc[
                services_billing['protocol'] == prot_number, 'serviceSourceAddressQty'
            ].values.item()
            service_collect_qty = int(service_collect_qty)
            service_budget_qty = services_billing.loc[
                services_billing['protocol'] == prot_number, 'budgetSourceAddressQty'
            ].values.item()
            service_budget_qty = int(service_budget_qty)
            add_collect = service_collect_qty - service_budget_qty

            if add_collect == 0:

                return 0

            else:
                collect_dict_list = sv.loc[sv['protocol'] == prot_number, 'serviceIDCollect'].values.item()
                success_value = sv.loc[
                    sv['protocol'] == prot_number, 'serviceIDRequested.budgetIDService.price_add_collect'
                ].values.item()
                unsuccessful_value = sv.loc[
                    sv['protocol'] == prot_number, 'serviceIDRequested.budgetIDService.price_unsuccessful_collect'
                ].values.item()
                total_mod_price = 0
                for collect_dict in collect_dict_list:
                    collect_step = collect_dict.get('step', np.nan)
                    collect_price = success_value if collect_step == 'DONE' else unsuccessful_value
                    total_mod_price += collect_price
                total_add_price = total_mod_price - success_value

                return total_add_price

    def hiae_dry_ice_cost(prot_number):

        client = report.loc[report['PROTOCOLO'] == prot_number, 'CLIENTE'].values.item()
        total_price = report.loc[
            report['PROTOCOLO'] == prot_number, 'VALOR COLETAS ADICIONAIS AJUSTADO'
        ].values.item()

        if client != "HIAE - HOSPITAL ALBERT EINSTEIN":
            return total_price

        dry_ice_qty = sv.loc[sv['protocol'] == prot_number, 'serviceIDRequested.gelo_seco'].values.item()
        budget_id = sv.loc[sv['protocol'] == prot_number, 'serviceIDRequested.budget_id'].values.item()

        if dry_ice_qty != 0 and budget_id in ['7f6b2c38-8fb1-47b7-a72e-2fa8acd73958', 'feffe85b-54cd-4ffa-9d2b-9e826422f26f']:
            add_collect_price = sv.loc[sv['protocol'] == prot_number, 'serviceIDRequested.price_add_collect'].values.item()
            new_price = total_price - add_collect_price + 60
            return new_price
        else:
            return total_price

    # def get_address_cnpj(add_list):
    #     _cnpj_list = []
    #     for add in add_list:
    #         cnpj = address.loc[address['id'] == add, 'cnpj_cpf'].values.item()
    #         _cnpj_list.append(cnpj)
    #     return _cnpj_list

    def get_address_city(add):
        add_lenght = len(add)
        add_total = []
        count = 0
        cities = ''
        for i in range(add_lenght):
            add_city = address.loc[address['id'] == add[i], 'cityIDAddress.name'].values.item()
            add_total.append(add_city)
        add_total = np.unique(add_total)
        for w in add_total:
            if count == 0:
                cities = w
            elif count == len(add_total) - 1:
                cities += ', ' + w
            else:
                cities += ', ' + w
            count += 1
        return cities

    def get_address_city_listed(add_list):
        _cities_list = []
        for add in add_list:
            add_city = address.loc[address['id'] == add, 'cityIDAddress.name'].values.item()
            _cities_list.append(add_city)
        cities_list = np.unique(_cities_list)
        return cities_list

    # def materials_value(mat_list, price):
    #     price_list = [5, 5, 10, 10, 12.5, 12.5, 0, price]
    #     total_price = 0
    #     for material, mat_price in zip(mat_list, price_list):
    #         total_price += material * mat_price
    #     return total_price
    #
    # def materials_description(mat_list):
    #     mat_description = ""
    #     count = 0
    #     description_list = ['Embalagem secundária', 'Gelox', 'Isopor 3L', 'Terciária 3L', 'Isopor 7L', 'Terciária 8L',
    #                         'Caixa térmica', 'Gelo Seco', 'Pote 1L']
    #     for material in mat_list:
    #         if material == 0:
    #             count += 1
    #             pass
    #         else:
    #             mat_description += f'{material} {description_list[count]}\n'
    #             count += 1
    #     mat_description = mat_description.strip("\n")
    #     return mat_description

    # def get_material_price(provider_id):
    #     try:
    #         dry_ice_price = provider.loc[provider['id'] == provider_id, 'material_price'].values.item()
    #     except ValueError:
    #         dry_ice_price = 0
    #     return dry_ice_price

    now = dt.datetime.now()
    now_date = dt.datetime.strftime(start_date, '%d-%m-%Y')
    now = dt.datetime.strftime(now, "%H-%M")

    di = dt.datetime.strftime(start_date, '%d/%m/%Y')
    df = dt.datetime.strftime(final_date, '%d/%m/%Y')

    di_dt = dt.datetime.strptime(di, '%d/%m/%Y')
    df_dt = dt.datetime.strptime(df, '%d/%m/%Y')

    di_temp = di_dt - dt.timedelta(days=5)
    df_temp = df_dt + dt.timedelta(days=5)

    di = dt.datetime.strftime(di_temp, '%d/%m/%Y')
    df = dt.datetime.strftime(df_temp, '%d/%m/%Y')

    di_formatted = dt.datetime.strftime(di_temp, '%Y-%m-%d')
    df_formatted = dt.datetime.strftime(df_temp, '%Y-%m-%d')

    pl = {
        "customer_id": None,
        "startDate": f"{di_formatted}",
        "endDate": f"{df_formatted}",
    }

    print("Requisitando relatório de faturamento")

    services_billing = r.request_private(
        link='https://transportebiologico.com.br/api/report/billing/data',
        request_type='post',
        payload=pl,
        nested=True
    )

    print("Requisitando tabelas principais")

    address = r.request_public('https://transportebiologico.com.br/api/public/address')
    services_ongoing = r.request_public('https://transportebiologico.com.br/api/public/service')
    services_finalized = r.request_public(
        link=f'https://transportebiologico.com.br/api/public/service/finalized/?startFilter={di}&endFilter={df}',
        request_type='post'
    )

    sv = pd.concat([services_ongoing, services_finalized], ignore_index=True)

    sv = sv[sv['is_business'] == False]

    # provider = r.request_private('https://transportebiologico.com.br/api/provider')  # FORNECEDOR DE GELO SECO

    df_dt += dt.timedelta(days=1)
    sv.drop(sv[sv['customerIDService.emission_type'] == 'NF'].index, inplace=True)
    sv['collectDateTime'] = pd.to_datetime(sv['serviceIDRequested.collect_date']) - dt.timedelta(hours=3)
    sv['collectDateTime'] = sv['collectDateTime'].dt.tz_localize(None)

    services_billing['collectDateTime'] = pd.to_datetime(
        services_billing['collectDate']
    ) - dt.timedelta(hours=3)
    services_billing['collectDateTime'] = services_billing['collectDateTime'].dt.tz_localize(None)

    sv.drop(sv[sv['collectDateTime'] < di_dt].index, inplace=True)
    sv.drop(sv[sv['collectDateTime'] > df_dt].index, inplace=True)
    if unique is False:
        sv = pd.concat([
            sv[sv['cte_complementary'].isnull()],
            sv[sv['cte_complementary'] == 'nan'],
            sv[sv['cte_complementary'] == '-']
                        ], ignore_index=True)
    # sv.drop(sv[sv['customerIDService.trading_firstname'] == "HPV - HOSPITAL MOINHOS DE VENTO"].index, inplace=True)
    sv = sv[(sv['cte_loglife'].notnull()) &
            (sv['cte_loglife'] != 'nan') &
            (sv['cte_loglife'] != '-') &
            (sv['step'] != 'toCollectService') &
            (sv['step'] != 'collectingService') &
            (sv['step'] != 'toBoardService') &
            (sv['step'] != 'boardingService') &
            (sv['step'] != 'toBoardValidate')]

    # # Client list FILTER START.
    # sv = sv.loc[
    #     (sv['customerIDService.trading_firstname'] == "CERBA-LCA") |
    #     (sv['customerIDService.trading_firstname'] == "PROVET") |
    #     (sv['customerIDService.trading_firstname'] == "FLOW") |
    #     (sv['customerIDService.trading_firstname'] == "ALCHEMYPET MEDICINA DIAGNÓSTICA VETERINÁRIA LTDA.") |
    #     (sv['customerIDService.trading_firstname'] == "LEMOS LABORATORIOS") |
    #     (sv['customerIDService.trading_firstname'] == "LABORATÓRIO KTZ") |
    #     (sv['customerIDService.trading_firstname'] == "NORDD PATOLOGIA")
    # ]
    # # Client list FILTER END.

    sv['origCityList'] = sv['serviceIDRequested.source_address_id'].map(get_address_city_listed)
    sv['destCityList'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city_listed)
    sv['origCity'] = sv['serviceIDRequested.source_address_id'].map(get_address_city)
    sv['destCity'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city)

    sv.sort_values(
        by="protocol", axis=0, ascending=True, inplace=True, kind='quicksort', na_position='last'
    )

    services_billing.sort_values(
        by="protocol", axis=0, ascending=True, inplace=True, kind='quicksort', na_position='last'
    )

    print(sv)

    services_billing = services_billing[services_billing['protocol'].isin(sv['protocol'])]
    sv = sv[sv['protocol'].isin(services_billing['protocol'])]

    services_billing.to_excel('Billing.xlsx', index=False)

    sv['NOME REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_name)
    sv['NOME DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_name)

    services_billing['CTE LOGLIFE'] = services_billing['protocol'].map(get_cte_number)
    services_billing['totalAdditionalCollects'] = services_billing['totalAdditionalCollects'].astype(float)

    # Materials

    # mt = pd.DataFrame(columns=[])
    #
    # sv['priceDryIce'] = sv['serviceIDRequested.provider_id'].map(get_material_price)
    #
    # materials = ['embalagem_secundaria',
    #              'gelox',
    #              'isopor3l',
    #              'terciaria3l',
    #              'isopor7l',
    #              'terciaria8l',
    #              'caixa_termica',
    #              'gelo_seco']
    #
    # for mat in materials:
    #     mt[f'{mat}Extra'] = sv[f'serviceIDRequested.{mat}'] - sv[f'serviceIDRequested.budgetIDService.{mat}']
    #     mt[f'{mat}Extra'] = np.where(mt[f'{mat}Extra'] < 0, 0, mt[f'{mat}Extra'])
    #
    # extra_list = []
    #
    # for rows in mt.itertuples():
    #     extram_list = [rows.embalagem_secundariaExtra,
    #                    rows.geloxExtra,
    #                    rows.isopor3lExtra,
    #                    rows.terciaria3lExtra,
    #                    rows.isopor7lExtra,
    #                    rows.terciaria8lExtra,
    #                    rows.caixa_termicaExtra,
    #                    rows.gelo_secoExtra]
    #
    #     extra_list.append(extram_list)
    #
    # sv = sv.assign(extraMaterials=extra_list)

    report = pd.DataFrame(columns=[])

    report['PROTOCOLO'] = services_billing['protocol']
    report['CLIENTE'] = services_billing['customer']
    report['ETAPA'] = services_billing['step']
    report['TIPO DE SERVIÇO'] = services_billing['serviceType']
    report['DATA COLETA'] = services_billing['collectDateTime'].dt.strftime(date_format='%d/%m/%Y')
    report['CIDADE ORIGEM'] = services_billing['sourceCity']
    report['CIDADE DESTINO'] = services_billing['destinationCity']
    report['CTE LOGLIFE'] = services_billing['CTE LOGLIFE']
    report['VALOR DO ORÇAMENTO'] = services_billing['budgetPrice']
    report['FRANQUIA'] = services_billing['franchising']
    report['PESO TAXADO NO SERVIÇO'] = services_billing['taxedWeight']
    report['VOLUME EMBARQUE'] = services_billing['boardVolume']
    # sv['VALOR TOTAL KG EXTRA'] = np.ceil(
    #     (sv['PESO TAXADO NO SERVIÇO'] - sv['FRANQUIA'])
    # ) * sv['VALOR KG EXTRA']
    # sv['VALOR TOTAL KG EXTRA'] = np.where(
    #     sv['VALOR TOTAL KG EXTRA'] < 0, 0, sv['VALOR TOTAL KG EXTRA'])
    report['VALOR KG EXTRA'] = services_billing['extraKGPrice']
    report['VALOR TOTAL KG EXTRA'] = services_billing['totalExtraKGPrice']
    report['QTD. END. ORIGEM NO SERVIÇO'] = services_billing['serviceSourceAddressQty']
    report['QTD. END. ORIGEM NO ORÇAMENTO'] = services_billing['budgetSourceAddressQty']
    report['VALOR COLETA ADICIONAL'] = services_billing['addCollectPrice']
    report['VALOR TOTAL COLETAS ADICIONAIS'] = services_billing['totalAdditionalCollects'].astype(float)
    report['VALOR COLETAS ADICIONAIS AJUSTADO'] = report['PROTOCOLO'].map(hiae_additional_cost)
    report['VALOR COLETAS ADICIONAIS AJUSTADO'] = report['PROTOCOLO'].map(hiae_dry_ice_cost)
    report['QTD. END. DESTINO NO SERVIÇO'] = services_billing['serviceDestinationAddressQty']
    report['QTD. END. DESTINO NO ORÇAMENTO'] = services_billing['budgetDestinationAddressQty']
    report['VALOR ENTREGA ADICIONAL'] = services_billing['addDeliveryPrice']
    report['VALOR TOTAL ENTREGAS ADICIONAIS'] = services_billing['totalAdditionalDeliveries'].astype(float)
    report['NOME REMETENTE'] = services_billing['sourceAddress']
    report['NOME DESTINATÁRIO'] = services_billing['destinationAddress']
    # sv['MATERIAL EXTRA (CLIENTE)'] = sv['extraMaterials'].map(materials_description)
    report['MATERIAL EXTRA (CLIENTE)'] = services_billing['infoExtraMaterials']
    # sv['CUSTO TOTAL MATERIAL EXTRA (CLIENTE)'] = sv.apply(
    #     lambda x: materials_value(x.extraMaterials, x.priceDryIce), axis=1)
    report['CUSTO TOTAL MATERIAL EXTRA (CLIENTE)'] = services_billing['totalCostExtraMaterials']
    report['PREÇO COLETA SEM SUCESSO'] = services_billing['unsuccessPrice']
    # sv['VALOR TOTAL DO EXTRA'] = sv['CUSTO TOTAL MATERIAL EXTRA (CLIENTE)'] + sv['VALOR TOTAL COLETAS ADICIONAIS'] \
    #                              + sv['VALOR TOTAL ENTREGAS ADICIONAIS'] + sv['VALOR TOTAL KG EXTRA']
    report['VALOR TOTAL DO EXTRA'] = services_billing['additionalChargesTotalValue'].str.lstrip("R$ ").astype(float)
    report['VALOR TOTAL DO EXTRA'] = report['VALOR TOTAL DO EXTRA'] - report['VALOR TOTAL COLETAS ADICIONAIS'] + report['VALOR COLETAS ADICIONAIS AJUSTADO']
    report['OBSERVAÇÕES'] = services_billing['observation']
    sv['VALOR TOTAL DO EXTRA'] = services_billing['additionalChargesTotalValue'].str.lstrip("R$ ").astype(float)
    sv['VALOR TOTAL DO EXTRA'] = np.where(sv['VALOR TOTAL DO EXTRA'] < 0,
                                          0,
                                          sv['VALOR TOTAL DO EXTRA'])

    report = report[report['VALOR TOTAL DO EXTRA'] != 0]

    # report = sv[[
    #     'PROTOCOLO',
    #     'CLIENTE',
    #     'ETAPA',
    #     'TIPO DE SERVIÇO',
    #     'DATA COLETA',
    #     'CIDADE ORIGEM',
    #     'CTE LOGLIFE',
    #     'VALOR DO ORÇAMENTO',
    #     'FRANQUIA',
    #     'PESO TAXADO NO SERVIÇO',
    #     'VOLUME EMBARQUE',
    #     'VALOR KG EXTRA',
    #     'VALOR TOTAL KG EXTRA',
    #     'QTD. END. ORIGEM NO SERVIÇO',
    #     'QTD. END. ORIGEM NO ORÇAMENTO',
    #     'VALOR COLETA ADICIONAL',
    #     'VALOR TOTAL COLETAS ADICIONAIS',
    #     'QTD. END. DESTINO NO SERVIÇO',
    #     'QTD. END. DESTINO NO ORÇAMENTO',
    #     'VALOR ENTREGA ADICIONAL',
    #     'VALOR TOTAL ENTREGAS ADICIONAIS',
    #     'NOME REMETENTE',
    #     'NOME DESTINATÁRIO',
    #     'MATERIAL EXTRA (CLIENTE)',
    #     'CUSTO TOTAL MATERIAL EXTRA (CLIENTE)',
    #     'VALOR TOTAL DO EXTRA',
    #     'OBSERVAÇÕES'
    # ]].copy()

    cte_comp_path = cte_comp_path.replace('/', '\\')

    if unique:
        protocol_entry = cte_cs
        protocol_list = protocol_entry.split(";")
        integer_map = map(int, protocol_list)
        protocol_list = list(integer_map)
        report = report[report['PROTOCOLO'].isin(protocol_list)]
    else:
        protocol_list = report['PROTOCOLO'].to_list()

    excel_file = f'{cte_comp_path}\\CTE complementar-{now_date}-{now}.xlsx'
    csv_file = f'{cte_comp_path}\\Upload complementar-{now_date}-{now}.csv'
    csv_associate = f'{cte_comp_path}\\Associar complementar-{now_date}-{now}.csv'

    cte_folder_path = cte_folder.replace('/', '\\')

    report.to_excel(excel_file, index=False)

    print("Relatório exportado!")

    bot_cte = bot.Bot()

    # bot_cte.open_bsoft(path=filename.get(), login=login.get(), password=password.get())
    current_row = 0

    for protocol in protocol_list:

        report_date = dt.datetime.strftime(dt.datetime.now(), '%d/%m/%Y')

        # tomador_cnpj = sv.loc[sv['protocol'] == protocol, 'customerIDService.cnpj_cpf'].values.item()
        source_add = sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.source_address_id'].values.item()
        destination_add = sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.destination_address_id'].values.item()
        valor = str(report.loc[report['PROTOCOLO'] == protocol, 'VALOR TOTAL DO EXTRA'].values.item())
        uf1 = address.loc[address['id'] == source_add[0], 'state'].values.item()
        uf2 = address.loc[address['id'] == destination_add[0], 'state'].values.item()
        # cnpj_remetente = get_address_cnpj(source_add)
        # cnpj_destinatario = get_address_cnpj(destination_add)
        uf_rem = uf_base.loc[uf_base['Estado'] == uf1, 'UF'].values.item()
        uf_dest = uf_base.loc[uf_base['Estado'] == uf2, 'UF'].values.item()
        icms_obs = uf_base.loc[uf_base['Estado'] == uf_rem, 'Info'].values.item()
        aliq = aliquota_base.loc[aliquota_base['UF'] == uf_rem, uf_dest].values.item()
        cte = sv.loc[sv['protocol'] == protocol, 'cte_loglife'].values.item()
        obs_text = f'Protocolo {protocol} - {icms_obs}'
        aliq_text = float(aliq) * float(valor) * 0.008
        aliq_text = "{:0.2f}".format(aliq_text)

        if uf_rem != "MG":

            obs_text = obs_text.replace('#', aliq_text)
            aliq = "0"

        bot_cte.part3_complimentary(
            cte=cte
        )

        bot_cte.part4(
            tax=str(aliq),
            tp_info=aliq_text,
            icms_text=obs_text,
            uf=uf_rem,
            price=valor,
            complimentary=True
        )

        cte_llm_complimentary = int(bot_cte.get_clipboard())
        
        payload = {
            "data": [
                {
                    "protocol": str(protocol),
                    "cte_complementary": cte_llm_complimentary,
                    "cte_complementary_emission_date": report_date
                }
            ]
        }
        
        r.request_private(
            link="https://transportebiologico.com.br/api/uploads/cte-complementary/json",
            request_type="post",
            payload=payload,
            json=True
        
        )
        report.at[report.index[current_row], 'CTE Complementar'] = cte_llm_complimentary

        cte_file = f'{str(cte_llm_complimentary).zfill(8)}.pdf'

        cte_csv = pd.DataFrame({
            'Protocolo': [protocol],
            'Arquivo PDF': [cte_file],
        })

        report.to_excel(excel_file, index=False)

        cte_csv.to_csv(csv_associate, index=False, encoding='utf-8')
        
        while True:
            try:
                r.post_file("https://transportebiologico.com.br/api/pdf",
                                              f'{cte_folder_path}\\{cte_file}',
                                              upload_type="CTE COMPLEMENTAR",
                                              file_format="application/pdf",
                                              file_type="pdf_files")
                break
            except FileNotFoundError:
                time.sleep(0.5)
                continue

        r.post_file('https://transportebiologico.com.br/api/pdf/associate',
                                     csv_associate,
                                     upload_type="CTE COMPLEMENTAR")

        current_row += 1

    confirmation_pop_up(root, 'Emissões de CTe complementar finalizadas!')


def cte_unique(cal_date, cte_path, cte_folder_path, cte_type, cte_s, volumes, root):
    def get_address_cnpj(add_list):
        _cnpj_list = []
        for add in add_list:
            cnpj = address.loc[address['id'] == add, 'cnpj_cpf'].values.item()
            _cnpj_list.append(cnpj)
        return _cnpj_list

    def get_collector_cnpj(col):
        _cnpj_list = []
        cnpj = collector.loc[collector['id'] == col, 'cnpj'].values.item()
        _cnpj_list.append(cnpj)
        return _cnpj_list

    def get_address_name(add):
        add_lenght = len(add)
        count = 0
        add_total = ''
        for i in range(add_lenght):
            add_name = address.loc[address['id'] == add[i], 'trading_name'].values.item()
            add_branch = address.loc[address['id'] == add[i], 'branch'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = str(add_name)
            else:
                listing = str(count) + '. ' + str(add_name)
            add_total += listing + ' - ' + add_branch + space
        return add_total

    def get_address_meta(add):
        add_lenght = len(add)
        count = 0
        add_total = ''
        for i in range(add_lenght):
            add_street = address.loc[address['id'] == add[i], 'street'].values.item()
            add_num = address.loc[address['id'] == add[i], 'number'].values.item()
            add_dist = address.loc[address['id'] == add[i], 'neighborhood'].values.item()
            add_city = address.loc[address['id'] == add[i], 'cityIDAddress.name'].values.item()
            add_state = address.loc[address['id'] == add[i], 'state'].values.item()
            add_cep = address.loc[address['id'] == add[i], 'cep'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = ''
            else:
                listing = str(count) + '. '
            add_total += listing + add_street.title() + ', ' + add_num + ' - ' + add_dist.title() + ', ' \
                         + add_city.title() + ' - ' + add_state + ', ' + add_cep + space
        return add_total

    def get_address_cnpj_listed(add_list):
        add_lenght = len(add_list)
        count = 0
        add_total = ''
        for add in add_list:
            cnpj = address.loc[address['id'] == add, 'cnpj_cpf'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = ''
            else:
                listing = str(count) + '. '
            add_total += listing + cnpj + space
        return add_total

    def get_collector(col):
        col_name = collector.loc[collector['id'] == col, 'trading_name'].values.item()
        return col_name

    def get_address_city(add):
        add_lenght = len(add)
        add_total = []
        count = 0
        cities = ''
        for i in range(add_lenght):
            add_city = address.loc[address['id'] == add[count], 'cityIDAddress.name'].values.item()
            add_total.append(add_city)
        add_total = np.unique(add_total)
        for p in add_total:
            if count == 0:
                cities = p
            elif count == len(add_total) - 1:
                cities += ', ' + p
            else:
                cities += ', ' + p
            count += 1
        return cities

    di = cal_date
    df = di

    now = dt.datetime.now()
    now_date = dt.datetime.strftime(di, '%d-%m-%Y')
    now = dt.datetime.strftime(now, "%H-%M")

    di = dt.datetime.strftime(di, '%d/%m/%Y')
    df = dt.datetime.strftime(df, '%d/%m/%Y')

    di_dt = dt.datetime.strptime(di, '%d/%m/%Y')
    df_dt = dt.datetime.strptime(df, '%d/%m/%Y')

    di_temp = di_dt - dt.timedelta(days=5)
    df_temp = df_dt + dt.timedelta(days=5)

    di = dt.datetime.strftime(di_temp, '%d/%m/%Y')
    df = dt.datetime.strftime(df_temp, '%d/%m/%Y')

    # Requesting data from API

    address = r.request_public('https://transportebiologico.com.br/api/public/address')
    collector = r.request_public('https://transportebiologico.com.br/api/public/collector')
    services_ongoing = r.request_public('https://transportebiologico.com.br/api/public/service')
    services_finalized = r.request_public(
        f'https://transportebiologico.com.br/api/public/service/finalized/?startFilter={di}&endFilter={df}',
        'post'
    )

    sv = pd.concat([services_ongoing, services_finalized], ignore_index=True)

    sv = sv.loc[sv['is_business'] == False]
    sv = sv.loc[sv['step'] != 'toValidateCancelRequest']

    df_dt += dt.timedelta(days=1)

    sv['collectDateTime'] = pd.to_datetime(sv['serviceIDRequested.collect_date']) - dt.timedelta(hours=3)
    sv['collectDateTime'] = sv['collectDateTime'].dt.tz_localize(None)

    sv['origCity'] = sv['serviceIDRequested.source_address_id'].map(get_address_city)
    sv['destCity'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city)

    sv.drop(sv[sv['collectDateTime'] < di_dt].index, inplace=True)
    sv.drop(sv[sv['collectDateTime'] > df_dt].index, inplace=True)

    sv.to_excel("debug.xlsx")

    report = pd.DataFrame(columns=[])

    report['PROTOCOLO'] = sv['protocol']
    report['CLIENTE'] = sv['customerIDService.trading_firstname']
    report['ETAPA'] = np.select(
        condlist=[
            sv['step'] == 'availableService',
            sv['step'] == 'toAllocateService',
            sv['step'] == 'toDeliveryService',
            sv['step'] == 'deliveringService',
            sv['step'] == 'toLandingService',
            sv['step'] == 'landingService',
            sv['step'] == 'toBoardValidate',
            sv['step'] == 'toCollectService',
            sv['step'] == 'collectingService',
            sv['step'] == 'toBoardService',
            sv['step'] == 'boardingService',
            sv['step'] == 'finishedService'],
        choicelist=[
            'AGUARDANDO DISPONIBILIZAÇÃO', 'AGUARDANDO ALOCAÇÃO', 'EM ROTA DE ENTREGA', 'ENTREGANDO',
            'DISPONÍVEL PARA RETIRADA', 'DESEMBARCANDO', 'VALIDAR EMBARQUE', 'AGENDADO', 'COLETANDO',
            'EM ROTA DE EMBARQUE', 'EMBARCANDO SERVIÇO', 'FINALIZADO'],
        default=''
    )
    report['DATA COLETA'] = sv['collectDateTime'].dt.strftime(date_format='%d/%m/%Y')
    report['PREÇO TRANSPORTE'] = sv['serviceIDRequested.budgetIDService.price']
    report['PREÇO KG EXTRA'] = sv['serviceIDRequested.budgetIDService.price_kg_extra']
    report['NOME REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_name)
    report['CIDADE ORIGEM'] = sv['origCity']
    report['ENDEREÇO REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_meta)
    report['CNPJ/CPF REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_cnpj_listed)
    report['COLETADOR ORIGEM'] = sv['serviceIDRequested.source_collector_id'].map(get_collector)
    report['NOME DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_name)
    report['CIDADE DESTINO'] = sv['destCity']
    report['ENDEREÇO DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_meta)
    report['CNPJ/CPF DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_cnpj_listed)
    report['COLETADOR DESTINO'] = sv['serviceIDRequested.destination_collector_id'].map(get_collector)

    cte_path = cte_path.replace('/', '\\')

    if cte_type == 0:
        excel_file = f'{cte_path}\\Lista CTE-{now_date}-{now}.xlsx'
    else:
        excel_file = f'{cte_path}\\CTe Simbólico-{now_date}-{now}.xlsx'

    csv_file = f'{cte_path}\\Upload-{now_date}-{now}.csv'
    csv_associate = f'{cte_path}\\Associar-{now_date}-{now}.csv'

    cte_folder_path = cte_folder_path.replace('/', '\\')

    protocol_entry = cte_s
    protocol_list = protocol_entry.split(";")

    report = report[report['PROTOCOLO'].isin([int(x) for x in protocol_list])]
    report.to_excel(excel_file, index=False)

    bot_cte = bot.Bot()

    current_row = 0

    print(protocol_list)

    for protocol in protocol_list:

        report_date = dt.datetime.strftime(dt.datetime.now(), '%d/%m/%Y')

        protocol = int(protocol)
        client_type = sv.loc[sv['protocol'] == protocol, 'customerIDService.emission_type'].values.item()
        tomador_cnpj = sv.loc[sv['protocol'] == protocol, 'customerIDService.cnpj_cpf'].values.item()
        source_add = sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.source_address_id'].values.item()
        destination_add = sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.destination_address_id'].values.item()
        uf1 = address.loc[address['id'] == source_add[0], 'state'].values.item()
        uf2 = address.loc[address['id'] == destination_add[0], 'state'].values.item()
        uf_rem = uf_base.loc[uf_base['Estado'] == uf1, 'UF'].values.item()
        uf_dest = uf_base.loc[uf_base['Estado'] == uf2, 'UF'].values.item()
        icms_obs = uf_base.loc[uf_base['Estado'] == uf_rem, 'Info'].values.item()
        aliq = aliquota_base.loc[aliquota_base['UF'] == uf_rem, uf_dest].values.item()
        obs_text = f'Protocolo {protocol} - {icms_obs}'
        if cte_type == 0:
            valor = str(sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.price'].values.item())
        else:
            valor = "5"

        aliq_text = float(aliq) * float(valor) * 0.008
        aliq_text = "{:0.2f}".format(aliq_text)

        if uf_rem != "MG":
            obs_text = obs_text.replace('#', aliq_text)
            aliq = "0"

        cliente = sv.loc[sv['protocol'] == protocol, 'customerIDService.trading_firstname'].values.item()

        if cte_type == 0:
            if client_type == "NF":
                confirmation_pop_up(root, f"Protocolo {protocol} não pode ser emitido como CTe normal!")
                break
            valor = str(sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.budgetIDService.price'].values.item())
            cnpj_remetente = get_address_cnpj(source_add)
            cnpj_destinatario = get_address_cnpj(destination_add)
            tipo_cte = None
            vols = volumes
            if tomador_cnpj in cnpj_remetente:
                tomador = "Remetente"
                cnpj_remetente = [tomador_cnpj]
            elif tomador_cnpj in cnpj_destinatario:
                tomador = "Destinatário"
                cnpj_destinatario = [tomador_cnpj]
            else:
                tomador = "Outro"
        else:
            source_collector = sv.loc[
                sv['protocol'] == protocol, 'serviceIDRequested.source_collector_id'
            ].values.item()
            dest_collector = sv.loc[
                sv['protocol'] == protocol, 'serviceIDRequested.destination_collector_id'
            ].values.item()
            valor = "5,00"
            cnpj_remetente = get_collector_cnpj(source_collector)
            if cliente == 'METHODOS LABORATORIO':
                cnpj_destinatario = ['30296133000118']
            else:
                cnpj_destinatario = get_collector_cnpj(dest_collector)
            tipo_cte = 1
            vols = volumes
            if cnpj_remetente[0] in [
                "17.062.517/0001-08", "17.062.517/0002-99", "50.699.404/0001-93"
            ] or cnpj_remetente in [
                "17.062.517/0001-08", "17.062.517/0002-99", "50.699.404/0001-93"
            ]:
                tomador = "Destinatário"
            else:
                tomador = "Remetente"

        print('Emissão iniciada!')

        bot_cte.action(
            cnpj_sender=cnpj_remetente,
            cnpj_receiver=cnpj_destinatario,
            payer=tomador,
            payer_cnpj=tomador_cnpj
        )
        bot_cte.part3_normal(
            cte_instance=tipo_cte,
            volumes=vols
        )
        bot_cte.part4(
            tax=str(aliq),
            tp_info=str(aliq_text),
            uf=uf_rem,
            icms_text=obs_text,
            price=valor
        )

        cte_llm = int(bot_cte.get_clipboard())

        cte_file = f'{str(cte_llm).zfill(8)}.pdf'
        
        if cte_type == 0:
        
            payload = {
                "data": [
                    {
                        "protocol": str(protocol),
                        "cte_loglife": cte_llm,
                        "cte_loglife_emission_date": report_date
                    }
                ]
            }
        
        else:
            
            payload = {
                "data": [
                    {
                        "protocol": str(protocol),
                        "cte_loglife": cte_llm,
                        "cte_loglife_emission_date": report_date,
                        "symbolic": True
                    }
                ]
            }
        

        r.request_private(
            link="https://transportebiologico.com.br/api/uploads/cte-loglife/json",
            request_type="post",
            payload=payload,
            json=True
        )

        report.at[report.index[current_row], 'CTE LOGLIFE'] = cte_llm
        report.to_excel(excel_file, index=False)

        csv_report = pd.DataFrame({
            'Protocolo': [int(protocol)],
            'CTE Loglife': [cte_llm],
            'Data Emissão CTE': [report_date]
        })

        csv_report = csv_report.astype(str)
        csv_report = csv_report.replace(to_replace="\.0+$", value="", regex=True)

        csv_report.to_csv(csv_file, index=False)

        if cte_type == 0:
            r.post_file('https://transportebiologico.com.br/api/uploads/cte-loglife', csv_file)

        associate = pd.DataFrame({
            'Protocolo': [int(protocol)],
            'Arquivo PDF': [cte_file],
        })

        associate.to_csv(csv_associate, index=False, encoding='utf-8')

        while True:
            try:
                r.post_file("https://transportebiologico.com.br/api/pdf",
                            f'{cte_folder_path}\\{cte_file}',
                            upload_type="CTE LOGLIFE",
                            file_format="application/pdf",
                            file_type="pdf_files")
                break
            except FileNotFoundError:
                time.sleep(0.5)
                continue

        r.post_file('https://transportebiologico.com.br/api/pdf/associate',
                    csv_associate,
                    upload_type="CTE LOGLIFE")

        os.remove(csv_file)
        os.remove(csv_associate)

        if cte_type == 1:
            os.remove(excel_file)

        current_row += 1

    confirmation_pop_up(root, "Emissões realizadas com sucesso!")


def cte_symbolic(start_date, final_date, folderpath, cte_folder, root):

    def get_address_city(add):
        add_lenght = len(add)
        add_total = []
        count = 0
        cities = ''
        for i in range(add_lenght):
            add_city = address.loc[address['id'] == add[i], 'cityIDAddress.name'].values.item()
            add_total.append(add_city)
        add_total = np.unique(add_total)
        for p in add_total:
            if count == 0:
                cities = p
            elif count == len(add_total) - 1:
                cities += ', ' + p
            else:
                cities += ', ' + p
            count += 1
        return cities

    def get_address_city_listed(add_list):
        _cities_list = []
        for add in add_list:
            add_city = address.loc[address['id'] == add, 'cityIDAddress.name'].values.item()
            _cities_list.append(add_city)
        cities_list = np.unique(_cities_list)
        return cities_list

    def get_collector(col):
        col_name = collector.loc[collector['id'] == col, 'trading_name'].values.item()
        return col_name

    def get_transp(id_t):
        try:
            if id_t is not None:
                transp = bases.loc[bases['id'] == id_t, 'shippingIDBranch.company_name'].values.item()
            else:
                transp = ""
        except ValueError:
            transp = ""
        return transp

    def get_collector_cnpj(col):
        _cnpj_list = []
        cnpj = collector.loc[collector['id'] == col, 'cnpj'].values.item()
        _cnpj_list.append(cnpj)
        return _cnpj_list

    now = dt.datetime.now()
    now_date = dt.datetime.strftime(start_date, '%d-%m-%Y')
    now = dt.datetime.strftime(now, "%H-%M")

    di = dt.datetime.strftime(start_date, '%d/%m/%Y')
    df = dt.datetime.strftime(final_date, '%d/%m/%Y')

    di_dt = dt.datetime.strptime(di, '%d/%m/%Y')
    df_dt = dt.datetime.strptime(df, '%d/%m/%Y')

    di_temp = di_dt - dt.timedelta(days=5)
    df_temp = df_dt + dt.timedelta(days=5)

    di = dt.datetime.strftime(di_temp, '%d/%m/%Y')
    df = dt.datetime.strftime(df_temp, '%d/%m/%Y')

    bases = r.request_public('https://transportebiologico.com.br/api/public/branch')
    address = r.request_public('https://transportebiologico.com.br/api/public/address')
    collector = r.request_public('https://transportebiologico.com.br/api/public/collector')
    services_ongoing = r.request_public('https://transportebiologico.com.br/api/public/service')
    services_finalized = r.request_public(
        f'https://transportebiologico.com.br/api/public/service/finalized/?startFilter={di}&endFilter={df}',
        'post'
    )

    sv = pd.concat([services_ongoing, services_finalized], ignore_index=True)
    sv = sv.loc[sv['is_business'] == False]

    df_dt += dt.timedelta(days=1)

    sv['collectDateTime'] = pd.to_datetime(sv['serviceIDRequested.collect_date']) - dt.timedelta(hours=3)
    sv['collectDateTime'] = sv['collectDateTime'].dt.tz_localize(None)

    sv.drop(sv[sv['collectDateTime'] < di_dt].index, inplace=True)
    sv.drop(sv[sv['collectDateTime'] > df_dt].index, inplace=True)

    sv.to_excel('ServicesAPI.xlsx', index=True)

    sv = pd.concat([sv[sv['cte_loglife_pdf_associated'].isnull()],
                    sv[sv['cte_loglife_pdf_associated'] == 'nan'],
                    sv[sv['cte_loglife_pdf_associated'] == '-']], ignore_index=True)

    sv.drop(sv[sv['customerIDService.emission_type'] == 'CTE'].index, inplace=True)
    sv.drop(sv[sv['customerIDService.emission_type'] == 'AMBOS'].index, inplace=True)
    sv.drop(sv[sv['serviceIDRequested.service_type'] == 'DEDICADO'].index, inplace=True)
    sv.drop(sv[sv['customerIDService.trading_firstname'] == 'BIOCLINICO'].index, inplace=True)
    sv.drop(sv[sv['customerIDService.trading_firstname'] == 'ONCOBIO SERVIÇOS DE SAÚDE'].index, inplace=True)
    # sv.drop(sv[sv['customerIDService.trading_firstname'] == 'OC PRECISION MEDICINE'].index, inplace=True)

    sv['TRANSPORTADORA'] = sv['serviceIDRequested.source_branch_id'].map(get_transp)

    sv = pd.concat([
        sv[sv['TRANSPORTADORA'] == 'AZUL CARGO'],
        sv[sv['TRANSPORTADORA'] == 'AGUIA BRANCA ENCOMENDAS'],
        sv[sv['TRANSPORTADORA'] == 'BRASIL SUL'],
        sv[sv['TRANSPORTADORA'] == 'SOL CARGAS'],
        sv[sv['TRANSPORTADORA'] == 'JEM']
    ],
        ignore_index=True)

    sv['origCityList'] = sv['serviceIDRequested.source_address_id'].map(get_address_city_listed)
    sv['destCityList'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city_listed)
    sv['origCity'] = sv['serviceIDRequested.source_address_id'].map(get_address_city)
    sv['destCity'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city)

    sv.sort_values(
        by="protocol", axis=0, ascending=True, inplace=True, kind='quicksort', na_position='last'
    )

    report = pd.DataFrame(columns=[])

    report['PROTOCOLO'] = sv['protocol']
    report['CLIENTE'] = sv['customerIDService.trading_firstname']
    report['ETAPA'] = np.select(
        condlist=[
            sv['step'] == 'availableService',
            sv['step'] == 'toAllocateService',
            sv['step'] == 'toDeliveryService',
            sv['step'] == 'deliveringService',
            sv['step'] == 'toLandingService',
            sv['step'] == 'landingService',
            sv['step'] == 'toBoardValidate',
            sv['step'] == 'toCollectService',
            sv['step'] == 'collectingService',
            sv['step'] == 'toBoardService',
            sv['step'] == 'boardingService',
            sv['step'] == 'finishedService'],
        choicelist=[
            'AGUARDANDO DISPONIBILIZAÇÃO', 'AGUARDANDO ALOCAÇÃO', 'EM ROTA DE ENTREGA', 'ENTREGANDO',
            'DISPONÍVEL PARA RETIRADA', 'DESEMBARCANDO', 'VALIDAR EMBARQUE', 'AGENDADO', 'COLETANDO',
            'EM ROTA DE EMBARQUE', 'EMBARCANDO SERVIÇO', 'FINALIZADO'],
        default=0
    )
    report['DATA COLETA'] = sv['collectDateTime'].dt.strftime(date_format='%d/%m/%Y')
    report['CIDADE ORIGEM'] = sv['origCity']
    report['COLETADOR ORIGEM'] = sv['serviceIDRequested.source_collector_id'].map(get_collector)
    report['CIDADE DESTINO'] = sv['destCity']
    report['COLETADOR DESTINO'] = sv['serviceIDRequested.destination_collector_id'].map(get_collector)

    cte_path = folderpath
    cte_path = cte_path.replace('/', '\\')

    excel_file = f'{cte_path}\\Lista CTE Simbólico-{now_date}-{now}.xlsx'
    csv_file = f'{cte_path}\\Upload Simbólicos-{now_date}-{now}.csv'
    csv_associate = f'{cte_path}\\Associar Simbólicos-{now_date}-{now}.csv'

    cte_folder_path = cte_folder.replace('/', '\\')

    report.to_excel(excel_file, index=False)

    print("Relatório exportado!")

    bot_cte = bot.Bot()

    # bot_cte.open_bsoft(path=filename.get(), login=login.get(), password=password.get())
    current_row = 0

    for protocol in sv['protocol']:

        report_date = dt.datetime.strftime(dt.datetime.now(), '%d/%m/%Y')
        source_add = sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.source_address_id'].values.item()
        destination_add = sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.destination_address_id'].values.item()
        valor = str(sv.loc[sv['protocol'] == protocol, 'serviceIDRequested.budgetIDService.price'].values.item())
        uf1 = address.loc[address['id'] == source_add[0], 'state'].values.item()
        uf2 = address.loc[address['id'] == destination_add[0], 'state'].values.item()
        uf_rem = uf_base.loc[uf_base['Estado'] == uf1, 'UF'].values.item()
        uf_dest = uf_base.loc[uf_base['Estado'] == uf2, 'UF'].values.item()
        icms_obs = uf_base.loc[uf_base['Estado'] == uf_rem, 'Info'].values.item()
        aliq = aliquota_base.loc[aliquota_base['UF'] == uf_rem, uf_dest].values.item()
        obs_text = f'Protocolo {protocol} - {icms_obs}'
        aliq_text = float(aliq) * float(valor) * 0.008
        aliq_text = "{:0.2f}".format(aliq_text)

        if uf_rem != "MG":

            obs_text = obs_text.replace('#', aliq_text)
            aliq = "0"

        source_collector = sv.loc[
            sv['protocol'] == protocol, 'serviceIDRequested.source_collector_id'
        ].values.item()
        dest_collector = sv.loc[
            sv['protocol'] == protocol, 'serviceIDRequested.destination_collector_id'
        ].values.item()
        valor = "5,00"
        cliente = sv.loc[sv['protocol'] == protocol, 'customerIDService.trading_firstname'].values.item()
        cnpj_remetente = get_collector_cnpj(source_collector)
        if cliente == 'METHODOS LABORATORIO':
            cnpj_destinatario = ['30296133000118']
        else:
            cnpj_destinatario = get_collector_cnpj(dest_collector)
        if cnpj_destinatario == ['12611020000188'] or cnpj_destinatario == ['12.611.020/0001-88']:
            cnpj_destinatario = ['09464343000181']
        tipo_cte = 1
        vols = 1
        if cnpj_remetente[0] in [
            "17.062.517/0001-08", "17.062.517/0002-99", "50.699.404/0001-93"
        ] or cnpj_remetente in [
            "17.062.517/0001-08", "17.062.517/0002-99", "50.699.404/0001-93"
        ]:
            tomador = "Destinatário"
        else:
            tomador = "Remetente"

        bot_cte.action(
            cnpj_sender=cnpj_remetente,
            cnpj_receiver=cnpj_destinatario,
            payer=tomador
        )
        bot_cte.part3_normal(
            cte_instance=tipo_cte,
            volumes=vols
        )
        bot_cte.part4(
            tax=str(aliq),
            tp_info=aliq_text,
            uf=uf_rem,
            icms_text=obs_text,
            price=valor
        )

        cte_llm = int(bot_cte.get_clipboard())
        
        payload = {
            "data": [
                {
                    "protocol": int(protocol),
                    "cte_loglife": cte_llm,
                    "cte_loglife_emission_date": report_date,
                    "symbolic": True
                }
            ]
        }
        
        r.request_private(
            link="https://transportebiologico.com.br/api/uploads/cte-loglife/json",
            request_type="post",
            payload=payload,
            json=True
        )

        cte_file = f'{str(cte_llm).zfill(8)}.pdf'

        report.at[report.index[current_row], 'CTE LOGLIFE'] = cte_llm
        report.to_excel(excel_file, index=False)

        associate = pd.DataFrame({
            'Protocolo': [int(protocol)],
            'Arquivo PDF': [cte_file],
        })

        associate.to_csv(csv_associate, index=False, encoding='utf-8')

        while True:
            try:
                r.post_file("https://transportebiologico.com.br/api/pdf",
                            f'{cte_folder_path}\\{cte_file}',
                            upload_type="CTE LOGLIFE",
                            file_format="application/pdf",
                            file_type="pdf_files")
                break
            except FileNotFoundError:
                time.sleep(0.5)
                continue

        r.post_file('https://transportebiologico.com.br/api/pdf/associate',
                    csv_associate,
                    upload_type="CTE LOGLIFE")

        os.remove(csv_file)
        os.remove(csv_associate)

        os.remove(excel_file)

        current_row += 1

    confirmation_pop_up(root, "Emissões realizadas com sucesso!")


def cte_list_grouped(start_date, final_date, folderpath, cte_folder, root):
    def get_address_name(address_id):
        add_lenght = len(address_id)
        count = 0
        add_total = ''
        for i in range(add_lenght):
            add_name = address.loc[address['id'] == address_id[i], 'trading_name'].values.item()
            add_branch = address.loc[address['id'] == address_id[i], 'branch'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = str(add_name)
            else:
                listing = str(count) + '. ' + str(add_name)
            add_total += listing + ' - ' + add_branch + space
        return add_total

    def get_address_meta(add):
        add_lenght = len(add)
        count = 0
        add_total = ''
        for i in range(add_lenght):
            add_street = address.loc[address['id'] == add[i], 'street'].values.item()
            add_num = address.loc[address['id'] == add[i], 'number'].values.item()
            add_dist = address.loc[address['id'] == add[i], 'neighborhood'].values.item()
            add_city = address.loc[address['id'] == add[i], 'cityIDAddress.name'].values.item()
            add_state = address.loc[address['id'] == add[i], 'state'].values.item()
            add_cep = address.loc[address['id'] == add[i], 'cep'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = ''
            else:
                listing = str(count) + '. '
            add_total += listing + add_street.title() + ', ' + add_num + ' - ' + add_dist.title() + ', ' \
                         + add_city.title() + ' - ' + add_state + ', ' + add_cep + space
        return add_total

    def get_address_cnpj_listed(add_list):
        add_lenght = len(add_list)
        count = 0
        add_total = ''
        for add in add_list:
            cnpj = address.loc[address['id'] == add, 'cnpj_cpf'].values.item()
            count += 1
            if count != add_lenght:
                space = "\n"
            else:
                space = ''
            if add_lenght == 1:
                listing = ''
            else:
                listing = str(count) + '. '
            add_total += listing + cnpj + space
        return add_total

    def get_address_cnpj(add_list):
        _cnpj_list = []
        for add in add_list:
            cnpj = address.loc[address['id'] == add, 'cnpj_cpf'].values.item()
            _cnpj_list.append(cnpj)
        return _cnpj_list

    def get_address_city(add):
        add_lenght = len(add)
        add_total = []
        count = 0
        cities = ''
        for i in range(add_lenght):
            add_city = address.loc[address['id'] == add[i], 'cityIDAddress.name'].values.item()
            add_total.append(add_city)
        add_total = np.unique(add_total)
        for p in add_total:
            if count == 0:
                cities = p
            elif count == len(add_total) - 1:
                cities += ', ' + p
            else:
                cities += ', ' + p
            count += 1
        return cities

    def get_address_city_listed(add_list):
        _cities_list = []
        for add in add_list:
            add_city = address.loc[address['id'] == add, 'cityIDAddress.name'].values.item()
            _cities_list.append(add_city)
        cities_list = np.unique(_cities_list)
        return cities_list

    def get_collector(col):
        col_name = collector.loc[collector['id'] == col, 'trading_name'].values.item()
        return col_name

    def get_additional_values(protocol):
        try:
            additional_value = services_billing.loc[services_billing['protocol'] == protocol, 'VALORES ADICIONAIS'].values.item()
        except Exception as e:
            additional_value = 0
            print(e)

        return additional_value

    # def get_transp(id_t):
    #     try:
    #         if id_t is not None:
    #             transp = bases.loc[bases['id'] == id_t, 'shippingIDBranch.company_name'].values.item()
    #         else:
    #             transp = ""
    #     except ValueError:
    #         transp = ""
    #     return transp

    now = dt.datetime.now()
    now_date = dt.datetime.strftime(start_date, '%d-%m-%Y')
    now = dt.datetime.strftime(now, "%H-%M")

    di = dt.datetime.strftime(start_date, '%d/%m/%Y')
    df = dt.datetime.strftime(final_date, '%d/%m/%Y')

    di_dt = dt.datetime.strptime(di, '%d/%m/%Y')
    df_dt = dt.datetime.strptime(df, '%d/%m/%Y')

    di_temp = di_dt - dt.timedelta(days=5)
    df_temp = df_dt + dt.timedelta(days=5)

    di = dt.datetime.strftime(di_temp, '%d/%m/%Y')
    df = dt.datetime.strftime(df_temp, '%d/%m/%Y')

    di_formatted = dt.datetime.strftime(di_temp, '%Y-%m-%d')
    df_formatted = dt.datetime.strftime(df_temp, '%Y-%m-%d')

    address = r.request_public('https://transportebiologico.com.br/api/public/address')
    collector = r.request_public('https://transportebiologico.com.br/api/public/collector')
    services_ongoing = r.request_public('https://transportebiologico.com.br/api/public/service')
    services_finalized = r.request_public(
        f'https://transportebiologico.com.br/api/public/service/finalized/?startFilter={di}&endFilter={df}',
        'post'
    )

    pl = {

        "shipping_id": None,
        "is_driver": False,
        "customer_id": None,
        "collector_id": None,
        "startFilter": f"{di_formatted}T00:00:00.000-03:00",
        "endFilter": f"{df_formatted}T23:59:00.000-03:00",
        "is_customer": False,
        "is_collector": False
    }

    services_billing = r.request_private(
        link='https://transportebiologico.com.br/api/report/billing',
        request_type='post',
        payload=pl
    )

    sv = pd.concat([services_ongoing, services_finalized], ignore_index=True)
    sv = sv.loc[sv['is_business'] == False]

    clients = [
        'OC PRECISION MEDICINE',
        'IRA INSTITUTO ROBERTO ALVARENGA LTDA',
        'MICROIMAGEM LAB DE ANATOMIA PAT E CITOPATOLOGIA SC LTDA'
    ]

    sv = sv.loc[
        sv['customerIDService.trading_firstname'].isin(clients)
    ]
    sv.drop(sv[sv['step'] == 'toValidateCancelRequest'].index, inplace=True)

    df_dt += dt.timedelta(days=1)

    sv['collectDateTime'] = pd.to_datetime(sv['serviceIDRequested.collect_date']) - dt.timedelta(hours=3)
    sv['collectDateTime'] = sv['collectDateTime'].dt.tz_localize(None)

    sv.drop(sv[sv['collectDateTime'] < di_dt].index, inplace=True)
    sv.drop(sv[sv['collectDateTime'] > df_dt].index, inplace=True)
    sv.to_excel('ServicesAPI.xlsx', index=True)

    sv = pd.concat([sv[sv['cte_loglife'].isnull()],
                    sv[sv['cte_loglife'] == 'nan'],
                    sv[sv['cte_loglife'] == '-']], ignore_index=True)

    sv.drop(sv[sv['customerIDService.trading_firstname'] == 'LOGLIFE'].index, inplace=True)
    sv.drop(sv[sv['serviceIDRequested.budgetIDService.price'] == 0].index, inplace=True)

    sv['origCityList'] = sv['serviceIDRequested.source_address_id'].map(get_address_city_listed)
    sv['destCityList'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city_listed)
    sv['origCity'] = sv['serviceIDRequested.source_address_id'].map(get_address_city)
    sv['destCity'] = sv['serviceIDRequested.destination_address_id'].map(get_address_city)

    sv['origState'] = sv['serviceIDRequested.source_address_id'] \
        .apply(lambda ids: address.loc[address['id'] == ids[0], 'state'].item())
    sv['destState'] = sv['serviceIDRequested.destination_address_id'] \
        .apply(lambda ids: address.loc[address['id'] == ids[0], 'state'].item())

    sv['origIsCapital'] = sv.apply(
        lambda r: (
                state_capitals.get(r['origState'], '')
                .strip()
                .lower()
                in str(r['origCity']).strip().lower()
        ),
        axis=1
    )

    sv['destIsCapital'] = sv.apply(
        lambda r: (
                state_capitals.get(r['destState'], '')
                .strip()
                .lower()
                in str(r['destCity']).strip().lower()
        ),
        axis=1
    )

    sv['isCapitalRoute'] = sv['origIsCapital'] & sv['destIsCapital']

    sv.sort_values(
        by=['isCapitalRoute', 'protocol'],
        ascending=[False, True],
        inplace=True,
        kind='quicksort',
        na_position='last'
    )

    sv.drop(columns=['origState', 'destState',
                     'origIsCapital', 'destIsCapital',
                     'isCapitalRoute'], inplace=True)

    services_billing['VALORES ADICIONAIS'] = (services_billing['additionalChargesTotalValue']
                                              .str
                                              .lstrip("R$ ")
                                              .astype(float))

    services_billing['VALORES ADICIONAIS'] = np.where(services_billing['VALORES ADICIONAIS'] < 0,
                                                      0,
                                                      services_billing['VALORES ADICIONAIS'])

    sv['VALORES ADICIONAIS'] = sv['protocol'].map(get_additional_values)

    sv['VALOR TOTAL'] = sv['serviceIDRequested.budgetIDService.price'] + sv['VALORES ADICIONAIS']

    sv['origUF'] = sv['serviceIDRequested.source_address_id'] \
        .apply(lambda ids: address.loc[address['id'] == ids[0], 'state'].item())
    sv['destUF'] = sv['serviceIDRequested.destination_address_id'] \
        .apply(lambda ids: address.loc[address['id'] == ids[0], 'state'].item())

    # 0.5) Remove if origin and destination UF are the same
    sv = sv[sv['origUF'] != sv['destUF']]

    # 1) Group by origin/destination state instead of city
    agg = sv.groupby(['origUF', 'destUF']).agg(
        total_price=('serviceIDRequested.budgetIDService.price', 'sum'),
        total_additional_price=('VALORES ADICIONAIS', 'sum'),
        final_value=('VALOR TOTAL', 'sum'),
        protocols=('protocol', lambda x: ' ,'.join(x.astype(str)))
    ).reset_index()

    # 2) Pick a representative for all of the other columns
    first_entries = (
        sv
        .groupby(['origUF', 'destUF'], group_keys=False)
        .nth(0)  # pick the 0th row of each group
        .reset_index()  # now you *can* reset_index()
    )

    # 3) Merge sums & concatenations back onto those reps
    grouped_sv = (
        agg
        .merge(first_entries,
               on=['origUF', 'destUF'],
               how='left',
               suffixes=('', '_rep'))
    )

    # 4) Overwrite price & protocol for the downstream code
    grouped_sv['protocol'] = grouped_sv['protocols']

    # Now replace your old `sv` with this new one:
    sv = grouped_sv

    report = pd.DataFrame(columns=[])

    report['PROTOCOLO'] = sv['protocol']
    report['CLIENTE'] = sv['customerIDService.trading_firstname']
    report['VALOR ORÇAMENTO'] = sv['total_price']
    report['VALORES ADICIONAIS'] = sv['total_additional_price']
    report['VALOR TOTAL'] = sv['final_value']
    report['NOME REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_name)
    report['CIDADE ORIGEM'] = sv['origCity']
    report['ENDEREÇO REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_meta)
    report['CNPJ/CPF REMETENTE'] = sv['serviceIDRequested.source_address_id'].map(get_address_cnpj_listed)
    report['COLETADOR ORIGEM'] = sv['serviceIDRequested.source_collector_id'].map(get_collector)
    report['NOME DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_name)
    report['CIDADE DESTINO'] = sv['destCity']
    report['ENDEREÇO DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_meta)
    report['CNPJ/CPF DESTINATÁRIO'] = sv['serviceIDRequested.destination_address_id'].map(get_address_cnpj_listed)
    report['COLETADOR DESTINO'] = sv['serviceIDRequested.destination_collector_id'].map(get_collector)

    cte_path = folderpath
    cte_path = cte_path.replace('/', '\\')

    excel_file = f'{cte_path}\\Lista CTE-{now_date}-{now}.xlsx'
    csv_file = f'{cte_path}\\Upload-{now_date}-{now}.csv'
    csv_associate = f'{cte_path}\\Associar-{now_date}-{now}.csv'

    cte_folder_path = cte_folder.replace('/', '\\')

    report.to_excel(excel_file, index=False)

    print("Relatório exportado!")
    # but when you get to your emission loop, do:

    bot_cte = bot.Bot()
    current_row = 0

    for _, row in sv.iterrows():
        # now `row['protocol']` is something like "1001 ,1002 ,1003"
        # and `row['serviceIDRequested.budgetIDService.price']` is the sum

        report_date = dt.datetime.now().strftime('%d/%m/%Y')
        prot_list = row['protocol']
        valor = str(row['final_value'])

        # take any of the representative fields you need:
        tomador_cnpj = row['customerIDService.cnpj_cpf']
        source_add = row['serviceIDRequested.source_address_id']
        destination_add = row['serviceIDRequested.destination_address_id']
        uf1 = address.loc[address['id'] == source_add[0], 'state'].item()
        uf2 = address.loc[address['id'] == destination_add[0], 'state'].item()
        cnpj_remetente = get_address_cnpj(source_add)
        cnpj_destinatario = get_address_cnpj(destination_add)
        uf_rem = uf_base.loc[uf_base['Estado'] == uf1, 'UF'].values.item()
        uf_dest = uf_base.loc[uf_base['Estado'] == uf2, 'UF'].values.item()
        icms_obs = uf_base.loc[uf_base['Estado'] == uf_rem, 'Info'].values.item()
        aliq = aliquota_base.loc[aliquota_base['UF'] == uf_rem, uf_dest].values.item()
        obs_text = f'Protocolos {prot_list} - {icms_obs}'
        aliq_text = float(aliq) * float(valor) * 0.008
        aliq_text = "{:0.2f}".format(aliq_text)

        if uf_rem != "MG":

            obs_text = obs_text.replace('#', aliq_text)
            aliq = "0"

        if tomador_cnpj in cnpj_remetente:
            tomador = "Remetente"
            cnpj_remetente = [tomador_cnpj]
        elif tomador_cnpj in cnpj_destinatario:
            tomador = "Destinatário"
            cnpj_destinatario = [tomador_cnpj]
        else:
            tomador = "Outro"

        # replace single‐protocol calls with the grouped ones:
        bot_cte.action(
            cnpj_sender=get_address_cnpj(source_add),
            cnpj_receiver=get_address_cnpj(destination_add),
            payer=tomador,
            payer_cnpj=tomador_cnpj
        )
        bot_cte.part3_normal()
        bot_cte.part4(
            tax=str(aliq),
            tp_info=aliq_text,
            uf=uf_rem,
            icms_text=f'Protocolos {prot_list} - {obs_text}',
            price=valor
        )

        cte_llm = int(bot_cte.get_clipboard())
        report.at[current_row, 'CTE LOGLIFE'] = cte_llm

        csv_report = pd.DataFrame({
            'Protocolo': [prot_list],
            'CTE Loglife': [cte_llm],
            'Data Emissão CTE': [report_date]
        })

        cte_file = f'{str(cte_llm).zfill(8)}.pdf'

        cte_csv = pd.DataFrame({
            'Protocolo': [prot_list],
            'Arquivo PDF': [cte_file],
        })

        report.to_excel(
            excel_file,
            index=False
        )

        csv_report = csv_report.astype(str)
        csv_report = csv_report.replace(to_replace="\.0+$", value="", regex=True)

        csv_report.to_csv(csv_file, index=False, encoding='utf-8')

        cte_csv.to_csv(csv_associate, index=False, encoding='utf-8')

        r.post_file('https://transportebiologico.com.br/api/uploads/cte-loglife', csv_file)

        while True:
            try:
                r.post_file("https://transportebiologico.com.br/api/pdf",
                            f'{cte_folder_path}\\{cte_file}',
                            upload_type="CTE LOGLIFE",
                            file_format="application/pdf",
                            file_type="pdf_files")
                break
            except FileNotFoundError:
                time.sleep(0.5)
                continue

        r.post_file('https://transportebiologico.com.br/api/pdf/associate',
                    csv_associate,
                    upload_type="CTE LOGLIFE")

        os.remove(csv_associate)

        current_row += 1

    confirmation_pop_up(root, "Emissões realizadas com sucesso!")


def clear_cte_number(start_date, final_date, folderpath, root):

    cte_path = folderpath
    cte_path = cte_path.replace('/', '\\')

    clear_cte_file = f'{cte_path}\\LimparCTE.csv'

    di = dt.datetime.strftime(start_date, '%d/%m/%Y')
    df = dt.datetime.strftime(final_date, '%d/%m/%Y')

    di_dt = dt.datetime.strptime(di, '%d/%m/%Y')
    df_dt = dt.datetime.strptime(df, '%d/%m/%Y')

    di_temp = di_dt - dt.timedelta(days=5)
    df_temp = df_dt + dt.timedelta(days=5)

    di = dt.datetime.strftime(di_temp, '%d/%m/%Y')
    df = dt.datetime.strftime(df_temp, '%d/%m/%Y')

    services_ongoing = r.request_public('https://transportebiologico.com.br/api/public/service')
    services_finalized = r.request_public(
        f'https://transportebiologico.com.br/api/public/service/finalized/?startFilter={di}&endFilter={df}',
        'post'
    )
    sv = pd.concat([services_ongoing, services_finalized], ignore_index=True)
    sv = sv.loc[sv['is_business'] == False]

    df_dt += dt.timedelta(days=1)

    sv['collectDateTime'] = pd.to_datetime(sv['serviceIDRequested.collect_date']) - dt.timedelta(hours=3)
    sv['collectDateTime'] = sv['collectDateTime'].dt.tz_localize(None)

    sv.drop(sv[sv['collectDateTime'] < di_dt].index, inplace=True)
    sv.drop(sv[sv['collectDateTime'] > df_dt].index, inplace=True)

    sv.drop(sv[sv['customerIDService.emission_type'] == 'CTE'].index, inplace=True)
    sv.drop(sv[sv['customerIDService.emission_type'] == 'AMBOS'].index, inplace=True)
    sv.drop(sv[sv['cte_loglife'].isnull()].index, inplace=True)
    sv.drop(sv[sv['cte_loglife'] == 'nan'].index, inplace=True)
    sv.drop(sv[sv['cte_loglife'] == '-'].index, inplace=True)

    sv.sort_values(
        by="protocol", axis=0, ascending=True, inplace=True, kind='quicksort', na_position='last'
    )

    for protocol in sv['protocol']:

        print(protocol)

        csv_report = pd.DataFrame({
            'Protocolo': [int(protocol)],
            'CTE Loglife': '-',
            'Data Emissão CTE': '-'
        })

        csv_report = csv_report.astype(str)
        csv_report = csv_report.replace(to_replace="\.0+$", value="", regex=True)

        csv_report.to_csv(clear_cte_file, index=False, encoding='utf-8')

        r.post_file('https://transportebiologico.com.br/api/uploads/cte-loglife', clear_cte_file)

        os.remove(clear_cte_file)

    confirmation_pop_up(root, "Numeração dos CTes simbólicos removidas do sistema.")


def cte_cancel_batch(start_date, final_date, root):
    """
    Cancel CTe documents in batch based on date range.
    Follows the same pattern as cte_list, cte_complimentary, etc.
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

    # Query API for cancelled services
    try:
        df = r.request_public(
            link=f'https://transportebiologico.com.br/api/public/service/cancelled-unsuccessful?initialDate={di_formatted}&finalDate={df_formatted}'
        )

        if "services" not in df.columns or df.empty:
            confirmation_pop_up(root, "⚠️ Estrutura de retorno da API inesperada. Nenhum CT-e extraído.")
            return

        lista_servicos = df.iloc[0]["services"]
        if not isinstance(lista_servicos, list):
            confirmation_pop_up(root, "⚠️ 'services' não contém uma lista. Nenhum CT-e extraído.")
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
                        "protocolo": protocolo
                    })

            # Check for Complementary CTe
            if "cte_complementary" in item and item["cte_complementary"]:
                numero_cte = str(item["cte_complementary"]).strip()
                if numero_cte not in ["-", "", "nan"] and numero_cte.isdigit():
                    ctes_complementary.append({
                        "numero": numero_cte,
                        "protocolo": protocolo
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
                        "data": [
                            {
                                "protocol": protocolo,
                                "cte_loglife": None,
                                "cte_loglife_emission_date": None
                            }
                        ]
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
                        "data": [
                            {
                                "protocol": protocolo,
                                "cte_complementary": None,
                                "cte_complementary_emission_date": None
                            }
                        ]
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
        confirmation_pop_up(root, f"❌ Erro ao consultar API: {e}")


def cancelar_avulso_cte(numero_cte, protocolo, root, cte_type="loglife"):
    """
    Cancel a single CTe document.
    Follows the same pattern as other emission functions.
    
    Args:
        numero_cte: CTe number to cancel
        protocolo: Protocol number to update in API
        root: Tkinter root window for popup messages
        cte_type: Type of CTe - "loglife" or "complementary" (default: "loglife")
    """
    if not numero_cte or not protocolo:
        confirmation_pop_up(root, "⚠️ CT-e e Protocolo são obrigatórios.")
        return

    try:
        # Execute UI automation
        bot_cte = bot.Bot()
        bot_cte.cancel_cte(numero_cte)
        
        # Determine API endpoint and payload based on cte_type
        if cte_type.lower() == "complementary":
            api_endpoint = "https://transportebiologico.com.br/api/uploads/cte-complementary/json"
            payload = {
                "data": [
                    {
                        "protocol": str(protocolo),
                        "cte_complementary": None,
                        "cte_complementary_emission_date": None
                    }
                ]
            }
            cte_type_label = "Complementar"
        else:  # Default to loglife
            api_endpoint = "https://transportebiologico.com.br/api/uploads/cte-loglife/json"
            payload = {
                "data": [
                    {
                        "protocol": str(protocolo),
                        "cte_loglife": None,
                        "cte_loglife_emission_date": None
                    }
                ]
            }
            cte_type_label = "Loglife"
        
        # Update API after successful cancellation
        r.request_private(
            link=api_endpoint,
            request_type="post",
            payload=payload,
            json=True
        )
        print(f"📤 Protocolo {protocolo} ({cte_type_label} avulso) enviado com sucesso.")
        
        confirmation_pop_up(root, f"✅ CT-e {cte_type_label} {numero_cte} cancelado com sucesso!")
    except Exception as e:
        print(f"❌ Erro fatal no cancelamento avulso do CT-e {numero_cte}: {e}")
        confirmation_pop_up(root, f"❌ Erro no cancelamento: {str(e)}")


def validar_e_cancelar_ctes(relatorio_bsoft_path, root):
    """
    Validates CTes from B-Soft report against API and cancels missing ones.
    
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
            confirmation_pop_up(root, f"❌ Coluna 'Nº CT-e' ou 'Número' não encontrada no relatório.\nColunas disponíveis: {', '.join(df_bsoft.columns)}")
            return
        
        print(f"📊 Usando coluna: {cte_column}")
        
        # Filter out rows where 'Total' column equals 5
        if 'Total' in df_bsoft.columns:
            df_bsoft = df_bsoft[df_bsoft['Total'] != 5]
            print(f"📊 Filtradas linhas com Total = 5")
        
        # Get CTes from B-Soft report (remove NaN, parse as int, convert to string)
        ctes_bsoft = set()
        for cte in df_bsoft[cte_column].dropna():
            try:
                # Try to convert to int first to remove decimals/formatting, then to string
                cte_int = int(float(str(cte).strip()))
                ctes_bsoft.add(str(cte_int))
            except (ValueError, TypeError):
                # If conversion fails, try as string
                cte_str = str(cte).strip()
                if cte_str and cte_str.lower() not in ['nan', '', '-']:
                    ctes_bsoft.add(cte_str)

        print(f"✅ {len(ctes_bsoft)} CT-es encontrados no Relatório B-soft")
        
        # Set date range for API query (last 90 days to cover most CTes)
        end_date = dt.datetime.now()
        start_date = end_date - dt.timedelta(days=90)
        
        di_formatted = start_date.strftime("%Y-%m-%d")
        df_formatted = end_date.strftime("%Y-%m-%d")
        
        # Request CTes from API with mandatory date parameters
        print(f"🔍 Consultando CT-es na API (período: {di_formatted} a {df_formatted})...")
        df_api = r.request_public(
            f'https://transportebiologico.com.br/api/public/service/with-cte?initialDate={di_formatted}&finalDate={df_formatted}'
        )

        print(df_api)
        
        if "services" not in df_api.columns or df_api.empty:
            confirmation_pop_up(root, "⚠️ Estrutura de retorno da API inesperada. Nenhum CT-e extraído.")
            return
        
        lista_servicos = df_api.iloc[0]["services"]
        if not isinstance(lista_servicos, list):
            confirmation_pop_up(root, "⚠️ 'services' não contém uma lista. Nenhum CT-e extraído.")
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


def comparar_gnre_target(relatorio_bsoft_path, relatorio_target_path, root):
    """
    Compares GNRE values between B-Soft and Target reports.
    
    Args:
        relatorio_bsoft_path: Path to B-Soft Excel report (.xlsx/.xls)
        relatorio_target_path: Path to Target Excel report (.xlsx/.xls)
        root: Tkinter root window for popup messages
    """
    try:
        print("\n📋 Iniciando comparação de GNRE entre B-Soft e Target")
        
        # Validate file paths
        if not relatorio_bsoft_path or relatorio_bsoft_path == 'Relatório B-soft (.xlsx/.xls)':
            confirmation_pop_up(root, "❌ Por favor, selecione o arquivo Relatório B-soft.")
            return
        
        if not relatorio_target_path or relatorio_target_path == 'Relatório Target (.xlsx/.xls)':
            confirmation_pop_up(root, "❌ Por favor, selecione o arquivo Relatório Target.")
            return
        
        # Read B-Soft report
        print(f"📂 Lendo B-Soft: {relatorio_bsoft_path}")
        try:
            df_bsoft = pd.read_excel(relatorio_bsoft_path, engine='openpyxl')
        except:
            df_bsoft = pd.read_excel(relatorio_bsoft_path, engine='xlrd')
        
        # Read Target report
        print(f"📂 Lendo Target: {relatorio_target_path}")
        try:
            df_target = pd.read_excel(relatorio_target_path, engine='openpyxl')
        except:
            df_target = pd.read_excel(relatorio_target_path, engine='xlrd')
        
        # Verify 'Nº CT-e' or 'Número' column exists in B-Soft
        cte_column = None
        if 'Nº CT-e' in df_bsoft.columns:
            cte_column = 'Nº CT-e'
        elif 'Número' in df_bsoft.columns:
            cte_column = 'Número'
        else:
            confirmation_pop_up(root, f"❌ Coluna 'Nº CT-e' ou 'Número' não encontrada no B-Soft.\nColunas disponíveis: {', '.join(df_bsoft.columns)}")
            return
        
        print(f"📊 Usando coluna: {cte_column}")
        
        # Verify other required columns in B-Soft
        required_bsoft_cols = ['Data', 'Remetente - UF', 'Destinatário - UF', 'Total']
        missing_bsoft = [col for col in required_bsoft_cols if col not in df_bsoft.columns]
        if missing_bsoft:
            confirmation_pop_up(root, f"❌ Colunas faltando no B-Soft: {', '.join(missing_bsoft)}")
            return
        
        # Verify required columns in Target
        required_target_cols = ['Chave Documento', 'Valor do GNRE', 'UF Favorecida']
        missing_target = [col for col in required_target_cols if col not in df_target.columns]
        if missing_target:
            confirmation_pop_up(root, f"❌ Colunas faltando no Target: {', '.join(missing_target)}")
            return
        
        # Extract CTe numbers from Target (positions 28-34 of 'Chave Documento')
        print("🔍 Extraindo CT-es do Target...")
        df_target['CTe_Numero'] = df_target['Chave Documento'].astype(str).str[25:34].str.strip()
        
        # Parse CTe numbers as integers and back to string for consistent comparison
        df_target['CTe_Numero'] = df_target['CTe_Numero'].apply(
            lambda x: str(int(x)) if x.isdigit() else x
        )
        
        # Parse B-Soft CTe numbers
        print("🔍 Processando CT-es do B-Soft...")
        df_bsoft['CTe_Parsed'] = df_bsoft[cte_column].apply(
            lambda x: str(int(float(str(x).strip()))) if pd.notna(x) else None
        )
        
        # Remove rows with invalid CTe numbers
        df_bsoft = df_bsoft[df_bsoft['CTe_Parsed'].notna()]
        df_target = df_target[df_target['CTe_Numero'].notna()]
        
        print(f"✅ {len(df_bsoft)} CT-es no B-Soft")
        print(f"✅ {len(df_target)} CT-es no Target")
        
        # Merge dataframes on CTe number
        df_merged = pd.merge(
            df_bsoft,
            df_target[['CTe_Numero', 'Valor do GNRE', 'UF Favorecida', 'Linha Digitável']],
            left_on='CTe_Parsed',
            right_on='CTe_Numero',
            how='inner'
        )
        
        print(f"✅ {len(df_merged)} CT-es encontrados em ambos os relatórios")
        
        if df_merged.empty:
            confirmation_pop_up(root, "⚠️ Nenhum CT-e correspondente encontrado entre os relatórios.")
            return
        
        # Calculate B-Soft expected GNRE value
        print("🧮 Calculando valores esperados de GNRE...")
        
        results = []
        
        for idx, row in df_merged.iterrows():
            try:
                uf_rem = row['Remetente - UF']
                uf_dest = row['Destinatário - UF']
                barcode = row['Linha Digitável']
                
                # Convert Brazilian format (comma decimal) to float
                total_value = str(row['Total']).replace(',', '.')
                total_value = float(total_value)
                
                target_gnre = str(row['Valor do GNRE']).replace(',', '.')
                target_gnre = float(target_gnre)
                
                uf_favorecida = row['UF Favorecida']
                cte_num = row['CTe_Parsed']
                
                # Get aliquot from aliquota_base
                try:
                    aliq = aliquota_base.loc[aliquota_base['UF'] == uf_rem, uf_dest].values[0]
                    aliq = float(aliq) * 0.8
                except (IndexError, KeyError, ValueError):
                    print(f"⚠️ Alíquota não encontrada para {uf_rem} -> {uf_dest} (CT-e {cte_num})")
                    aliq = 0
                
                # Calculate expected GNRE: (aliquot * total) / 100
                expected_gnre = (aliq * total_value) / 100
                
                # Calculate difference
                difference = expected_gnre - target_gnre
                
                results.append({
                    'CT-e': cte_num,
                    'UF Origem': uf_rem,
                    'UF Destino': uf_dest,
                    'UF Favorecida': uf_favorecida,
                    'Total B-Soft': total_value,
                    'Alíquota (%)': aliq,
                    'GNRE Calculado': round(expected_gnre, 2),
                    'GNRE Target': round(target_gnre, 2),
                    'Diferença': round(difference, 2),
                    'Linha Digitável': barcode
                })
                
            except Exception as e:
                print(f"⚠️ Erro ao processar CT-e {row.get('CTe_Parsed', 'N/A')}: {str(e)}")
                continue
        
        # Create results dataframe
        df_results = pd.DataFrame(results)
        
        if df_results.empty:
            confirmation_pop_up(root, "⚠️ Nenhum resultado foi gerado.")
            return
        
        # Create summary by UF Favorecida
        print("📊 Gerando resumo por UF...")
        summary = df_results.groupby('UF Favorecida').agg({
            'CT-e': 'count',
            'GNRE Calculado': 'sum',
            'GNRE Target': 'sum',
            'Diferença': 'sum'
        }).reset_index()
        
        summary.columns = ['UF Favorecida', 'Qtd CT-es', 'Total GNRE Calculado', 'Total GNRE Target', 'Total Diferença']
        summary['Total GNRE Calculado'] = summary['Total GNRE Calculado'].round(2)
        summary['Total GNRE Target'] = summary['Total GNRE Target'].round(2)
        summary['Total Diferença'] = summary['Total Diferença'].round(2)
        
        # Export reports
        now = dt.datetime.now()
        timestamp = now.strftime("%d-%m-%Y-%H-%M")
        
        # Get output path from 'CTe normal' folder (folderpath)
        try:
            with open('folderpath.txt') as f:
                output_path = f.read().strip()
        except FileNotFoundError:
            # Fallback to same directory as B-Soft report
            output_path = relatorio_bsoft_path.rsplit('\\', 1)[0] if '\\' in relatorio_bsoft_path else '.'
        
        output_path = output_path.replace('/', '\\')
        output_file = f'{output_path}\\comparar-GNRE-{timestamp}.xlsx'
        
        # Save both sheets in a single Excel file
        with pd.ExcelWriter(output_file, engine='openpyxl') as writer:
            summary.to_excel(writer, sheet_name='Resumo', index=False)
            df_results.to_excel(writer, sheet_name='Detalhado', index=False)
        
        print(f"\n✅ Relatório gerado: {output_file}")
        
        # Create summary message
        total_ctes = len(df_results)
        total_diff = df_results['Diferença'].sum()
        
        result_message = f"Comparação GNRE concluída!\n\n"
        result_message += f"CT-es processados: {total_ctes}\n"
        result_message += f"Diferença total: R$ {total_diff:,.2f}\n\n"
        result_message += f"Relatório salvo em:\n{output_file}"
        
        confirmation_pop_up(root, result_message)
        
    except Exception as e:
        print(f"❌ Erro na comparação de GNRE: {e}")
        import traceback
        traceback.print_exc()
        confirmation_pop_up(root, f"❌ Erro na comparação: {str(e)}")


def processar_gnre_target(relatorio_target_path, root):
    """
    Process GNRE entries from Target report and enter them into the system.
    
    Args:
        relatorio_target_path: Path to the Target Excel report
        root: Tkinter root for popup messages
    """
    try:
        print(f"\n📂 Carregando relatório Target: {relatorio_target_path}")
        
        # Read the Target report from "Detalhes" tab
        df = pd.read_excel(relatorio_target_path, sheet_name='Detalhado')
        
        print(f"✅ Relatório carregado. Total de linhas: {len(df)}")
        
        # Filter out rows without GNRE values
        df_gnre = df[df['GNRE Target'].notna() & (df['GNRE Target'] > 0)].copy()
        
        if df_gnre.empty:
            confirmation_pop_up(root, "⚠️ Nenhuma GNRE encontrada para processar.")
            return
        
        print(f"📋 {len(df_gnre)} GNREs para processar")
        
        # Initialize bot
        bot_cte = bot.Bot()
        
        processados = 0
        erros = 0
        
        # Process each line
        for index, row in df_gnre.iterrows():
            try:
                # Extract CTe number
                cte_number = str(row['CT-e']).strip()
                
                # Extract slip value
                slip_value = row['GNRE Target']
                
                # Extract emission date
                emission_date = row['Data']
                
                # Get supplier from state full name
                uf_favorecida = str(row['UF Favorecida']).strip().upper()
                supplier = state_full_name.get(uf_favorecida, '')
                
                if not supplier:
                    print(f"⚠️ UF Favorecida não encontrada no dicionário: {uf_favorecida}")
                    erros += 1
                    continue
                
                # Get cost center from API
                try:
                    print(f"🔍 Consultando cost_center para CT-e {cte_number}")
                    service_data = r.request_public(
                        f'https://transportebiologico.com.br/api/public/service/by-cte?cteNumber={cte_number}'
                    )

                    print(service_data)
                    
                    if service_data.empty:
                        print(f"⚠️ Nenhum serviço encontrado para CT-e {cte_number}")
                        erros += 1
                        continue
                    
                    # Extract cost_center from API response
                    cost_center = service_data.cost_center[0]
                    
                    if not cost_center:
                        print(f"⚠️ Cost center não encontrado para CT-e {cte_number}")
                        erros += 1
                        continue
                        
                except Exception as api_error:
                    print(f"❌ Erro ao consultar API para CT-e {cte_number}: {api_error}")
                    erros += 1
                    continue
                
                # Extract barcode number
                barcode_number = str(row['Linha Digitável']).strip()
                
                # Call Bot.icms_slip_entry
                print(f"📝 Processando CT-e {cte_number} - Valor: R$ {slip_value:.2f}")
                bot_cte.icms_slip_entry(
                    cte_number=cte_number,
                    slip_value=slip_value,
                    supplier=supplier,
                    cost_center=cost_center,
                    barcode_number=barcode_number,
                    emission_date=emission_date
                )
                
                processados += 1
                print(f"✅ CT-e {cte_number} processado com sucesso ({processados}/{len(df_gnre)})")
                
            except Exception as e:
                print(f"❌ Erro ao processar linha {index + 1}: {e}")
                erros += 1
                continue
        
        # Show results
        print(f"\n📊 Processamento concluído.")
        print(f"✅ GNREs processadas: {processados}")
        print(f"⚠️ Erros: {erros}")
        
        result_message = f"Processamento concluído!\n\nGNREs processadas: {processados}\nErros: {erros}"
        confirmation_pop_up(root, result_message)
        
    except Exception as e:
        print(f"❌ Erro ao processar GNREs: {e}")
        traceback.print_exc()
        confirmation_pop_up(root, f"❌ Erro ao processar GNREs: {str(e)}")


if __name__ == "__main__":
    pass

    # Exemplo de como chamar o cancelamento avulso
    # cancelar_avulso_cte(numero_cte="12345", protocolo="987654321")
