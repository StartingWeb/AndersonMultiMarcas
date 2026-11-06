# CI/CD e preparação de produção

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Estado atual

A PR #1 já foi integrada. A referência validada é `1e5612276e0809166a2ea33a21deabee0bf0f768` em `main`. [CI 42135590822](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42135590822) aprovou 184 testes .NET, segurança, SQL e imagem. [CD 42136110621](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42136110621) empacotou o artefato `release-1e5612276e0809166a2ea33a21deabee0bf0f768-42136110621-1`; o job production foi ignorado.

A preparação desta branch instala backup e destino isolado, configura Environment/secrets e corrige a compatibilidade entre o image store do runner e o do servidor. **As correções de entrega precisam de revisão e merge autorizados, novo CI e novo artefato do commit resultante antes de usar o pipeline para o primeiro corte.** O artefato existente comprovou a aplicação sobre a cópia de produção, mas referencia a versão anterior dos scripts CI.

## Fluxo e proveniência

[ci.yml](../.github/workflows/ci.yml) restaura dependências travadas, compila .NET 10, executa testes com SQL Server descartável e testes Python de CI/deploy/operação, valida YAML, ensaia rollback, gera SQL idempotente, audita vulnerabilidades e publica aplicação/checksums. As actions estão fixadas por SHA; permissões são contents/read.

[cd.yml](../.github/workflows/cd.yml) aceita CI de push main do mesmo repositório, recusa fonte obsoleta, verifica manifestos/SHAs e constrói linux/amd64 no runner. O servidor recebe imagem pronta. Production exige os três gates de repositório em true, Environment protegido e gates privados locais; todos permanecem bloqueados.

O pacote contém `image.tar.gz`, `release.json` e `SHA256SUMS`. Hashes do ensaio:

| Arquivo | SHA-256 |
| --- | --- |
| Imagem | `4351bc95a24064c6ac04ced7f80931656eba8c6dc4a3a69252cbec1c03ce6300` |
| Manifesto | `0a09c4e0c30b612d6ec2b0316ca411b5c9437fe3394c71976481561d4af8ae24` |
| SQL original do CI | `cc838077ae10de1af0dc59fbc67e9cf372c3e6e4d575d0a7b4fc60f1dfb3265d` |

Imagem/config do CI: `sha256:cb6efb50078385a03a905c312cdfee30cb4c8f7ad355dc2a0c161b7791b33e24`. Imagem OCI efetiva no servidor: `sha256:08b3ab8301d123f5b99e359bc7ec5bb4c94a28b678f38aef7706fffe6b62680f`. Docker clássico identifica configuração; o image store containerd identifica manifest/index OCI. [release.py](../scripts/deploy/release.py) agora valida identidade/tag do arquivo, configuração carregada, rootfs, arquitetura e commit, e só então usa o ID imutável local. O manifesto original permanece inalterado. Tests cobrem identidade OCI, tag incorreta e configuração/camadas adulteradas.

## Environment e gates

`production` foi criado pela API GitHub com uma única deployment branch policy: `main`, tipo branch. `can_admins_bypass=false` foi gravado e confirmado por GET. A API real expõe esse campo; [verify_environment.py](../scripts/ci/verify_environment.py) agora recusa true ou campo ausente, além de exigir a declaração de autorização humana.

Somente `StartingWeb` apareceu como colaborador. Ainda falta indicar um revisor independente. Não se configurou o próprio iniciador como aprovação fictícia. GitHub recusou `prevent_self_review` sem ao menos um reviewer; ambos devem ser configurados juntos quando a identidade autorizada for informada. `main` não tinha proteção global de branch; sua política de PR/revisão também deve ser decidida antes da ativação.

| Tipo | Nome/estado |
| --- | --- |
| Repository variable | `PRODUCTION_DEPLOY_ENABLED=false` |
| Repository variable | `PRODUCTION_SOURCE_PARITY_CONFIRMED=false` |
| Repository variable | `PRODUCTION_ADMIN_BYPASS_REVIEWED=false` |
| Environment variable | `PRODUCTION_PUBLIC_HEALTH_URL=https://www.andersonmultimarcas.com.br/health/release` |
| Environment secret | `PRODUCTION_SSH_HOST` configurado |
| Environment secret | `PRODUCTION_SSH_USER` configurado |
| Environment secret | `PRODUCTION_SSH_PORT` configurado |
| Environment secret | `PRODUCTION_SSH_PRIVATE_KEY` configurado |
| Environment secret | `PRODUCTION_SSH_KNOWN_HOSTS` configurado |
| Gate local | `shared/deploy.enabled` ausente |
| Aprovação local | `shared/source-parity.approved.json` ausente |

