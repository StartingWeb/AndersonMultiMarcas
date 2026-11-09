# Migrations produtivas — executadas em 07/11/2026

A/B/C foram autorizadas explicitamente pelo proprietário e **aplicadas em produção**, após novo backup restaurável e ensaio dos scripts exatos. Não há nova solicitação pendente de nome ou nulabilidade. Nenhum deploy/merge foi realizado. [Recibo consolidado e journal](./evidence/PRODUCTION_SQL_2026-11-07.json).

## Destino e autorização

Container SQL `34bf488b2a59fa8084c0d22ad58fd541be5cf9f08e8d6975c1312c7b92e103b2`, instância `491cbc5de981`, banco AndersonMultiMarcas, GUID `9D811E45-14B7-48E2-A376-74B3D6A89ADF`, collation SQL_Latin1_General_CP1_CI_AS: todos reconferidos antes da execução. Sem triggers ativos inesperados, requests bloqueadores ou alterações de contrato. Referência da autorização desta janela: `owner-2026-11-07-explicit-sql-abc`.

[Manifesto preparado](../deploy/production/target.prepared.json) permanece um modelo desabilitado, não uma autorização reutilizável. A cópia de execução privada recebeu os valores expressamente aprovados; o journal/recibo registra a autorização efetivamente usada. Não habilitar esse modelo para outras janelas por inferência.

## Backup e ensaio prévios

Backup novo `20261107T150452Z-c36eef7e`, COPY_ONLY/CHECKSUM/VERIFYONLY, 30.334.976 bytes, SHA `15b714753be43bba0336877b863d86d46c93d8fd90e897f61545ee52df725370`, snapshot `28278403757ac1386d8a5c2e15821b200d04d423eaf2eb8a5c0da98a4b24df4b`. Restic autenticado, restore --verify e check/read-data aprovados; cópias anteriores preservadas. Restauração SQL real `46fe30bdb760` concluiu 12:07:28 Brasília com DBCC, constraints e igualdade de schema/contagens. Aviso e confirmação do restore aceitos pelo SMTP, sem prova de entrega na caixa de entrada.

Ensaio `23b9b122277e` dos SQLs produtivos exatos: A/B/C com replay inalterado; 78 checkpoints dos binários antigo/original, antigo/ampliado, novo/ampliado e retorno ao antigo; nove negativas de privilégios, CRUD/login sintético/mídia local e máximos de escrita preservados. DBCC e limpeza aprovados. Rede interna e volumes/instância próprios, sem credenciais produtivas nem requisições R2. [Executor do ensaio](../scripts/compatibility/rehearse_production.py), derivado do ensaio anterior para usar os guards produtivos na cópia, incluindo remapeamento do usuário restaurado somente nela.

```sh
python3 -I -B rehearse_production.py --schema-dir DIRETORIO_PRIVADO_DOS_TRES_SQLS \
  --runtime-grants DIRETORIO_PRIVADO/runtime_permissions.production.sql
```

Executar apenas na VPS autorizada com backup verificado e margem de memória; não é um runner de migrations produtivas. Recusa backup incompatível e confere ownership de todo recurso temporário antes de limpar. Exige backup que contenha o usuário runtime já instalado e estado inicial aprovado das marcas; não usar para um backup pós-A/B/C como se estivesse no estado original.

## Execuções produtivas

| Migration | Horário Brasília | Resultado |
| --- | --- | --- |
| [A — 010_schema.sql](../deploy/production/migrations/010_schema.sql) | 12:11:07 | 12 ampliações; somente Marca.LogoUrl NULL, demais nulabilidades preservadas |
| [B — 020_marca.sql](../deploy/production/migrations/020_marca.sql) | 12:11:32 | Somente Nome do ID 4: Jaguar → Jaguar (cadastro adicional) |
| [C — 030_index.sql](../deploy/production/migrations/030_index.sql) | 12:13:33 | IX_Marca_Nome único, nonclustered, Nome ASC, sem filtro |

