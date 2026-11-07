# Estado de produção — 07/11/2026

**A/B/C aplicadas e validadas. O site continua no legado; nenhum merge ou deploy foi executado.** [Recibo sanitizado](./evidence/PRODUCTION_SQL_2026-11-07.json).

## Concluído e comprovado

- Novo backup pré-migration `20261107T150452Z-c36eef7e`, COPY_ONLY/CHECKSUM/VERIFYONLY, SHA-256, Restic autenticado e check/read-data aprovados. Restauração SQL real `46fe30bdb760`: DBCC, constraints, schema e contagens iguais à origem. Relatório e confirmação de restauração aceitos pelo Gmail (DATA 250/TLS 1.3), sem prova de entrega na caixa de entrada. Nenhum backup anterior excluído; retenção diária 7/4/3 preservada.
- A: 12 ampliações, somente Marca.LogoUrl passa a nvarchar(400) NULL. Demais nulabilidades e valores preservados.
- B: somente Nome do ID 4, Jaguar → Jaguar (cadastro adicional). ID 1 Jaguar, veículos 1/0, ambos ativos e mesmas datas de cadastro. Nenhuma exclusão, mudança de MarcaId ou relacionamento.
- C: IX_Marca_Nome único, não filtrado, coluna Nome; zero grupos duplicados sob SQL_Latin1_General_CP1_CI_AS. Três recibos no journal; histórico EF original com 23 entradas preservado.
- DBCC produtivo e preflight de backup/schema aprovados. Runtime existente autenticado: CRUD necessário, sem sysadmin/db_owner/DDL/admin; nenhuma recriação/rotação.
- Ensaio dos SQLs produtivos exatos na cópia: antigo/original → antigo/ampliado → novo/ampliado → retorno ao antigo, 78 checkpoints e nove negativas de permissão. Inclui login sintético, CRUD, mídia local, limites máximos e replay A/B/C; DBCC e limpeza aprovados. Nenhum teste de escrita em produção.
- Comparação das 20 tabelas: 19 inalteradas (normalizando apenas o nome autorizado). Veiculo teve somente quatro incrementos de visualizações em um registro durante a janela; nova restauração do backup comprovou os demais campos dos 401 veículos iguais. Não foram revertidas gravações legítimas.
- Legado healthy, rotas públicas/login/imagem padrão 200, três domínios HTTPS 200, zero erros SQL/falhas de aplicação nos logs da janela. IDs/imagens/inícios de legado, SQL e NPM preservados. Login autenticado foi ensaiado na cópia; nenhuma conta real foi redefinida.
- Preflight corrigido para selecionar o banco por `sqlcmd -d`: a mensagem de mudança de contexto não contamina o escalar. Teste de regressão e 37 testes operacionais passaram. Backup extra admite preservação explícita de todas as cópias, sem alterar o padrão diário.

## Preparado, aguardando dependência

Environment production limitado à main, cinco secrets SSH, sem aprovador por release e sem bypass de admins. Fluxo CI → artefato imutável/proveniência → preflight → deploy/health/rollback preparado. As três variáveis de ativação seguem false; os arquivos locais de habilitação/paridade continuam ausentes.

Ordem: revisar/integrar PR #3 → atualizar PR #2 com main resultante → CI combinado/revisão → integrar PR #2 → CI do SHA final. Main permanece `1e5612276e0809166a2ea33a21deabee0bf0f768`. O CI das atualizações desta rodada deve passar antes da integração.

## Bloqueios externos e ação exata

1. **Revisor independente:** somente StartingWeb é colaborador, sem equipe elegível. Proprietário deve indicar e autorizar mantenedor independente com acesso de escrita, incluí-lo corretamente no CODEOWNERS aplicável e obter revisão real do último push. Main exige validate strict, uma aprovação, code owner aplicável, aprovação do último push por outro ator e conversas resolvidas, inclusive admins. Não houve convite, bypass ou remoção de regras.
2. **R2 real:** HEAD autenticado 200, endpoint/base HTTPS e região auto conferidos, sem PUT/DELETE. A amostra posterior encontrou veículos 200 e vendedores 404 (ver falha técnica abaixo). Falta bucket/prefixo de homologação expressamente autorizado e isolado, credencial privada restrita e URL de teste; executar ciclo sintético real conforme [VALIDACAO_R2.md](./VALIDACAO_R2.md). Mocks/storage local não substituem o provedor.
3. **Backup externo:** informar destino/namespace/chave/acesso/responsável autorizados para segunda cópia criptografada e restauração, ou aceitar explicitamente a operação temporária sem cobertura de perda total da VPS. Nenhum destino novo recebeu dados; nenhum serviço foi contratado.
4. **Paridade e corte:** concluir evidências R2 e revisão integrada, aprovar paridade com evidência e confirmar pacote/rollback/health do SHA final. A publicação já tem autorização condicional; não pedir novamente autorização SQL. Criar novo backup restaurável imediatamente antes do corte futuro.

## Falhas técnicas

Banco/preflight sem falha aberta. Na leitura R2 real, três fotos de vendedores retornaram 404 público e S3 autenticado, sem original encontrado no container/checkout legado; três imagens de veículos responderam 200. Localizar originais ou fonte de recuperação autorizada e definir reparação, sem inventar arquivos/substituir URLs. Detalhes em [VALIDACAO_R2.md](./VALIDACAO_R2.md). As divergências de auditoria SQL foram investigadas; publicação segue bloqueada também pelas dependências acima. Não criar deploy.enabled nem atestar paridade antecipadamente. [Ativação](./ATIVACAO_PRODUCAO.md), [rollback](./ROLLBACK_PRODUCAO.md), [backup](./BACKUP_ANDERSONMULTIMARCAS.md).