## SSH de publicação

Chave Ed25519 exclusiva, conta existente `pedro`, com forced command [ssh_gate.py](../scripts/ops/ssh_gate.py), `restrict`, sem shell, SFTP, leitura de arquivos, forwarding ou PTY. Não se copiou a chave administrativa existente para o GitHub. A chave privada fica no Environment e em arquivo privado no SSH do Mac, fora do repositório.

Known hosts foi comparado com a confiança já estabelecida no Mac e com as chaves públicas do host obtidas por essa sessão SSH. Nenhum ssh-keyscan foi usado como prova única.

O gate aceita somente criação de diretório incoming com identidade de release, SCP de recebimento e execução do executor instalado/revisado. O Python arbitrário enviado pelo cliente nunca é executado. SHA do executor recebido precisa coincidir com a cópia instalada; mudanças futuras do executor exigem atualização revisada no servidor. Arquivos recebidos têm limite de tamanho. A conta Docker tem privilégios amplos no host; o escopo da chave é imposto pelo forced command, não pela associação ao grupo Docker.

[deliver.py](../scripts/ci/deliver.py) usa `scp -O` para o protocolo de recebimento restrito. O script antigo sem essa opção tentaria SFTP e seria recusado. Autenticação, host key, recebimento SCP e recusa de shell/leitura/SFTP/destino legado foram testados de fato. O executor real, chamado com a chave, recusou deploy por ausência de `deploy.enabled`.

## Servidor preparado

Raiz `/opt/startingweb/apps/anderson-multimarcas-unified`, modo 0700, com `shared/`, `releases/`, `incoming/`, `history/`, `snapshots/`, `bin/` e lock 0600. `shared/runtime.env` e `shared/server.json` foram preparados com modo 0600; valores reais não passaram pelo Git. O env-file usa valores Docker literais, sem expansão/aspas Compose. A configuração preserva R2, SQL, rede e volume DP e fixa migrations/importação/recovery de jobs em false.

Rede: `startingweb_network`. Volume: `andersonmultimarcas-dataprotection-keys`, usuário app 1654, caminho `/home/app/.aspnet/DataProtection-Keys`, aplicação `AndersonMultiMarcas`. Upstream continua `andersonmultimarcas-web:8080`. Não há mount de uploads na aplicação atual; arquivos do checkout legado foram inventariados e mantidos.

Fallback planejado: `andersonmultimarcas-legacy-fb8a205768dc`. **Esse nome é uma reserva do futuro corte; o container ainda não foi parado nem renomeado.** O executor recusa o container legado vivo/não gerido mesmo se alguém preparar gates por engano.

A imagem legada foi exportada e verificada por SHA, blobs e docker load. Consulte [ROLLBACK_PRODUCAO.md](./ROLLBACK_PRODUCAO.md). Nenhum prune, remoção de volumes antigos, reinício SQL ou alteração NPM foi executado.

## Verificação e limites

- Build local .NET 10: zero erros/avisos; 164 testes locais passaram e 20 testes SQL não foram executados nessa rodada local sem conexão. O CI remoto de referência executou 184/184, sem skips.
- **82 testes Python passaram**: 25 de CI/segurança, 38 de arquivo OCI/transações de deploy e 19 de operação. Os testes de operação foram incluídos na CI.
- Nove cenários Docker locais passaram com limpeza conferida: publicação, falhas de startup/health, pacote inválido/adulterado, configuração insegura, transferência interrompida, rollback por arquivo e retorno ao legado simulado.
- O artefato .NET real foi carregado no VPS, ligado somente à cópia SQL interna e verificado sem credenciais produtivas, jobs externos ou migrations na cópia original. DP persistiu entre containers de teste.
- HTTPS válido e `/health=200` foram confirmados nos três domínios. `/health/release=404` continua sendo a resposta do legado; a nova release deve responder com seu commit após um corte autorizado.
- O banco tem drift de limites/índice em relação ao schema novo, apesar dos testes de leitura aprovados. Correção foi preparada em branch separada. R2 de escrita, destinatário SMTP e revisores continuam pendentes.

Operação completa: [ATIVACAO_PRODUCAO.md](./ATIVACAO_PRODUCAO.md). Backup: [BACKUP_ANDERSONMULTIMARCAS.md](./BACKUP_ANDERSONMULTIMARCAS.md). Diferenças SQL: [VALIDACAO_SCHEMA_PRODUCAO.md](./VALIDACAO_SCHEMA_PRODUCAO.md). Pendências: [PENDENCIAS_PRODUCAO.md](./PENDENCIAS_PRODUCAO.md).
