# CI/CD e preparação de produção

Atualizado em 07/11/2026. Preparação autorizada; **nenhum merge, deploy ou ativação nesta rodada**. [Auditoria](./evidence/PIPELINE_AUDIT_2026-11-07.json) e [pendências](./PENDENCIAS_PRODUCAO.md).

## Estado auditado

Main: `1e5612276e0809166a2ea33a21deabee0bf0f768`. [CI 42135590822](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42135590822) aprovado e [CD 42136110621](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42136110621) com pacote produzido/production ignorado são evidências históricas desse SHA. Não aprovam os commits novos das PRs.

[PR #3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3), tip auditado `8116388ca2eaac0ba7e9a0e473c9401b776f8912`, e [PR #2](https://github.com/StartingWeb/AndersonMultiMarcas/pull/2), tip antes destes recibos `32ffadfa3ab78a8cbed148fff5ea541d03e041cc`, continuam abertas/draft com validate aprovado e sem conflito com main. A simulação da combinação inicial não apresentou conflitos; não é merge nem CI combinado. A branch da PR #2 recebe a correção descrita abaixo e precisa de CI no novo tip. [Auditoria GitHub](./GITHUB_POLITICA_REPOSITORIO_INDIVIDUAL.md).

## Fluxo e proveniência

[ci.yml](../.github/workflows/ci.yml) executa em PR/push/workflow_dispatch: restore travado, build .NET 10, testes SQL descartáveis, segurança/HTTP/storage, Python, Actionlint/ShellCheck, falhas de deploy/rollback Docker, SQL revisável sem acesso produtivo e auditoria NuGet. Somente push/main publica o artefato de aplicação imutável `app-SHA-attempt`, sem sobrescrita. Permissão global `contents: read`, actions por SHA e checkout sem persistência de credenciais.

[cd.yml](../.github/workflows/cd.yml) recebe `workflow_run` de CI e exige sucesso, evento push, branch main e mesmo repositório. Confere igualdade entre SHA do CI, checkout e main consultada no checkout, baixa artefato do run correto e verifica manifest/checksums/commit/ciRunId. Constrói linux/amd64 no runner; nunca compila no servidor.

**Correção de 07/11:** a reconstrução da imagem no CD podia diferir da imagem testada no CI, inclusive por dependências instaladas no Dockerfile. O CD agora ensaia a própria imagem final com [test_runtime_image.py](../scripts/ci/test_runtime_image.py), sem rede, SQL ou R2, usando conexão sintética. Verifica usuário app/arquitetura/revisão, startup/health, commit, fallback estático, WebP nativo e recusa de migrations produtivas. Confere limpeza dos containers descartáveis. Não é deploy de teste em produção.

[package_release.py](../scripts/ci/package_release.py) exige `--runtime-report`, status passado, todos os cenários, rede none, ausência de containers remanescentes, mesmo commit e **mesmo imageId** da imagem a exportar, além de usuário não root. Evidência ausente/incompleta/divergente bloqueia antes da exportação. O relatório fica no artefato `cd-evidence-run-attempt` por 90 dias, inclusive em falha; o bundle imutável continua com `image.tar.gz`, `release.json`, `SHA256SUMS`, retido 90 dias, sem sobrescrita.

O servidor valida novamente SHA/manifesto/tag/configuração/rootfs/arquitetura/commit. Docker clássico usa config digest e o image store containerd pode usar OCI digest; [release.py](../scripts/deploy/release.py) autentica ambas as identidades, usa ID imutável local e preserva o manifesto do CI. Os testes de adulteração OCI/transação/rollback continuam obrigatórios. SHA/checksum e origem do run fornecem proveniência interna; não foi criada uma atestação Sigstore externa.

## Environment, governança e gates

`production`: apenas branch `main`, tipo branch, `can_admins_bypass=false`, sem required reviewers por release. [verify_environment.py](../scripts/ci/verify_environment.py) recusa bypass ausente/true, revisores por execução, tags/outras branches e main sem proteção. Esse helper verifica existência da proteção; a configuração detalhada é auditada pela API administrativa conforme a [política GitHub](./GITHUB_POLITICA_REPOSITORIO_INDIVIDUAL.md).

Proteção aplicada em 07/11 às 16:26 UTC: PR obrigatório, validate strict app 15368, **zero aprovações**, code owner/último push false, descarte de aprovações antigas e conversas resolvidas, admins sujeitos às regras e force push/exclusão proibidos. Comparação integral antes/depois confirmou somente os três campos autorizados. Nenhum ruleset/bypass criado. CODEOWNERS da PR #2 permanece intacto. [Recibo da política](./evidence/GITHUB_POLICY_APPLIED_2026-11-07.json).

| Tipo | Nome/estado revalidado |
| --- | --- |
| Repository variable | `PRODUCTION_DEPLOY_ENABLED=false` |
| Repository variable | `PRODUCTION_SOURCE_PARITY_CONFIRMED=false` |
| Repository variable | `PRODUCTION_ADMIN_BYPASS_REVIEWED=false` |
| Environment variable | `PRODUCTION_PUBLIC_HEALTH_URL=https://www.andersonmultimarcas.com.br/health/release` |
| Environment secrets, somente nomes | `PRODUCTION_SSH_HOST`, `PRODUCTION_SSH_USER`, `PRODUCTION_SSH_PORT`, `PRODUCTION_SSH_PRIVATE_KEY`, `PRODUCTION_SSH_KNOWN_HOSTS` |
| Gate local | `shared/deploy.enabled` ausente |
| Aprovação local | `shared/source-parity.approved.json` ausente |

O CD serializa publicações com `cancel-in-progress=false`; no servidor há lock e journal. CI pode cancelar execução superada por novo commit. Checks de um SHA anterior não aprovam outro. Há uma janela entre consulta da main e entrega; o SHA entregue ainda precisa ser o que passou no CI e o CD é serializado. A origem nunca é uma tag mutável ou um rebuild no host produtivo.

## SSH e servidor

Raiz exclusiva `/opt/startingweb/apps/anderson-multimarcas-unified`, modo 0700. Env/server.json privados 0600; valores reais fora do Git. A conexão futura usa `anderson_runtime`; login/configuração do legado preservados, sem recriação/rotação. Migrations/importação/recovery de jobs permanecem false.

Chave Ed25519 dedicada com forced command [ssh_gate.py](../scripts/ops/ssh_gate.py), restrict, sem shell/SFTP/leitura/forwarding/PTY. [deliver.py](../scripts/ci/deliver.py) usa SCP receive-only `-O`, valida destino exclusivo e host key fixada, transfere pacote e só permite executor instalado/revisado com SHA igual. A chave administrativa do Mac não é a chave do GitHub. A conta Docker tem privilégios amplos no host; o isolamento é imposto pelo forced command. Alterar o executor requer instalação revisada separada; esta correção de empacotamento não altera o executor instalado.

Rede `startingweb_network`, volume DP `andersonmultimarcas-dataprotection-keys`, usuário app 1654, caminho `/home/app/.aspnet/DataProtection-Keys`, ApplicationName AndersonMultiMarcas. Upstream legado continua `andersonmultimarcas-web:8080`; nenhum mount novo de uploads. O fallback reservado `andersonmultimarcas-legacy-fb8a205768dc` ainda não foi renomeado/criado. O executor recusa parar/substituir legado vivo não gerido.

## Preflight, saúde e rollback

Antes de deploy, o executor exige checker fixo de backup/schema, pacote íntegro, gates/paridade, configuração segura e baseline conhecido. A/B/C já aplicadas; não há migration automática. Antes do primeiro corte futuro exigir novo backup restaurável, sem excluir anteriores. [Backup local aceito](./ACEITE_RISCO_BACKUP_LOCAL.md) não elimina verificações, monitor/alertas ou retenção 7/4/3.

Health interno exige liveness e commit; HTTPS público deve responder o mesmo commit em `/health/release`. Falha inicia retorno pelo journal ao container/configuração anterior. Rollback não exige checker de backup para evitar impedir recuperação da aplicação; preserva lock/gates/integridade/health. Não altera SQL ou objetos R2. O legado tem `/health=200` e `/health/release=404`; não atribuir um SHA do código recuperado ao binário antigo.

## Verificação desta rodada e limites

Os resultados exatos e os SHAs auditados constam em [PIPELINE_AUDIT_2026-11-07.json](./evidence/PIPELINE_AUDIT_2026-11-07.json). Testes Python cobrem configuração GitHub, proveniência, rejeição de evidências da imagem, backup, chaves/caminhos, falhas de deploy e rollback. Actionlint/ShellCheck e scanner de secrets devem passar antes do push. O ensaio real local usa imagem .NET previamente disponível como fixture; ele verifica o novo empacotador, não aprova uma futura release integrada. Sem SQL descartável configurado, testes .NET SQL são explicitamente ignorados localmente; o CI obrigatório os executa em SQL descartável.

[R2 real isolado](./VALIDACAO_R2.md) homologado: ciclo autenticado, oito AccessDenied, HTTPS/hash públicos e limpeza aprovados, com credencial exclusiva. A [investigação das fotos](./RECUPERACAO_MIDIAS_R2.md) continua pendente. Zero objetos produtivos alterados.

Sequência restante: integração autorizada PR #3 → atualização/CI combinado PR #2 → integração PR #2 → CI main final → pacote/runtime final → mídias/paridade → backup novo restaurável → autorização de primeiro corte/gates. [Ativação](./ATIVACAO_PRODUCAO.md), [rollback](./ROLLBACK_PRODUCAO.md), [pendências](./PENDENCIAS_PRODUCAO.md). Nada nesta documentação autoriza merge/deploy nesta rodada.

## Revalidação de 07/11 após política/R2

PR #2 no SHA 32ffadf: CI pull_request 42390020907, 193 testes .NET e 111 Python aprovados. PR #3 no SHA 8116388: CI pull_request 42386554511, 187 .NET e 59 Python aprovados. Checks de push também aprovados nos mesmos tips. A nova publicação documental exige CI do seu próprio SHA; essas contagens não aprovam commits futuros. O teste offline da imagem final permanece incorporado ao CD, que não foi disparado manualmente. Não houve publicação de imagem produtiva. Artefatos dos runs consultados continuam não expirados; CI das PRs não constitui pacote final de main nem aprova seu SHA integrado. [Comparação operacional](./evidence/POLICY_R2_OPERATIONS_2026-11-07.json).
