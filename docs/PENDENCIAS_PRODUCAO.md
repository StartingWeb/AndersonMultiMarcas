# Estado de preparação e bloqueios de produção

Atualizado em 06/11/2026, America/Sao_Paulo. **Nenhum deploy, merge, migration produtiva ou restart autorizado/executado.**

## Resolvido

- Backup diário às 03:00 America/Sao_Paulo, timers de backup/monitor enabled/active e persistentes, configuração 0600 e `startingwebltda@gmail.com`. Último backup controlado: 05/11/2026 23:22:54 Brasília, 30.007.296 bytes, SHA/VERIFYONLY/recuperação autenticada Restic aprovados. Último restore SQL completo inicial: 05/11/2026 22:14:57, DBCC aprovado. Próximo diário: 06/11/2026 03:00; não houve espera artificial nem novo e-mail nesta revisão. Retenção 7/4/3; monitor saudável.
- Dois testes SMTP da etapa anterior foram aceitos pelo Gmail com DATA 250/TLS 1.3; isso não confirma entrega na caixa de entrada. Credenciais compartilhadas não duplicadas.
- Environment production main-only, can_admins_bypass=false, sem required reviewers por publicação; cinco secrets SSH exclusivos presentes. Main com validate/strict/GitHub Actions, revisão de PR/último push, conversas resolvidas, admins incluídos e force push/exclusão bloqueados. Nenhuma proteção foi removida.
- Preflight continua recusando backup ausente/vencido/corrompido, schema conhecido incompatível, runtime sem identidade ou com `sa`. Concorrência, lock, commit/proveniência/SHA, health e rollback cobertos pelos testes existentes.
- Reauditoria SQL: 12 colunas nvarchar menores que o modelo, sem valores acima dos limites propostos. Um grupo duplicado de Marca.Nome, IDs técnicos 1/4 idênticos byte a byte, sem espaços nas extremidades. Uma referência de veículo no ID 1 e nenhuma no ID 4. Nenhum nome real foi publicado.
- Ensaio na cópia da VPS: antigo/original → antigo/ampliado → novo/ampliado → rollback binário antigo, com 78 checkpoints e nove negativas de permissões, DBCC e limpeza conferidos. Inclui CRUD, exclusão física de mídia, arquivos locais compartilhados e escrita máxima pela nova aplicação editada pelo binário antigo sem truncamento. Evidências e propostas na [PR #3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3).
- Perfil SQL restrito ensaiado: CRUD em 14 tabelas utilizadas, leitura nas demais tabelas Identity auxiliares/metadados/menus, sem sysadmin/db_owner/DDL/procedures de negócio. [Proposta e mecanismo de credencial](./USUARIO_SQL_RUNTIME.md). Fixtures de criação de banco usam admin só no SQL descartável; os fluxos de runtime usam o login dedicado.
- R2: HEAD do bucket 200 novamente, zero operações em objetos produtivos. Oito testes S3 em memória adicionados, cobrindo integridade, overwrite, nomes especiais, limite, ausência, falhas e timeout. [Auditoria e limites](./VALIDACAO_R2.md).
- Validação local da PR #2: restore locked, build Release sem erros/avisos, **193 testes .NET sem skips**, **98 Python** (28 CI, 42 deploy, 28 operação), nove cenários Docker/rollback e limpeza, YAML/Actionlint/ShellCheck, diff e Gitleaks; NuGet sem vulnerabilidades. A PR #3 acrescenta ensaio SQL automatizado e três testes Python; seus resultados finais ficam registrados na própria PR.

## Preparado, aguardando autorização

- Ampliação idempotente/transacional preservando comprimentos maiores, nulabilidade existente e dados; alternativa opcional para diferenciar o ID 4 com ledger privado e undo sem reduzir colunas. A escolha do rótulo não foi feita pelo ensaio. A unicidade recusa duplicados/índice inesperado antes de remover qualquer índice. Scripts aceitam somente bases de ensaio.
- Login/grants e proposta de conexão 0600 fora do checkout, sem senha no Git/log, sem conexão/instalação automática. Nada foi criado na instância produtiva; o runtime privado ainda herda `sa` e é recusado.
- Contrato de [backup externo](./BACKUP_EXTERNO_PROPOSTA.md), desabilitado e sem destino/credenciais. A configuração de backup reutilizada continua local; nenhum destino externo autorizado foi identificado nela.
- [Ativação](./ATIVACAO_PRODUCAO.md) e [rollback](./ROLLBACK_PRODUCAO.md) documentados. O fluxo futuro é push/merge main → CI aprovado → pacote imutável/proveniência → preflights → publicação/health/commit → sucesso ou rollback. Não há aprovação manual por release após a ativação inicial.

## Bloqueante

1. **Decisão funcional e autorização SQL:** escolher solução para os nomes duplicados e revisar/autorizar a versão produtiva de ampliação/índice/dados. O schema e duplicados produtivos continuam originais e incompatíveis. Não remover os guards de ensaio nem executar migrations de startup.
2. **Instalação SQL runtime:** autorizar criação de login/usuário/grants mínimos e atualização privada da conexão; validar permissões efetivas. `sa` produtivo preparado permanece recusado. Não ampliar os grants para fazer fixtures administrativas passarem.
3. **R2 real de homologação:** fornecer bucket/endpoint/base pública/credenciais exclusivos autorizados e executar ciclo real com falhas/rollback. Local/mocks não comprovam recuperação de arquivos R2. Nenhum PUT/DELETE produtivo permitido.
4. **Cópia externa:** destino/namespace/chave independente/credencial/responsável e transferência real expressamente autorizados, com recuperação e monitor externo. O backup no mesmo disco não cobre perda total da VPS.
5. **Revisão independente:** só StartingWeb e nenhuma equipe elegível foram encontrados. **Merge bloqueado por falta de revisor independente autorizado.** Não convidar/conceder acesso nem reduzir proteção para contornar isso.
6. **Aceitação e primeiro corte:** revisão/merge autorizados das PRs, paridade formal, CI/artefato do SHA final integrado, janela/operador/retorno e autorização separada explícita de ativação/primeiro corte. As pendências anteriores precisam ser resolvidas antes dessa decisão.

## Produção preservada

Três Repository variables continuam false; `shared/deploy.enabled` e `shared/source-parity.approved.json` ausentes. Não há deployment production criado. Legado, SQL e NPM mantêm IDs/imagens/inícios; upstream legado e configurações dos proxy hosts preservados. Não houve alteração estrutural ou de dados pelo trabalho; auditorias produtivas são somente leitura. Publicador antigo preservado, sem DNS/NPM/restart/merge. Metadados/contagens e hashes do proxy são reconferidos ao encerramento.

O SQL compartilhado exposto na porta 1433 e a confiança TLS existente permanecem temas de revisão própria, sem alterar outros bancos/serviços nesta tarefa. Auditorias locais preexistentes seguem fora dos commits.
