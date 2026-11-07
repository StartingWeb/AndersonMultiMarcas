# Validação do schema de produção

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Resultado e limite da aprovação

**Backup/restauração/integridade e leitura da aplicação foram aprovados. A compatibilidade completa de gravação ainda exige revisão do drift encontrado.** Nenhuma migration ou alteração de dados/schema foi executada no banco produtivo.

A cópia nativa foi restaurada em SQL Server 2022 novo, com rede Docker interna, sem portas publicadas, CPU/memória limitados e volumes/labels próprios. O banco original foi consultado somente para metadados e agregados. A imagem .NET 10 recebeu credenciais exclusivas de um usuário db_datareader da cópia e nenhum segredo produtivo. Recovery de jobs, migrations e storage migration ficaram desabilitados; a rede não permite SMTP/R2 externos.

Relatório completo protegido: `/opt/startingweb/apps/anderson-multimarcas-unified/snapshots/amm-restore-a2483165e38b/restore-report.json`. Evidências estruturais na mesma pasta: `source-schema.json`, `restored-schema.json`, `migration-trial-schema.json`, `expected-schema.json`. Os relatórios não contêm linhas de clientes.

| Verificação | Resultado |
| --- | --- |
| Native restore | Aprovado |
| DBCC CHECKDB | Aprovado |
| DBCC CHECKCONSTRAINTS | Zero violações |
| Tabelas/colunas | 20 tabelas, 280 colunas |
| Índices/FKs | 65 índices identificados; 22 relações FK, nenhuma desabilitada/não confiável |
| Schema e contagens origem × cópia | Iguais antes de qualquer migration |
| Identidade | AspNetUsers/AspNetRoles em uniqueidentifier; flags identity/PK/FK comparadas |
| CNPJ | Opcional em produção; nvarchar(18), enquanto modelo declara 14 |
| Aplicação na cópia original | Home, catálogo, empresa, login e imagem estática 200; Admin 302; commit correto, zero erros SQL |
| Cópia original após aplicação | Schema e contagens inalterados |
| DP de teste | Mesmas chaves entre containers sucessivos; volume produtivo não foi usado |

As contagens de veículos, lojas, marcas, mídias, importações e Identity foram comparadas sem expor seus registros. Seus valores ficam no recibo privado. Teste de login administrativo real/CRUD produtivo não foi realizado.

## SQL do CI e segunda cópia

SQL idempotente original: SHA-256 `cc838077ae10de1af0dc59fbc67e9cf372c3e6e4d575d0a7b4fc60f1dfb3265d`. O arquivo original foi preservado. Para execução via sqlcmd, remove-se apenas o BOM na leitura de stdin, usa-se `-I` e opções ANSI/ARITHABORT adequadas a índices filtrados. Sem QUOTED_IDENTIFIER, sqlcmd falha com 1934, embora SqlClient habilite a opção. Esse comportamento do cliente foi corrigido no ensaio, sem alterar o SQL nem produção.

O SQL foi aplicado somente em **AndersonSchemaTrial**, segunda cópia restaurada. Passou, e a reaplicação foi idempotente. Não alterou colunas/índices/FKs/checks nem contagens de negócio nessa cópia; acrescentou somente as duas entradas de histórico que faltavam:

- `20260530052000_RepairMissingAuditColumns`: as colunas já existiam; os guards evitaram DDL.
- `20261105185516_AlignRecoveredReleaseModel`: alinhamento de snapshot sem Up/Down estrutural.

Não se inseriram essas entradas no histórico produtivo para fabricar equivalência. A aplicação funcionou sobre a cópia original com 23 entradas. Uma terceira base vazia foi criada na instância de teste para executar o SQL inteiro e obter referência estrutural nova; a execução integral do SQL também passou.

O SQL contém operações históricas de alteração de Identity sob chaves, saneamento UPDATE, mudança de CNPJ e reconstrução de índices. Idempotência não elimina risco. Guards/histórico da cópia evitaram reaplicá-las; não executar o arquivo inteiro em produção por causa desse teste.

## Drift encontrado

Não faltam colunas esperadas na origem. Há 12 colunas adicionais ligadas ao legado/menu/roles/veículo, índices extras e quatro FKs extras; foram preservados. Existem 81 diferenças somente de defaults, além de diferenças de nulabilidade e capacidade. Comparar metadados normalizados, não tratar cada representação de DEFAULT como corrupção.

| Área | Diferença de gravação relevante |
| --- | --- |
| Veiculo | Modelo/Versao 100 × modelo 150; Cor 30 × 60; Titulo 150 × 180; Descricao 1000 × 4000; ObservacoesInternas 1000 × 2000; UrlVideo 255 × 400 |
| Loja | Email 150 × 180; algumas colunas mais largas ou anuláveis que o modelo, preservadas |
| Vendedor | Cargo 100 × 120; Email 150 × 180; FotoUrl 255 × 400 |
| Marca | LogoUrl NOT NULL/255 × nullable/400; IX_Marca_Nome não único × modelo único |
| Dados legados | Agregado detectou um grupo de nomes de marcas duplicados; nenhum valor/nome foi mostrado |

Em produção, agregados verificaram zero linhas com NULL nos campos obrigatórios avaliados de veículos/lojas e zero CNPJs acima de 14 caracteres. Isso não autoriza endurecer nulabilidade ou reduzir colunas. A ausência de índice único de marcas e os limites menores podem afetar gravações que os testes de leitura não cobrem.

## Proposta separada de correção

Branch `infra/schema-compatibility-review`, com proposta de ampliação de 12 colunas, preservando comprimentos já maiores e nulabilidade existente, e permitindo LogoUrl nulo conforme os modelos. Não muda dados/defaults, não reduz strings, não elimina tabelas legadas e não altera chaves GUID.

A proposta de índice único é separada e recusa a execução diante de duplicidade **antes de remover o índice existente**. Não há deduplicação automática. É necessária uma decisão funcional de dados ou de modelo antes de adotá-la. Os scripts são deliberadamente restritos à segunda cópia de ensaio; sua adoção produtiva exige revisão e autorização específicas.

O ensaio da ampliação passou na segunda cópia, com contagens preservadas e reaplicação idempotente. A aplicação continuou funcional depois da ampliação. A proposta de índice foi recusada com 51021 diante da duplicidade, preservando schema/contagens; criação e reaplicação passaram em dados exclusivamente sintéticos, e o guard recusou banco incorreto. A proposta não deve ser aplicada silenciosamente como parte do deploy; os gates de ativação continuam desligados. A leitura aprovada não equivale a aprovação global do schema de escrita.

## Repetição segura

```sh
python3 -I -B /opt/startingweb/backups/anderson-multimarcas/bin/restore_check.py \
  --bundle /opt/startingweb/apps/anderson-multimarcas-unified/incoming/rehearsal-artifact \
  --migration-sql /opt/startingweb/apps/anderson-multimarcas-unified/incoming/review-required.sql
```

Executar em janela com memória suficiente, backup validado e artefato revisado. A rotina cria exclusivamente recursos novos, não aceita nome de container produtivo como alvo e confere as labels antes da limpeza. Testar restauração mensalmente e antes de alteração de schema. Não transferir backups reais a serviços não aprovados.
