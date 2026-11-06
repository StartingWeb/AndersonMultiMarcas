# Compatibilidade de schema, binários e rollback

Execução final `76c9228e388b`, de 06/11/2026 00:08:59 a 06/11/2026 00:12:20, America/Sao_Paulo. **Somente cópia descartável na VPS autorizada. Nenhum deploy, migration produtiva ou troca do legado.**

## Resolvido em ensaio

[Recibo sanitizado](./evidence/COMPATIBILITY_2026-11-06.json): **78 checkpoints aprovados**, quatro fases, nove negativas de permissões, DBCC CHECKDB aprovado e limpeza de todos os containers/volumes/rede confirmada. Nenhum registro de cliente saiu da VPS. Os logs públicos não contêm nomes reais, páginas, senhas ou conexões.

| Fase | Checkpoints | Resultado |
| --- | --- | --- |
| Schema original + binário legado | 19 | aprovado |
| Propostas de ampliação/diferenciação/índice + legado | 19 | aprovado |
| Schema ampliado + aplicação nova | 20 | aprovado |
| Retorno ao mesmo binário legado mantendo schema ampliado | 20 | aprovado |

Todos os cadastros usaram **anderson_runtime**, sem sysadmin/db_owner/DDL: login Identity sintético, leitura pública/admin, cadastro/edição de lojas, marcas, vendedores e veículos, consulta de veículo, upload/bytes/referência de imagem, exclusão física da linha de mídia e exclusão lógica dos quatro cadastros. Os arquivos-sentinela sintéticos de fases anteriores continuaram legíveis pelo binário seguinte. Configurações appsettings foram substituídas por arquivos sanitizados; nenhum env/mount/credencial produtivo foi herdado. Rede Docker internal, sem portas públicas, volumes SQL/DP/uploads novos, labels de execução e limites de memória/CPU.

A aplicação nova gravou Modelo/Versao 150, Titulo 180, Cor 60 e Descricao 4000 caracteres. O binário antigo leu e **editou novamente** esse veículo, alterando preço e preservando integralmente esses tamanhos. O rollback não executou SQL de redução de colunas ou exclusão de dados.

Imagem legada: `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5`. Nova: artefato auditado do commit `1e5612276e0809166a2ea33a21deabee0bf0f768`, identificado/autenticado pelo executor instalado. Esta rodada altera infraestrutura/testes/documentação, não o código da aplicação desse artefato. O recibo inclui ID imutável da imagem e hashes de scripts/backup.

A ampliação foi idempotente, preservou contagens, nulabilidade existente (exceto relaxar LogoUrl), IDs/FKs/defaults/dados legados. Nenhuma coluna foi estreitada. A criação do índice único foi recusada antes da proposta opcional de nomes; depois passou e foi reaplicada. O undo da proposta opcional foi idempotente e preservou o schema ampliado.

## Diagnóstico de dados e proposta opcional

[Auditoria de limites](./evidence/SCHEMA_TARGETS_2026-11-06.json) reconfirmou as 12 colunas nvarchar menores que o modelo, sem valores acima da capacidade do modelo nesses alvos. Há NULLs legítimos em campos opcionais; não endurecer nulabilidade nem converter para vazio. Modelos/configurações EF, formulários de escrita e SQL foram comparados; defaults/colunas/tabelas legadas extras permanecem preservados.

[Auditoria de marcas](./evidence/MARCA_CONFLICTS_2026-11-06.json): um grupo, dois registros ativos, IDs técnicos **1 e 4**, idênticos byte a byte, sem espaços iniciais/finais. ID 1 tem uma referência de Veiculo; ID 4 tem zero. A única FK é Veiculo.MarcaId, NO_ACTION. Collation SQL_Latin1_General_CP1_CI_AS: ignora caixa, distingue acento e a comparação SQL ignora espaços finais. Neste conflito não há variantes: um grupo tanto por bytes exatos quanto pela comparação CI_AI. Não foram publicados os nomes.

Recomendação para decisão funcional: **preservar IDs/registros/referências** e avaliar diferenciar o rótulo do registro sem referências. [RENAME_UNREFERENCED_MARCA_PROPOSAL.sql](./schema/RENAME_UNREFERENCED_MARCA_PROPOSAL.sql) acrescenta um sufixo técnico ao ID 4, somente no ensaio e apenas no estado previsto. Guarda o nome original em ledger privado, usa SERIALIZABLE/locks/transação, recusa terceiros/novas referências/colisão/truncamento/collation inesperados e permite replay. Não escolhe registros para excluir, não mescla e não move FKs.

Alternativas: unificação com plano de referências e decisão de negócio (maior risco); rótulos funcionais específicos aprovados pelo responsável; ou revisão deliberada da regra/modelo de unicidade. Apenas desativar uma marca não resolve um índice único global que inclui inativos. Não adotar a alternativa de remover a unicidade silenciosamente para liberar o deploy.

[UNDO_MARCA_RENAME_TRIAL.sql](./schema/UNDO_MARCA_RENAME_TRIAL.sql) desfaz somente o rename/índice opcionais da cópia com ledger/índice esperados. Reintroduz deliberadamente o conflito original, portanto **não é o rollback normal do binário** e não deve ser executado em produção sem revisão própria. Nunca reduz as colunas ampliadas.

## Reprodução e testes

O harness [rehearse.py](../scripts/compatibility/rehearse.py) exige o helper/executor de backup já auditado na PR #2 e recebe a proposta de grants dessa PR como arquivo revisado. Resolve sempre recursos de teste com prefixo/label aleatórios; a limpeza confere propriedade. O banco real só é consultado nos scripts de auditoria somente leitura, nunca pelo harness para gravação.

```sh
python3 -I -B rehearse.py --schema-dir CAMINHO_PRIVADO_REVISADO/schema \
  --runtime-grants CAMINHO_PRIVADO_REVISADO/runtime_permissions_trial.sql
```

O comando roda somente na VPS com as raízes fixas auditadas, memória disponível, backup autenticado e artefato revisado. Relatórios completos privados em `snapshots/amm-compat-76c9228e388b/report.json`. Falhas iniciais do próprio harness (permissão de fixture e handler incorreto de mídia) foram corrigidas; não se suprimiu requisito para obter o resultado final.

O teste SQL automatizado da PR cria AndersonSchemaTrial em instância descartável, aplica/reaplica expansão, verifica recusa 51021 sem remover índice, renomeia/cria índice/desfaz com contagens preservadas, recusa terceiro duplicado e banco incorreto. Testes Python exercitam formulário/antiforgery e hash Identity V3. Nenhum teste desses seleciona um banco produtivo como alvo mutável.

## Preparado, aguardando autorização

A ampliação e a alternativa opcional de dados estão preparadas e ensaiadas; a forma produtiva continua exigindo revisão, decisão funcional sobre nomes e autorização separada de SQL. Os scripts permanecem protegidos por DB_NAME de ensaio. Não removê-lo para executar produção por conta própria. Preparar janela/backup/recibo e versão produtiva revisada somente após essa autorização.

## Bloqueante

O schema **produtivo continua original e incompatível** com o contrato de escrita; o preflight deve continuar recusando-o. A decisão de nomes não foi tomada pelo ensaio. R2 real em homologação e sua compatibilidade de rollback não foram comprovados; os testes de mídia desta matriz usam storage local isolado. Não habilitar os gates nem considerar o ensaio uma autorização de deploy/merge.
