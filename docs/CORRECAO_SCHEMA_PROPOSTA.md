# Proposta de compatibilidade do schema — revisão separada

Preparada em 05/11/2026 na branch `infra/schema-compatibility-review`. **Nenhuma alteração de schema ou dados foi executada em produção. Não aplicar estes scripts no banco produtivo.** A preparação de infraestrutura está em branch/PR independente.

## Problema confirmado

A restauração do banco AndersonMultiMarcas passou por VERIFYONLY/checksum, DBCC CHECKDB, constraints e comparação exata com a origem. A aplicação .NET 10 funcionou com leitura sobre a cópia original, sem migrations. Porém, o schema real permite valores menores em algumas colunas que os aceitos pelo modelo e tem `Marca.LogoUrl` obrigatório, embora a fonte/modelo histórico já o trate como opcional.

O SQL idempotente atual do CI, aplicado somente a uma segunda cópia, inseriu duas entradas de histórico ausentes sem alterar essa estrutura: as migrations de reparo antigas já constavam como aplicadas. Reaplicar esse SQL em produção não é uma solução para o drift observado.

Também foi constatado que `IX_Marca_Nome` não é único, em desacordo com o modelo. A consulta agregada detectou um grupo de nomes duplicados. Nenhum nome ou registro foi mostrado e nenhuma marca foi mesclada/removida.

## Ampliação proposta

[EXPAND_STORAGE_PROPOSAL.sql](./schema/EXPAND_STORAGE_PROPOSAL.sql) amplia somente 12 colunas para suportar os limites do modelo:

| Campo | Capacidade proposta em caracteres |
| --- | --- |
| Loja.Email | 180 |
| Marca.LogoUrl | 400, permite NULL conforme modelo |
| Veiculo.Cor | 60 |
| Veiculo.Descricao | 4000 |
| Veiculo.Modelo / Versao | 150 |
| Veiculo.ObservacoesInternas | 2000 |
| Veiculo.Titulo | 180 |
| Veiculo.UrlVideo | 400 |
| Vendedor.Cargo | 120 |
| Vendedor.Email | 180 |
| Vendedor.FotoUrl | 400 |

O script confere tabelas, tipos e colunas antes de operar, usa transação/XACT_ABORT e não reduz larguras existentes. Preserva nulabilidade corrente, exceto o relaxamento de LogoUrl. Não altera defaults, GUIDs, chaves, dados, colunas/tabelas legadas ou o histórico EF. Colunas já mais largas que o modelo, como CNPJ/CEP/placa, permanecem assim. Campos produtivos anuláveis que o modelo declara obrigatórios também não são endurecidos automaticamente; o agregado avaliado não encontrou NULLs nos campos obrigatórios de veículos/lojas.

Ampliar colunas não significa zero risco operacional: existe lock de schema e deve haver janela, revisão de índices e aceitação de rollback. Os scripts desta proposta aceitam somente `AndersonSchemaTrial`, a segunda cópia na instância isolada. Uma versão produtiva deve ser incorporada por processo de migration/SQL aprovado, com autorização específica; não remover o guard para executar em produção por conta própria.

## Unicidade: decisão funcional pendente

[MARCA_NOME_UNIQUE_PROPOSAL.sql](./schema/MARCA_NOME_UNIQUE_PROPOSAL.sql) é uma proposta separada. Ela detecta duplicidade com locks transacionais e recusa com erro 51021 **antes de remover o índice existente**. Não deduplica nem escolhe uma marca vencedora.

É necessário decidir se a regra de unicidade continua desejada e, nesse caso, aprovar um plano de dados/relacionamentos para os duplicados. Não alterar o modelo para aceitar duplicidades nem mudar dados produtivos silenciosamente. Esta PR não declara a compatibilidade completa de gravação aprovada.

## Evidência de ensaio

Os dois scripts foram enviados somente à instância SQL isolada usada para ensaio, com volumes/rede novos e sem portas públicas. A cópia original foi preservada para leitura e comparação antes das propostas.

- Ampliação: aplicada e reaplicada com sucesso na segunda cópia; schema idempotente, contagens iguais antes/depois, DBCC CHECKDB aprovado.
- Aplicação .NET 10 após ampliação: home, catálogo, empresa e login 200; Admin 302, zero erros SQL e nenhuma credencial produtiva.
- Índice único sobre a cópia real: recusado com 51021 por duplicidade, mantendo schema e contagens exatamente iguais.
- Em dados exclusivamente sintéticos, a criação do índice e reaplicação passaram, os dois registros sintéticos foram preservados e o guard recusou banco diferente com 51090. O container foi removido e sua ausência foi conferida. Recibo: `history/marca-unique-synthetic-test.json` na raiz unificada.
- O banco produtivo foi consultado somente para metadados/agregados. Nenhum dado real saiu da VPS. Recursos de teste foram identificados por labels exclusivas e removidos ao término.

Recibo do ensaio real: `/opt/startingweb/apps/anderson-multimarcas-unified/snapshots/amm-restore-a2483165e38b/restore-report.json`. SHA-256 da ampliação: `1fa8984cc3e5cbda2de0c4831f62c7675fb7a8ee5a62be236e2453a6bff53c6d`. A proposta não habilita deploy, aplica migrations no startup, altera proxy ou interrompe o legado.

## Reavaliação em 06/11/2026

O diagnóstico foi repetido por auditoria somente leitura e está detalhado em [COMPATIBILIDADE_BINARIOS_2026-11-06.md](./COMPATIBILIDADE_BINARIOS_2026-11-06.md), com evidências sanitizadas. O ensaio completo antigo → ampliação/proposta opcional → novo → rollback passou com 78 checkpoints, nove negativas SQL e DBCC, incluindo escrita máxima pela nova aplicação seguida de edição pelo binário antigo sem truncamento. Todos os recursos descartáveis foram removidos.

A PR agora prepara uma alternativa **opcional** para diferenciar o ID técnico 4 sem apagar registros/mover referências, com ledger privado e undo restrito à cópia. A escolha do rótulo continua sendo decisão funcional. A criação do índice também recusa definição existente inesperada antes de removê-lo. São propostas, não autorização de DML/DDL produtivo; o schema real e os duplicados permanecem intactos e bloqueiam ativação.

Usuário/grants runtime e mecanismo de credencial pertencem à PR #2. O harness recebe aquele SQL como insumo revisado; nenhuma senha foi incorporada a esta PR. A matriz usa Local storage e não aprova R2 real. Os dois PRs precisam de revisão independente; não fazer merge automaticamente.
