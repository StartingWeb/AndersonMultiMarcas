# Pendências antes da ativação

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Concluído e verificado

- Backup nativo inicial com checksum, SHA-256, VERIFYONLY e cópia criptografada Restic restaurada/autenticada.
- Restauração SQL isolada, DBCC CHECKDB, constraints, schema e contagens iguais à origem; aplicação .NET 10 sem migrations na cópia original.
- Full diário às 03:00 America/Sao_Paulo, systemd habilitado/persistente e usuário com linger; monitor horário; retenção 7/4/3.
- Destino unificado privado e configuração real fora do Git; legado vivo preservado, imagem exportada e testada com docker load.
- Environment production main-only, bypass administrativo false, cinco secrets SSH, chave exclusiva restrita e host key conferida.
- Ensaio real dos nove cenários de deploy/rollback e validação de configuração/rootfs OCI.
- R2 configurado, endpoint/região/base pública avaliados e HEAD do bucket produtivo 200, sem requests a objetos.
- Três gates GitHub false; gates locais ausentes; container Anderson, SQL compartilhado e NPM originais preservados. A substituição observada da API de outro projeto está registrada em [MIGRACAO_REPOSITORIOS.md](./MIGRACAO_REPOSITORIOS.md).

## Resultado desta execução

- Destinatário exclusivo `startingwebltda@gmail.com` configurado em arquivo 0600, SMTP compartilhado sem duplicar credenciais. Dois testes reais aceitos pelo Gmail com DATA 250/TLS 1.3; entrega na caixa de entrada não comprovada.
- Último backup controlado concluído em **05/11/2026 23:22:54 Brasília**, 30.007.296 bytes, SHA/VERIFYONLY/restore autenticado Restic aprovados. Último restore SQL completo: **05/11/2026 22:14:57 Brasília**, DBCC aprovado. Próximo diário: **06/11/2026 03:00 America/Sao_Paulo**. Retenção 7/4/3; timers habilitados/persistentes, monitor saudável.
- Environment `production`: main-only, bypass administrativo false, sem aprovação humana por publicação. Cinco secrets SSH exclusivos presentes; seus valores não foram exibidos. Três Repository variables seguem false; arquivos locais de habilitação e paridade ausentes.
- Proteção main aplicada pela API: CI `validate` obrigatório/strict/GitHub Actions, revisão de PR e do último push, code owner review, conversas resolvidas, admins incluídos, force push e exclusão bloqueados. CODEOWNERS proposto nesta PR para workflows/infra. Nenhuma alteração direta no código de main ou merge.
- Preflight fixo de backup íntegro/recente e contrato conhecido de schema instalado; teste real **recusou** as incompatibilidades SQL. Deploy/rollback não executam migrations. Rollback mantém caminho de recuperação independente de falhas do backup.
- Container legado/SQL/NPM preservados, com os mesmos IDs/imagens/StartedAt. Hashes dos proxy hosts 4 e 5 iguais ao snapshot anterior, upstream `andersonmultimarcas-web:8080` preservado; health HTTPS 200 nos três domínios auditados. Publicador antigo preservado. Nenhum deploy executado.

## Bloqueios e decisões externos

