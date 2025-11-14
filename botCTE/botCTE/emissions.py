import datetime as dt
import os
import time
from datetime import datetime
import requests
import numpy as np
import bot
from functions import *
from botcity.core import DesktopBot

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

uf_base = pd.read_excel('Complementares.xlsx', sheet_name='Plan1')
aliquota_base = pd.read_excel('Alíquota.xlsx', sheet_name='Planilha1')


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
        default=0
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

        print(protocol)

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

        csv_report = pd.DataFrame({
            'Protocolo': [int(protocol)],
            'CTE Loglife': [cte_llm],
            'Data Emissão CTE': [report_date]
        })

        cte_file = f'{str(cte_llm).zfill(8)}.pdf'

        cte_csv = pd.DataFrame({
            'Protocolo': [int(protocol)],
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
        report.at[report.index[current_row], 'CTE Complementar'] = cte_llm_complimentary

        cte_file = f'{str(cte_llm_complimentary).zfill(8)}.pdf'

        cte_csv = pd.DataFrame({
            'Protocolo': [protocol],
            'Arquivo PDF': [cte_file],
        })

        report.to_excel(excel_file, index=False)

        csv_report = pd.DataFrame({
            'Protocolo': [int(protocol)],
            'CTE Complementar': [cte_llm_complimentary],
            'Data Emissão CTE': [report_date]
        })

        csv_report = csv_report.astype(str)
        csv_report = csv_report.replace(to_replace="\.0+$", value="", regex=True)

        csv_report.to_csv(csv_file, index=False, encoding='utf-8')

        cte_csv.to_csv(csv_associate, index=False, encoding='utf-8')

        first_response = r.post_file('https://transportebiologico.com.br/api/uploads/cte-complementary', csv_file)

        while True:
            try:
                second_response = r.post_file("https://transportebiologico.com.br/api/pdf",
                                              f'{cte_folder_path}\\{cte_file}',
                                              upload_type="CTE COMPLEMENTAR",
                                              file_format="application/pdf",
                                              file_type="pdf_files")
                break
            except FileNotFoundError:
                time.sleep(0.5)
                second_response = 0
                continue

        third_response = r.post_file('https://transportebiologico.com.br/api/pdf/associate',
                                     csv_associate,
                                     upload_type="CTE COMPLEMENTAR")

        csv_report.to_csv(csv_file, index=False, encoding='utf-8')

        print(first_response, second_response, third_response)
        print(first_response.text, second_response.text, third_response.text)

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
        default=0
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
        aliq_text = float(aliq) * 5 * 0.008
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

        cte_file = f'{str(cte_llm).zfill(8)}.pdf'

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
            'Protocolo': [int(protocol)],
            'CTE Loglife': [cte_llm],
            'Data Emissão CTE': [report_date]
        })

        cte_file = f'{str(cte_llm).zfill(8)}.pdf'

        cte_csv = pd.DataFrame({
            'Protocolo': [int(protocol)],
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

    sv.to_excel("CTe simbólico.xlsx", index=False)

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


def run_emissions():
    pass


#### ABA CANCELAMENTO ##############################################


# ============================
# Robô principal com passo a passo visual
# ============================

class Bot(DesktopBot):
    def action(self, data_inicial, data_final, execution=None):
        # 🔄 Consulta os CT-es via API
        self.ctes_recebidos = consultar_ctes_para_cancelar(data_inicial, data_final)

        # 🔁 Cria um dicionário com {cte_loglife: protocolo}
        mapa_ctes = {
            item["cte_loglife"].strip(): str(item["protocol"])
            for item in self.ctes_recebidos
            if item.get("cte_loglife") and item.get("protocol")
        }

        # 📋 Lista de CT-es a processar
        lista_ctes = list(mapa_ctes.keys())

        if not lista_ctes:
            print("⚠️ Nenhum CT-e encontrado para cancelamento.")
            input("\n⏹️ Pressione Enter para fechar...")
            return

        print(f"\n🔁 Iniciando cancelamento de {len(lista_ctes)} CT-es")

        ctes_canceladas = []
        ctes_com_erro = []

        # 🧭 Etapas iniciais da interface
        # if not self.find("1emissao_de_cte", matching=0.97, waiting_time=60000):
        #     self.not_found("1emissao_de_cte")
        #     return
        # self.click()
        # self.wait(1500

        # 🔁 Laço principal com os CT-es da API
        for numero_cte in lista_ctes:

            if not self.find("1emissao_de_cte", matching=0.97, waiting_time=30000):
                self.not_found("1emissao_de_cte")
                return
            self.click()
            self.wait(500)

            # 🧹 Limpeza inicial do campo (apenas uma vez)
            if self.find("3N_Ct-e", matching=0.97, waiting_time=5000):
                self.click_relative(x=20, y=20, clicks=2, interval_between_clicks=200)
                self.backspace()
                self.wait(500)

            if not str(numero_cte).isdigit():
                print(f"⚠️ CT-e ignorado (não numérico): {numero_cte}")

            print(f"\n🔄 Processando CT-e: {numero_cte}")

            try:
                # 3.2 Digitar número da CT-e
                self.type_keys(numero_cte)
                self.wait(500)

                # 4. Clicar em localizar
                if not self.find("4Localizar", matching=0.97, waiting_time=5000):
                    raise Exception("Elemento '4Localizar' não encontrado")
                self.click()
                self.wait(1500)

                # 5. Duplo clique em status
                if not self.find("5duploClickStatus", matching=0.97, waiting_time=5000):
                    raise Exception("Elemento '5duploClickStatus' não encontrado")
                self.double_click()
                self.wait(1000)

                # 5.1 Se não avançar, tenta a próxima tela
                if not self.find("5.1ct-e", matching=0.97, waiting_time=10000):
                    raise Exception("Elemento '5.1ct-e' não encontrado")
                self.click()
                self.wait(1500)

                # 5.2 Clica em cancelar cte
                if not self.find("5.2Cancelar_CTE", matching=0.97, waiting_time=10000):
                    raise Exception("Elemento '5.2Cancelar_CTE' não encontrado")
                self.click()
                self.wait(1000)

                # Escreve o motivo do cancelamento
                self.type_keys("transporte cancelado")
                self.wait(1000)

                # Clica em confirmar
                if not self.find("8confirmar", matching=0.97, waiting_time=10000):
                    raise Exception("Elemento '8confirmar' não encontrado")
                self.click()
                self.wait(6000)

                # 🔄 Envia o protocolo com campos nulos para a API
                protocolo = mapa_ctes.get(str(numero_cte).strip())
                if not protocolo:
                    print(f"⚠️ Protocolo ausente para CT-e {numero_cte}")
                    raise Exception(f"Protocolo não encontrado para o CT-e {numero_cte}")

                payload = {
                    "data": [
                        {
                            "protocol": str(protocolo),
                            "cte_loglife": None,
                            "cte_loglife_emission_date": None
                        }
                    ]
                }

                try:
                    r.request_private(
                        link="https://transportebiologico.com.br/api/uploads/cte-loglife/json",
                        request_type="post",
                        payload=payload,
                        json=True
                    )
                    print(f"📤 Protocolopayload {protocolo} enviado com sucesso com CT-e nulo.")
                except Exception as api_error:
                    print(f"❌ Erro ao enviar protocolo {protocolo}: {api_error}")

                self.wait(2000)

                # 🖱️ Clique na confirmação de sucesso
                if self.find("9-sucesso", matching=0.97, waiting_time=20000):
                    self.enter()
                    print("✅ Pop-up de sucesso confirmado.")
                else:
                    raise Exception("Pop-up '9-sucesso' não encontrado.")

                print(f"✅ CT-e {numero_cte} cancelado com sucesso")
                ctes_canceladas.append(numero_cte)

            except Exception as e:
                print(f"⚠️ Erro no processamento do CT-e {numero_cte}: {str(e)}")
                ctes_com_erro.append(numero_cte)
                # Adicionar uma lógica para resetar a tela se der erro, como voltar para a consulta

        print(f"\n📊 Cancelamento concluído.")
        print(f"✅ CT-es cancelados: {len(ctes_canceladas)} -> {ctes_canceladas}")
        print(f"⚠️ CT-es com erro: {len(ctes_com_erro)} -> {ctes_com_erro}")

    def not_found(self, label):
        print(f"❌ Elemento não encontrado: {label}")


# ============================
# Funções auxiliares
# ============================

def formatar_data(data):
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


def consultar_ctes_para_cancelar(data_inicial, data_final):
    di_formatted = formatar_data(data_inicial)
    df_formatted = formatar_data(data_final)

    if not di_formatted or not df_formatted:
        print("❌ Datas inválidas. Cancelando consulta.")
        return []

    print(f"📅 Consultando cancelamentos entre {di_formatted} e {df_formatted}")

    r = RequestDataFrame()
    try:
        df = r.request_public(
            link=f'https://transportebiologico.com.br/api/public/service/cancelled-unsuccessful?initialDate={di_formatted}&finalDate={df_formatted}'
        )

        if "services" in df.columns and not df.empty:
            lista_servicos = df.iloc[0]["services"]
            if not isinstance(lista_servicos, list):
                print("⚠️ 'services' não contém uma lista. Nenhum CT-e extraído.")
                return []

            print(f"\n📋 {len(lista_servicos)} serviços encontrados na API.")

            lista_filtrada = [
                item for item in lista_servicos
                if "cte_loglife" in item and item["cte_loglife"] and str(item["cte_loglife"]).strip() not in ["-", ""]
            ]
            print(f"✅ {len(lista_filtrada)} CT-es válidos para processamento.")
            return lista_filtrada

        print("⚠️ Estrutura de retorno da API inesperada. Nenhum CT-e extraído.")
        return []
    except Exception as e:
        print(f"❌ Erro ao consultar API: {e}")
        return []


# ============================
# Cancelamento avulso (botão: cancelar_avulso_cte)
# ============================

def cancelar_avulso_cte(numero_cte, protocolo):
    bot = Bot()
    print(f"\n🔄 Iniciando cancelamento avulso para o CT-e: {numero_cte}")

    try:
        # 🧭 Etapas iniciais da interface
        if not bot.find("1emissao_de_cte", matching=0.97, waiting_time=60000):
            raise Exception("Elemento '1emissao_de_cte' não encontrado")
        bot.click()
        bot.wait(1500)

        if not bot.find("2consultas", matching=0.97, waiting_time=30000):
            raise Exception("Elemento '2consultas' não encontrado")
        bot.click()
        bot.wait(500)

        # 🧹 Limpeza do campo
        if not bot.find("10ClicarBordaInferior", matching=0.97, waiting_time=5000):
            raise Exception("Campo para digitar CT-e não encontrado ('10ClicarBordaInferior')")
        x, y, w, h = bot.get_last_element()
        bot.click_at(x + int(w * 0.5), y + h - 1)
        bot.wait(500)
        bot.control_a()
        bot.wait(300)
        bot.delete()
        bot.wait(100)

        # 3.2 Digitar número da CT-e
        bot.type_keys(str(numero_cte))
        bot.wait(2000)

        # 4. Clicar em localizar e seguir o fluxo
        if not bot.find("4Localizar", matching=0.97, waiting_time=5000):
            raise Exception("Elemento '4Localizar' não encontrado")
        bot.click()
        bot.wait(1500)

        if not bot.find("5duploClickStatus", matching=0.97, waiting_time=5000):
            raise Exception("Elemento '5duploClickStatus' não encontrado")
        bot.double_click()
        bot.wait(1000)

        if not bot.find("5.1ct-e", matching=0.97, waiting_time=10000):
            raise Exception("Elemento '5.1ct-e' não encontrado")
        bot.click()
        bot.wait(1500)

        if not bot.find("5.2Cancelar_CTE", matching=0.97, waiting_time=10000):
            raise Exception("Elemento '5.2Cancelar_CTE' não encontrado")
        bot.click()
        bot.wait(1000)

        bot.type_keys("transporte cancelado")
        bot.wait(1000)

        if not bot.find("8confirmar", matching=0.97, waiting_time=10000):
            raise Exception("Elemento '8confirmar' não encontrado")
        bot.click()
        bot.wait(1000)

        # 🔄 Envia o protocolo com campos nulos para a API
        payload = {
            "data": [{"protocol": str(protocolo), "cte_loglife": None, "cte_loglife_emission_date": None}]
        }
        r = RequestDataFrame()
        r.request_private(
            link="https://transportebiologico.com.br/api/uploads/cte-loglife/json",
            request_type="post",
            payload=payload
        )
        print(f"📤 Protocolo {protocolo} (avulso) enviado com sucesso.")

        # 🖱️ Clique na confirmação de sucesso
        if bot.find("9-sucesso", matching=0.97, waiting_time=20000):
            x, y, w, h = bot.get_last_element()
            bot.click_at(x + w // 2, y + h - 1)
            bot.wait(1000)
        else:
            raise Exception("Pop-up '9-sucesso' não encontrado.")

        print(f"✅ CT-e avulso {numero_cte} cancelado com sucesso!")

    except Exception as e:
        print(f"❌ Erro fatal no cancelamento avulso do CT-e {numero_cte}: {e}")
        # Aqui você poderia adicionar um pop-up de erro se estivesse usando uma GUI


# ============================
# Execução principal (Exemplo)
# ============================

if __name__ == "__main__":
    # Exemplo de como chamar o robô principal
    bot_principal = Bot()
    # As datas podem vir de uma interface gráfica ou serem fixas
    data_inicio = "2025-10-29"
    data_fim = "2025-10-30"
    bot_principal.action(data_inicio, data_fim)

    # Exemplo de como chamar o cancelamento avulso
    # cancelar_avulso_cte(numero_cte="12345", protocolo="987654321")
