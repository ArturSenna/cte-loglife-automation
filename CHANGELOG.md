# Changelog

O que muda em cada versão do CTe LogLife (C#). A seção de uma versão vira o texto do release no GitHub,
que é o que o programa mostra em "Novidades" ao oferecer a atualização: escreva para quem opera o robô,
em texto simples (a janela não formata Markdown).

Para uma versão nova: suba `<Version>` em `Directory.Build.props` e acrescente aqui, no topo, uma seção
`## [x.y.z] - AAAA-MM-DD`. Sem seção para a versão, o release usa a lista automática do GitHub.
Versões anteriores à 2.0.3: veja os releases no GitHub.

## [2.0.4] - 2026-11-26

- Corrigido: ao lançar guias de ICMS, o robô parava em "Pré-cadastrada" e em "Observação" avisando de tela desconhecida, e só continuava quando o operador clicava fora do campo. Agora ele reconhece esses dois campos também quando o cursor do teclado já está neles.

## [2.0.3] - 2026-11-23

- Corrigido: na busca do remetente, do destinatário e do tomador, quando o CNPJ tem mais de um cadastro no Bsoft (a busca mostra "Qtde.: 2" ou mais), o robô parava dizendo que não localizou a empresa. Agora ele clica no primeiro resultado da lista e confirma.
- Com um resultado só, o robô também clica na linha antes de confirmar.
- Se a lista da busca já tiver linhas antes de o CNPJ ser digitado, o robô continua aceitando só resultado único, como antes, para não escolher uma empresa que não veio da busca.
- As imagens das telas do Bsoft que o robô procura (pasta Templates) ganharam nomes em português, agrupados por tela, como busca_cliente_confirmar, transmissao_numero_cte e guia_icms_codigo_barras. As telas que o operador já ensinou ao robô continuam valendo.
