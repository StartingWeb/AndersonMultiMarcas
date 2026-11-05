# CI/CD e publicação do AndersonMultiMarcas

Data da investigação: 2026-11-05. Este documento descreve a implementação local e a preparação de uma ativação futura. **Nenhum deploy de produção está autorizado ou habilitado por esta entrega.**

## Situação e bloqueios

O repositório único é [StartingWeb/AndersonMultiMarcas](https://github.com/StartingWeb/AndersonMultiMarcas), branch `main`. A implementação foi preparada em worktree isolada, a partir de `origin/main` em `162602aa97c58c53ffbec2aad5120219a20de214`. O checkout de trabalho anterior foi preservado.

Os bloqueios de ativação são concretos:

- A publicação legada contém 26 commits posteriores à última revisão da fonte. Antes de substituir produção, recuperar e revisar as correções de lojas/CNPJ, schema, upload R2, catálogo e tag de veículos. A versão informacional das DLLs mantém o SHA antigo e não demonstra equivalência funcional.
- A auditoria anterior identificou escritas de API sem autenticação, senhas constantes e dependências vulneráveis. CI verde não substitui a correção e a revisão desses riscos.
- Não foi demonstrado backup recente e restaurável do banco `AndersonMultiMarcas`. O backup diário observado cobre outros sistemas.
- Não foram configurados Environment, revisores, variáveis, secrets, diretórios ou gates no servidor. O workflow antigo continua ativo.
- A política de limpeza diária de imagens Docker precisa preservar o baseline de rollback. Arquivos offline das imagens são necessários; tags locais isoladas não são garantia suficiente.
- O runtime .NET 9 aproxima-se do fim do suporte em 2026-11-10. A migração para uma versão suportada integra os requisitos de ativação; o pipeline mantém .NET 9 para validar a base solicitada. [Política oficial de suporte](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

Veja a investigação e o plano de corte em [MIGRACAO_REPOSITORIOS.md](./MIGRACAO_REPOSITORIOS.md) e a recuperação em [ROLLBACK_PRODUCAO.md](./ROLLBACK_PRODUCAO.md).

## Arquitetura escolhida

Manter Docker reduz a mudança operacional: a aplicação atual, SQL Server e Nginx Proxy Manager já usam Docker e a rede `startingweb_network`; o host não tem runtime .NET disponível no PATH. Não há necessidade de instalar runtime no servidor ou introduzir um registry nesta fase.

O runner constrói a aplicação e a imagem, identifica tudo pelo SHA integral do commit e exporta a imagem em arquivo. O servidor recebe o pacote verificado por SSH, carrega a imagem já construída e executa uma publicação controlada. Não há `git pull`, `git reset` ou build de código no servidor no fluxo novo.

```text
branch / pull request
        │
        ▼
CI: restore → build Release → testes → segurança → SQL revisável
        │
        ├─ pull request: relatórios e validação, sem deploy
        │
        └─ push em main aprovado em todas as etapas
                         │
                         ▼
                aplicação publicada + artefato app-<SHA>-<tentativa-CI>
                         │
                         ▼
CD: workflow_run bem-sucedido, repositório e SHA exatos
                         │
                         ▼
             imagem Docker + pacote de release
                         │
                         ▼
gates de ativação + revisão do Environment production
                         │
                         ▼
SSH com host conhecido → verificação de integridade → lock
                         │
                         ▼
snapshot → troca controlada → health e commit esperado
                         │
                         └─ falha: restaurar aplicação anterior
```

O pacote contém `image.tar.gz`, `release.json` e `SHA256SUMS`; a imagem usa a tag `andersonmultimarcas-unified:<SHA>`. O manifesto registra a identidade do commit e da imagem. O SHA-256 esperado do manifesto é transmitido separadamente ao comando remoto, e os checksums são conferidos antes de carregar a imagem. Integridade depende também da confiança no runner, nas actions e na chave SSH; o checksum não é uma assinatura independente.

O artefato da aplicação é publicado pela CI somente após sucesso completo, para um push da `main`. A CD consome o artefato da execução de CI que a disparou, sem substituir o SHA por uma leitura posterior da ponta da branch, e constrói a imagem no runner. Se a main avançou enquanto o job aguardava aprovação, a verificação de revisão atual recusa a publicação obsoleta. O armazenamento de artefatos do GitHub é temporário: os artefatos de aplicação/evidência retêm 30 dias e os bundles da CD 90 dias. Definir retenção operacional protegida no servidor antes de ativar.

Os nomes incluem a tentativa da execução: `app-<SHA>-<tentativa-CI>` e `release-<SHA>-<run-CD>-<tentativa-CD>`, mantendo `overwrite: false`. A produção baixa o bundle pelo ID imutável devolvido pelo job de empacotamento; isso também permite repetir somente jobs falhos sem procurar um artefato inexistente da nova tentativa. O servidor conserva snapshots por commit e recusa substituir uma release existente por conteúdo diferente. Uma reconstrução do mesmo SHA que produza outra imagem exige análise e novo commit, em vez de sobrescrever o pacote histórico.

## CI

O workflow [ci.yml](../.github/workflows/ci.yml) executa em pull requests, pushes em branches e acionamento manual. As etapas incluem checkout, setup do SDK .NET 9.0.320, restore, build Release, testes .NET, testes dos scripts Python, validações de segurança e geração offline de SQL idempotente revisável. Resultados de testes e relatórios são preservados também quando as verificações falham.

A verificação NuGet inclui dependências transitivas. Vulnerabilidades de severidade alta ou crítica reprovam a CI e impedem a criação do artefato de publicação; não há supressão automática para fazer o pipeline ficar verde. O inventário anterior de vulnerabilidades deve ser tratado como dívida conhecida, não como justificativa para ignorar o gate.

Os scripts de migration são gerados com uma factory de design time, sem abrir conexão com banco real e sem executar a inicialização da aplicação. A geração do SQL não afirma que o schema de produção seja compatível: há migrations legadas inconsistentes e operações potencialmente destrutivas a revisar.

Permissões de workflow são mínimas. Actions devem permanecer fixadas por commit confiável; atualizações de seus SHAs devem passar por revisão. O job que prepara o artefato não recebe credenciais de produção.

## CD desabilitada por padrão

O workflow [cd.yml](../.github/workflows/cd.yml) foi preparado para `workflow_run` de CI bem-sucedida após push na `main` do próprio repositório. A execução de workflows que recebem esse evento exige cuidado: ela não deve aceitar artefatos de forks ou executar scripts baixados de uma pull request.

Há camadas independentes de bloqueio:

1. `PRODUCTION_DEPLOY_ENABLED` deve ser exatamente `true`.
2. `PRODUCTION_SOURCE_PARITY_CONFIRMED` deve ser exatamente `true`, após resolver e revisar a divergência entre fonte e binários legados.
3. `PRODUCTION_ADMIN_BYPASS_REVIEWED` deve ser exatamente `true`, somente após conferir manualmente na interface do GitHub que o bypass de aprovação por administradores está desabilitado.
4. O job remoto usa o GitHub Environment `production`, com revisores obrigatórios e prevenção de autoaprovação.
5. A verificação do Environment exige proteção de branch compatível com `main`, revisores e prevenção de autoaprovação, por leitura da API do GitHub. O bypass por administradores depende da revisão manual declarada acima: o endpoint GET Environment não retorna esse campo.
6. No servidor, o script exige gates locais de autorização, incluindo `deploy.enabled` e `source-parity.approved.json` válidos.
7. A instalação precisa estar preparada no diretório novo, com configuração externa, baseline anterior recuperável e autorização explícita para o corte.

As três variáveis de ativação não foram configuradas nesta atividade. Não criar gates para contornar uma falha de aprovação. Se a API não permitir verificar as proteções que ela expõe, corrigir o acesso ou a configuração: não remover a verificação para prosseguir. A declaração de revisão de bypass não é uma verificação automática nem elimina a necessidade de reconferir a interface quando as regras mudarem.

### GitHub Environment e segredos necessários

Configuração futura, a ser realizada somente após autorização:

- Criar Environment `production` e cadastrar revisores humanos responsáveis.
- Ativar prevenção de autoaprovação e usar exatamente uma regra customizada de branch, `main` (tipo branch, sem tags).
- Na interface de Settings → Environments → production, desabilitar o bypass de proteção por administradores e registrar a conferência humana. Somente depois dessa revisão e da autorização de ativação definir `PRODUCTION_ADMIN_BYPASS_REVIEWED=true`; não inferir esse estado pela resposta da API.
- Confirmar que regras de aprovação estão disponíveis no plano e na visibilidade do repositório e que a API usada pelo workflow consegue lê-las.
- Guardar os secrets abaixo no Environment, com chave exclusiva para o novo deploy e acesso restrito ao servidor correto.
- Manter a chave de host SSH obtida por canal confiável. Não executar `ssh-keyscan` no job como substituto da verificação de identidade.

| Tipo | Nome | Finalidade |
| --- | --- | --- |
| Secret | `PRODUCTION_SSH_HOST` | Host confirmado do Anderson |
| Secret | `PRODUCTION_SSH_USER` | Conta de publicação previamente autorizada |
| Secret | `PRODUCTION_SSH_PORT` | Porta SSH, normalmente 22 |
| Secret | `PRODUCTION_SSH_PRIVATE_KEY` | Chave privada exclusiva para autenticação |
| Secret | `PRODUCTION_SSH_KNOWN_HOSTS` | Entrada de host e chave pública conferida fora do workflow |
| Repository variable | `PRODUCTION_DEPLOY_ENABLED` | Gate global de ativação; ausente por padrão |
| Repository variable | `PRODUCTION_SOURCE_PARITY_CONFIRMED` | Confirmação revisada de equivalência da fonte; ausente por padrão |
| Repository variable | `PRODUCTION_ADMIN_BYPASS_REVIEWED` | Declaração de conferência manual do bypass de administradores desabilitado; ausente por padrão |
| Environment variable | `PRODUCTION_PUBLIC_HEALTH_URL` | URL HTTPS de `/health/release` em um dos três domínios auditados; obrigatório |
| Workflow env fixo | `DEPLOY_ROOT` | `/opt/startingweb/apps/anderson-multimarcas-unified`; outra raiz é recusada pelo entregador |

Nenhuma conexão SQL, chave R2, senha ou configuração de produção deve aparecer no YAML, no manifesto ou no pacote da aplicação. Configurações reais permanecem no servidor, com permissão restrita. Um usuário no grupo Docker tem controle amplo sobre o host; a restrição operacional da chave e a revisão de comandos continuam necessárias.

As três variáveis avaliadas em `jobs.production.if` precisam ser Repository variables, disponíveis antes de alocar o job. Secrets SSH ficam no Environment `production`. A URL pública deve ser, por exemplo, `https://andersonmultimarcas.com.br/health/release`, sem credenciais, query, fragmento, porta alternativa ou redirect; conferir qual domínio chega diretamente ao endpoint.

## Contrato do servidor

A raiz nova é distinta de `/opt/startingweb/apps/anderson-multimarcas`, que pertence à publicação legada. Sua estrutura é preparada somente durante uma etapa futura autorizada:

```text
/opt/startingweb/apps/anderson-multimarcas-unified/
  shared/
    runtime.env                       # configuração real; modo 0600
    server.json                       # contrato operacional sem segredos
    deploy.enabled                    # autorização local; modo 0600
    source-parity.approved.json        # aprovação da reconciliação; modo 0600
  releases/                           # pacotes e snapshots de cada release
  incoming/                           # destino de transferência, previamente preparado
  current.json                        # release atual e anterior
  transaction.json                    # journal somente durante a troca/recuperação
  history.jsonl                       # eventos de publicação e recuperação
  deploy.lock                         # lock exclusivo gerido pelo script
```

Consultar o schema e as validações de [release.py](../scripts/deploy/release.py) e os exemplos versionados antes de preparar a configuração. `server.json` declara `schemaVersion`, `containerName`, `network`, `dataProtectionVolume`, `uploads` e `legacyFallbackContainer`. A política de uploads suportada é `{ "type": "none", "reason": "justificativa revisada" }`, compatível com o R2 auditado; habilitar armazenamento local exige revisão do executor. O fallback deve identificar o container legado retido, no formato `andersonmultimarcas-legacy-<sufixo>`.

Exemplos sem credenciais: [server.example.json](../deploy/server.example.json), [runtime.env.example](../deploy/runtime.env.example) e [source-parity.approved.example.json](../deploy/source-parity.approved.example.json). Os placeholders de aprovação/baseline são deliberadamente inválidos para evitar ativação por cópia acidental; não há arquivo `deploy.enabled` versionado.

O arquivo `shared/deploy.enabled` exige o conteúdo `enabled`. A aprovação de paridade contém exatamente `reviewedUnifiedCommit`, `legacyImageId` e `reviewedAt`; registrar o SHA completo revisado, o ID da imagem legada preservada e a data da revisão. Essa aprovação não substitui o processo de revisão dos commits futuros. Não copiar valores de exemplo como se representassem produção. Não alterar o diretório antigo nem expor `runtime.env` em logs, artefatos ou saídas de diagnóstico. Raiz, `shared` e `releases` não podem ser graváveis por grupo/outros; snapshots e arquivos sensíveis são modo 0600.

O contrato precisa preservar:

- Rede externa `startingweb_network` e upstream do proxy `andersonmultimarcas-web:8080`.
- Volume `andersonmultimarcas-dataprotection-keys`, destino `/home/app/.aspnet/DataProtection-Keys` e `DataProtection__ApplicationName=AndersonMultiMarcas`.
- Conexão externa SQL Server para o banco Anderson, sem recriar o SQL Server compartilhado.
- Provider R2 e configuração de mídias; eventual volume de uploads só deve ser adicionado após inventário e validação de permissões.
- Identidade estável da aplicação, configuração de proxy e certificados válidos.
- `Database__ApplyMigrationsOnStartup=false`, `StorageMigration__Execute=false` e `LegacyImageImport__RecoverPendingJobsOnStartup=false`; `ASPNETCORE_ENVIRONMENT=Production` e, quando presente, `DOTNET_ENVIRONMENT=Production`. O executor fornece `Deployment__Commit` pelo manifesto, e recusa esse campo no env-file.

O env-file usa a sintaxe Docker `CHAVE=valor`, sem `export`, chaves duplicadas ou expansão de shell. Revisar aspas e interpolações do `.env` antigo: Compose e `docker run --env-file` não devem ser considerados parsers equivalentes. Os valores reais de conexão SQL/R2 devem ser preparados sem imprimi-los.

O script recusa assumir controle de container existente que não seja gerido pelo novo mecanismo. O primeiro corte é uma operação manual autorizada: preservar o container legado, interrompê-lo e liberar nome/alias de forma controlada. O script deve ter a referência desse legado retido para restaurá-lo se a primeira publicação falhar. Não remover o container antigo para resolver uma colisão de nomes.

Para releases subsequentes, a troca usa snapshots da aplicação e da configuração anterior. Existe um intervalo de indisponibilidade na troca de container com upstream estável; não se promete zero downtime. O lock do servidor impede publicações simultâneas do novo mecanismo. Concurrency do GitHub não coordena repositórios diferentes: o mecanismo antigo precisa de um plano de desativação autorizado antes do corte.

## Operação futura de publicação

Após autorização, reconciliação da fonte, correção dos riscos, backups e preparação do servidor:

1. Rever o SHA e o resultado integral da CI; revisar o SQL gerado separadamente.
2. Confirmar que a release é compatível com o banco atual sem migration automática.
3. Confirmar Environment, secrets, revisores, prevenção de autoaprovação, regra main, gates locais e retenção do baseline; conferir manualmente na interface que administradores não podem ignorar a aprovação.
4. Realizar o corte inicial conforme [MIGRACAO_REPOSITORIOS.md](./MIGRACAO_REPOSITORIOS.md), sem apagar o legado.
5. Habilitar as variáveis somente com a autorização explícita registrada. O job ainda exigirá aprovação do Environment.
6. Aprovar o SHA exato, acompanhar transferência, checksums, histórico e verificações.
7. Verificar a aplicação e seu commit esperado no container e por HTTPS nos domínios públicos. Se falhar, seguir [ROLLBACK_PRODUCAO.md](./ROLLBACK_PRODUCAO.md).

O comando remoto preparado para um pacote já transferido é:

```sh
python3 scripts/deploy/release.py deploy \
  --root /opt/startingweb/apps/anderson-multimarcas-unified \
  --bundle /caminho/revisado/do/pacote \
  --manifest-sha256 SHA256_DO_RELEASE_JSON
```

Esse exemplo não foi executado em produção. Usar o script correspondente à versão revisada do pacote e os caminhos efetivos gerados pelo workflow.

### Arquivos de implementação

| Arquivo | Responsabilidade |
| --- | --- |
| [ci.yml](../.github/workflows/ci.yml) | Integração, testes, auditoria NuGet, SQL e artefato da aplicação |
| [cd.yml](../.github/workflows/cd.yml) | Proveniência do CI, imagem no runner, pacote e job protegido |
| [Dockerfile](../deploy/Dockerfile) | Runtime oficial fixado por digest e configuração segura de startup |
| [application_manifest.py](../scripts/ci/application_manifest.py) | Manifesto e checksums da aplicação publicada |
| [package_release.py](../scripts/ci/package_release.py) | Exportação da imagem e manifesto da release |
| [check_vulnerabilities.py](../scripts/ci/check_vulnerabilities.py) | Gate por severidade, incluindo dependências transitivas |
| [verify_environment.py](../scripts/ci/verify_environment.py) | Validação por API de revisores, prevenção de autoaprovação e regra main; exige a declaração de revisão manual do bypass |
| [deliver.py](../scripts/ci/deliver.py) | SSH/SCP com host conhecido, verificação do executor e health público |
| [release.py](../scripts/deploy/release.py) | Locks, gates, snapshots, execução Docker, health, histórico e rollback |
| [release.sh](../scripts/deploy/release.sh) / [rollback.sh](../scripts/deploy/rollback.sh) | Entradas shell para o executor Python |

### Health e identificação da release

`/health` é uma verificação de liveness: a aplicação responde. Não comprova disponibilidade do SQL, permissões de negócio, R2 ou paridade com o sistema anterior.

O endpoint `GET /health/release` informa apenas o commit da release, sem dados de configuração. A publicação exige liveness e correspondência do commit com o manifesto. Isso evita o erro do script antigo, cujo `localhost:8080` acessava outro serviço do host. A validação deve alcançar o container correto, não uma porta arbitrária do host.

A aceitação operacional também exige HTTPS público e navegação dos fluxos relevantes. Uma falha de proxy, DNS ou certificado deve bloquear a conclusão; não promover uma release apenas porque o container responde internamente.

## Política de banco

A inicialização de produção não executa `Database.MigrateAsync`, seed ou reparos de schema. A implementação separa a política de startup: em Development mantém o comportamento automático anterior, que pode ser desabilitado por `Database:ApplyMigrationsOnStartup=false`; em qualquer outro ambiente fica desabilitado e configurar `true` lança erro antes de iniciar. Não habilitar esse comportamento em produção para resolver uma falha de startup.

Alteração de banco é uma atividade independente do deploy da aplicação:

1. Gerar SQL idempotente offline e preservar como artefato revisável.
2. Identificar `DROP`, alterações de tipo/nulabilidade/chaves, reconstruções de tabelas e atualizações de dados. Idempotência não torna uma operação segura.
3. Comparar o script com o schema e o histórico de migrations reais em ambiente autorizado e com acesso de leitura apropriado.
4. Criar backup recente do banco correto, validar a integridade e restaurar em instância isolada. Registrar horário, identificação, destino protegido e resultado da restauração, sem credenciais.
5. Validar compatibilidade da aplicação nova e da anterior com o schema resultante, preferindo mudanças aditivas e retirada em release posterior.
6. Solicitar autorização específica para a migration e sua janela, executar com identidade apropriada e verificar o resultado.

Nenhum script de deploy ou rollback executa SQL remoto. Voltar a imagem não reverte o banco. Restauração de backup ou correção de schema exige um plano próprio, avaliação de perda de dados e autorização explícita.

Riscos concretos da cadeia atual: a conversão Identity string→Guid altera colunas sob PK/FK sem removê-las/recriá-las; o reparo `RepairMissingAuditColumns` não é descoberto pelo EF; saneamentos atribuem defaults e a primeira loja/marca sem reversão; alguns Down apagam tabelas/histórico/contadores e outros são vazios. O diagnóstico anterior identificou dez arquivos de migration e somente nove descobertos. Esta implementação não corrige essas migrations nem aplica o SQL gerado. Backup e restauração isolada devem preceder qualquer autorização de alteração de schema. A documentação Microsoft recomenda revisão dos scripts e separar privilégios de atualização do schema dos usados em runtime: [Applying Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

## Validação e pendências

Validação executada no Mac em 05/11/2026, SDK 9.0.320/runtime 9.0.22 isolados:

| Verificação | Resultado |
| --- | --- |
| Restore da solução | Aprovado, incluindo Project.Tests |
| Build Release | Aprovado: zero erros, seis warnings NuGet de ImageSharp |
| Testes .NET | **61 aprovados, zero falhos, zero ignorados**: 51 anteriores e dez casos novos de segurança do startup/metadados/design time |
| Testes dos helpers de CI/entrega | **15 aprovados**, incluindo revisão de Environment, checksums, rejeição de identidade incorreta, resposta pública inválida e chamada de rollback |
| Testes unitários do executor | **30 aprovados**, incluindo locks, adulteração, journal, recuperação, configurações imutáveis, ausência de vazamento e retorno ao legado após primeira publicação |
| Docker real isolado | **Aprovado**: duas publicações, candidato com HTTP503 e recuperação automática, rollback por arquivo preservado e retorno ao legado; todos os recursos tinham prefixo/label exclusivo e foram removidos/conferidos |
| YAML e shell | actionlint1.7.12, ShellCheck0.11.0 e bash -n aprovados; binários locais verificados por checksum, sem instalação global |
| Geração EF | Aprovada: SQL idempotente com106326 bytes, sem conexão nem execução de migrations |
| Startup em Production com opt-in de migrations | Recusado antes de procurar conexão; nenhum banco acessado |
| Publicação .NET em diretório local | Aprovada; sem copiar arquivos ao servidor |
| Dockerfile e pacote reais da aplicação | Build linux/amd64 aprovado, usuário app; 264 arquivos publicados com checksums verificados e imagem exportada em arquivo de154841836 bytes; não iniciada a aplicação nem acessado SQL/R2 |
| Gate NuGet | **Reprovado corretamente**: nove advisories distintos, sete altos e dois moderados; não foi contornado |

Quatro advisories altos e dois moderados são do ImageSharp em runtime. Outros três altos são das versões8 do grafo privado de tooling de Core; o runtime web seleciona versões9, conforme a auditoria anterior. O gate é deliberadamente estrito e também inclui tooling. A CI completa do commit atual deve falhar nessa etapa até correção revisada; não haverá artefato de publicação nem CD de produção bem-sucedida nesse estado.

O smoke real usa uma aplicação HTTP descartável, não o Anderson com SQL/R2. Ele prova o ciclo Docker do executor em ambiente seguro, não paridade funcional, credenciais, proxy/NPM ou prontidão de produção. Comando reprodutível:

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py' -v
python3 -m unittest discover -s scripts/deploy/tests -p 'test_*.py' -v
python3 scripts/deploy/tests/smoke_docker.py \
  --base-image nginx@sha256:30f1c0d78e0ad60901648be663a710bdadf19e4c10ac6782c235200619158284
```

Logs/TRX/SQL/scan locais ficam na pasta ignorada `.cache/cicd-results`; os workflows publicarão seus próprios artefatos de evidência após uma execução futura no GitHub. Não houve execução real dos workflows no GitHub porque push e alterações remotas não foram autorizados. Environment e secrets não foram criados; a validação das regras foi testada com respostas simuladas, não com acesso autenticado ao novo Environment.

Nenhum teste usa o banco de produção. Não houve push, PR ou merge, nem deploy, migrations, reinicializações ou alterações de configuração em produção.