Os hashes SHA-256 dos arquivos e fingerprints anterior/posterior estão no recibo e nas três entradas de AndersonProductionMigrationJournal. Transações, XACT_ABORT, applock exclusivo, lock timeout, autorização por escopo/instância/GUID e recusa de estado inesperado foram mantidos. Não se removeram guards de ensaio; as versões produtivas não foram alteradas para execução. Não houve migration automática no startup nem escrita no histórico EF.

## Resultado validado

| Informação | Resultado |
| --- | --- |
| ID 1 | Jaguar, ativa, cadastro 2026-03-17T21:25:53.2550543, 1 veículo |
| ID 4 | Jaguar (cadastro adicional), ativa, cadastro 2026-03-24T23:08:09.7600000, 0 veículos |
| Nome | nvarchar(100), SQL_Latin1_General_CP1_CI_AS, nenhum grupo duplicado |
| LogoUrl | nvarchar(400) NULL; valores existentes preservados |
| Exclusões / relações alteradas | 0 / 0; nenhum MarcaId alterado |
| Contagens | 49 marcas, 401 veículos, 4.429 mídias, demais tabelas preservadas; apenas três entradas no journal novo |
| FK / constraints / histórico | Inalterados; 23 migrations EF preexistentes preservadas |
| DBCC produtivo | Aprovado |
| Runtime | Login real confirmado, CRUD necessário, sem sysadmin/db_owner/DDL/admin; sem rotação/recriação |
| Preflight instalado | ready, backup verified, schema compatible |
| Legado | Healthy; páginas, catálogo, login/imagem e três domínios HTTPS 200; logs da janela sem erro SQL/falha |

Os fingerprints de dados diferiram em Veiculo durante a janela. A migration B verificou a igualdade dos veículos dentro da transação; investigação adicional restaurou o backup novamente (`e609ecd17121`) e comprovou que apenas QuantidadeVisualizacoes aumentou quatro vezes em um registro. Todos os demais campos dos 401 veículos permaneceram iguais. As outras 19 tabelas ficaram iguais, normalizando exclusivamente o nome aprovado da marca. Nenhuma gravação legítima foi revertida. Login autenticado/CRUD foram exercidos somente na cópia; contas de clientes/administradores reais não foram redefinidas.

## Rollback e pendências

Retorno normal do **binário**, mantendo schema ampliado, LogoUrl NULL, rótulo aprovado, índice único, IDs e dados. Não reduzir colunas, desfazer nome/índice ou restaurar banco automaticamente. Backup pré-migration fica na VPS em diretório privado identificado pelo run ID; recuperação produtiva pode descartar gravações posteriores e requer decisão própria. Legado e identidade antiga preservados.

Primeiro revisar/integrar PR #3; atualizar PR #2 com main resultante e validar/revisar o conjunto; integrar PR #2 somente com política satisfeita. Só StartingWeb tem acesso elegível: falta mantenedor independente autorizado e CODEOWNERS aplicável, sem bypass. R2 real isolado, destino externo de backup ou aceite explícito de risco, paridade e CI do SHA final ainda bloqueiam publicação. Gates continuam false e habilitação local ausente. Novo backup restaurável imediatamente antes do corte futuro.

## Pendência de mídia verificada após o SQL

Leitura R2 da página pública: três imagens de veículos 200 público/S3 autenticado; três fotos de vendedores 404 nos dois acessos, sem arquivo correspondente no container/checkout legado. Nenhuma URL foi modificada por A/B/C e nenhum PUT/DELETE foi feito. Localizar originais/fonte de recuperação autorizada antes de declarar a integridade dessa mídia; não substituir dados ou imagens por inferência. [Amostra e inventário](./evidence/PRODUCTION_SQL_2026-11-07.json). Homologação real de escrita continua dependendo de destino isolado autorizado.
