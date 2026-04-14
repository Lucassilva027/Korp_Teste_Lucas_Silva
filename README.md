# Sistema de Emissao de Notas Fiscais

Aplicacao full stack desenvolvida para o desafio tecnico da Korp com:

- `frontend`: Angular 20
- `StockService`: ASP.NET Core 10 + EF Core + SQLite
- `BillingService`: ASP.NET Core 10 + EF Core + SQLite

## Arquitetura

- Microsservico de Estoque: cadastro de produtos, consulta de saldos, baixa de estoque e simulacao de falha.
- Microsservico de Faturamento: criacao de notas fiscais com numeracao sequencial, status Aberta ou Fechada e impressao.
- Frontend Angular: interface unica para cadastrar produtos, abrir notas, imprimir e testar o comportamento de falha.

Cada microsservico possui seu proprio banco SQLite:

- `StockService/stock.db`
- `BillingService/billing.db`

## Funcionalidades implementadas

- Cadastro de produtos com codigo, descricao e saldo.
- Cadastro de notas fiscais com multiplos produtos e quantidades.
- Numeracao sequencial automatica para notas fiscais.
- Impressao de nota apenas quando o status esta `Aberta`.
- Atualizacao do status para `Fechada` apos impressao bem-sucedida.
- Baixa de estoque com base nas quantidades da nota.
- Simulacao de falha no servico de estoque com feedback visual ao usuario.
- Recuperacao do fluxo apos desativar a falha simulada.
- Idempotencia na baixa de estoque por chave de operacao da nota.