| Pendência | Evidência e ação necessária |
| --- | --- |
| Destinatário dos relatórios | Resolvido: endereço autorizado configurado e SMTP aceito. Entrega efetiva na caixa de entrada não confirmada pelo mecanismo. |
| Revisor independente no PR | Só StartingWeb apareceu como colaborador. Indicar e autorizar outro mantenedor para revisão de código antes da entrada em main. Não haverá required reviewers por execução no Environment. A proteção aplicada pode bloquear merges até existir esse revisor. |
| Branch main | Proteção global aplicada e confirmada; não relaxar para contornar falta de revisor. CODEOWNERS entrará em vigor após merge autorizado da PR #2. |
| Drift SQL para gravações | Produção tem colunas menores que o modelo e índice de marcas não único com duplicidade existente. Rever a branch separada infra/schema-compatibility-review e seu ensaio. Não se autoriza reduzir strings, eliminar/mesclar marcas ou mudar produção implicitamente. |
| Aceitação do schema | Ler VALIDACAO_SCHEMA_PRODUCAO.md; aprovar diferenças retidas, comportamento de escrita e rollback. Duas migrations de histórico estão ausentes, mas a aplicação de leitura funcionou sem executá-las. |
| R2 exclusivo de testes | Falta bucket/credenciais exclusivos. Preparar teste sintético upload/HEAD/read/hash/delete/limpeza. Não usar prefixo dentro do bucket produtivo. Permissões de escrita/exclusão e imagens reais não foram declaradas comprovadas. |
| Cópia fora da VPS | Backup atual está no mesmo disco. Indicar destino protegido aprovado e gestão independente da chave; armazenamento local não atende perda total da VPS. |
| Revisão das mudanças de infraestrutura | Rever e integrar manualmente a branch/PR. O artefato atual contém scripts anteriores ao ajuste SCP/OCI; o primeiro deploy via Actions precisa do CI e pacote do commit final. |
| Paridade e gates | Revisão formal da matriz, SHA final e arquivo de aprovação. Não criar source-parity.approved.json nem deploy.enabled ou habilitar variáveis sem autorização explícita. |
| Janela e corte | Definir operador, janela, observação e critérios; depois solicitar autorização do corte com evidências atualizadas. Workflow Release permanece ativo até essa janela. |
| Login SQL de runtime | O arquivo privado preparado ainda herda `sa`. O executor agora recusa login ausente/sa. Criar e revisar credencial dedicada com privilégios mínimos exige autorização própria de banco; não houve criação de usuário ou alteração de permissões nesta etapa. |
| Login administrativo produtivo | Validar com operador autorizado no aceite; nenhum login/senha real foi solicitado, exibido ou usado no ensaio. |

## Fora do escopo de mudança atual

O SQL compartilhado continua com exposição de porta 1433 e configuração de conexão observada sem criptografia obrigatória; firewall e confiança TLS precisam de revisão própria, sem reiniciar o serviço ou afetar outros bancos. Configurações antigas de backup de outros projetos não foram alteradas. Não se revogaram credenciais legadas nem se arquivou o Release.

O estado do monitor Anderson é saudável após o envio aceito. Nenhuma pendência foi substituída por gate fictício, endereço inventado ou uso de objetos de clientes em testes.

Documentos operacionais: [backup](./BACKUP_ANDERSONMULTIMARCAS.md), [schema](./VALIDACAO_SCHEMA_PRODUCAO.md), [R2](./VALIDACAO_R2.md), [ativação](./ATIVACAO_PRODUCAO.md) e [rollback](./ROLLBACK_PRODUCAO.md).

## Validação desta revisão

Testes Python: **95 aprovados** (28 CI/segurança, 42 deploy/arquivos, 25 operação), incluindo CI reprovado/outra branch/commit, Environment, pacote/SHA, transferência, health, rollback/falha de recuperação, backup ausente/vencido/corrompido, schema e lock concorrente. Nove cenários Docker reais passaram com limpeza verificada. Build Release: zero erros/avisos. YAML/Actionlint/ShellCheck e Gitleaks passaram. Auditoria NuGet: zero vulnerabilidades. A primeira execução .NET local revelou contaminação pelo helper Development; migrations/bootstrap foram desabilitados no modo test e adicionada regressão. Repetição .NET: **184/184 aprovados, sem skips**, incluindo SQL isolado, e container descartável removido. O CI do SHA final deve ser conferido na PR #2 antes de integração.

A PR #3 foi revisada sem modificações: ampliação transacional/idempotente preserva dados e comprimentos maiores; unicidade recusa os duplicados existentes antes de remover o índice. Os ensaios anteriores continuam registrados e seu CI está aprovado. Não há aceite completo de gravações, testes de escrita do binário antigo após a proposta ou solução funcional para a duplicidade. Esse bloqueio permanece explícito.

Nenhum destino externo de backup previamente autorizado nem bucket R2 exclusivo de homologação foi identificado na configuração auditada. Não houve cópia externa nova, PUT ou DELETE em produção. Essas etapas dependem de destino/credenciais e autorização próprios.
