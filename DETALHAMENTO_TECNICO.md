# Detalhamento Tecnico

## Stack adotada

- Frontend: Angular 20 com componentes standalone, Reactive Forms e RxJS.
- Backend: C# com ASP.NET Core 10 em Minimal APIs.
- Persistencia: Entity Framework Core 10 com SQLite.

## Ciclos de vida do Angular utilizados

- `OnInit`: usado no componente principal para carregar os dados iniciais do dashboard assim que a aplicacao e inicializada.

## Uso de RxJS

Sim.

RxJS foi utilizado principalmente em:

- `forkJoin`: carregar produtos, notas e estado da simulacao de falha em paralelo.
- `switchMap`: encadear operacoes como cadastrar produto e depois recarregar o dashboard.
- `catchError`: capturar erros HTTP e apresentar feedback amigavel.
- `finalize`: controlar estados visuais de carregamento e processamento.
- Observables do `HttpClient`: base para integracao do frontend com os microsservicos.

## Outras bibliotecas utilizadas

- `@angular/forms`: formularios reativos para cadastro de produtos e notas.
- `@angular/common/http`: comunicacao HTTP com os microsservicos.
- `Microsoft.EntityFrameworkCore.Sqlite`: persistencia com SQLite.

## Bibliotecas visuais utilizadas

- Nenhuma biblioteca visual externa foi utilizada.
- A interface foi desenvolvida com HTML + SCSS customizados.

## Gerenciamento de dependencias no Golang

- Nao se aplica, pois a implementacao backend foi feita em C#.

## Frameworks utilizados no C#

- `ASP.NET Core 10`
- `Entity Framework Core 10`

## Tratamento de erros e excecoes no backend

- Middleware global simples em cada microsservico para capturar excecoes inesperadas e retornar `ProblemDetails` com HTTP 500.
- Validacoes de negocio com retornos controlados para:
  - codigo de produto duplicado
  - saldo insuficiente
  - produto inexistente
  - tentativa de impressao de nota ja fechada
  - indisponibilidade simulada do servico de estoque
- O frontend interpreta esses erros e exibe mensagens amigaveis ao usuario.

## Uso de LINQ no C#

Sim.

LINQ foi utilizado para:

- ordenar produtos e notas fiscais
- agrupar itens repetidos de uma nota
- calcular somatorios de quantidade
- consultar o proximo numero sequencial de nota
- projetar entidades em DTOs de resposta

## Persistencia e banco de dados

- Cada microsservico possui seu proprio banco SQLite fisico.
- O banco e criado automaticamente com `Database.EnsureCreated()` ao subir cada servico.
- Isso mantem o isolamento entre contexto de estoque e contexto de faturamento.

## Comunicacao entre microsservicos

- O `BillingService` chama o `StockService` via HTTP no momento da impressao.
- Se a baixa de estoque for concluida, a nota fiscal e fechada.
- Se o estoque falhar ou retornar erro, a nota permanece aberta e o usuario recebe o motivo.

## Tratamento de falha recuperavel

- O `StockService` expoe um endpoint para ativar ou desativar um modo de falha simulada.
- Quando ativo, a baixa de estoque responde com HTTP `503 Service Unavailable`.
- O `BillingService` tenta novamente em falhas transientes e, se nao conseguir, devolve o erro ao frontend.
- Ao desativar a falha simulada, o usuario pode repetir a impressao normalmente.

## Idempotencia

- A baixa de estoque utiliza uma `operationKey` unica por nota fiscal.
- Se a mesma operacao for reenviada, o estoque nao e debitado duas vezes.
- Isso evita efeitos colaterais indesejados em cenarios de repeticao de requisicao.
