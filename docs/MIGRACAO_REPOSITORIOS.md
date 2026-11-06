# Migração dos repositórios e do mecanismo de publicação

Data: 2026-11-05. Auditorias GitHub e SSH exclusivamente de leitura. **Os passos de alteração descritos neste documento são futuros e dependem de autorização explícita; não foram executados.**

## Repositórios encontrados

| Papel | URL e situação | Revisão observada |
| --- | --- | --- |
| Fonte antiga Code, agora repositório único | [StartingWeb/AndersonMultiMarcas](https://github.com/StartingWeb/AndersonMultiMarcas); o URL [AndersonMultiMarcas-Code](https://github.com/StartingWeb/AndersonMultiMarcas-Code) redireciona para ele | `main`: `162602aa97c58c53ffbec2aad5120219a20de214`, 2026-07-29 |
| Publicação legada | [StartingWeb/AndersonMultiMarcas-Release](https://github.com/StartingWeb/AndersonMultiMarcas-Release); público, não arquivado, workflow ativo | `main`: `fb8a205768dce239c960ffb1cfb043b2680de7b3`, 2026-08-13 |

A API identifica `StartingWeb` como conta GitHub: a listagem pública `/users/StartingWeb/repos` retornou esses dois repositórios, enquanto `/orgs/StartingWeb/repos` retornou 404. O GitHub CLI local estava sem autenticação; a auditoria usou Git e API pública, sem login ou pedido de credenciais. Não é possível excluir repositórios privados invisíveis sem autenticação.

O histórico do Code foi preservado pelo renomeio. A fonte `main` tem 24 commits; as branches adicionais `Release` (`8e0617afaaaf15e8022707bb445425c770f716e4`) e `homolog` (`7f8b5a94515dfe2643d9d5e72a1d5d502642c2ee`) são anteriores à main. Não foram retornadas tags. O Release contém 108 commits e somente a branch `main`.

Nenhum repositório antigo foi modificado, excluído ou arquivado.

## Arquitetura anterior

```text
Código C# / Razor no Code
          │ publicação local em pasta
          ▼
Release: DLLs, dependências e wwwroot versionados
          │ push em main
          ▼
GitHub Actions → SSH → deploy.sh no host
          │ fetch + reset hard de origin/main
          ▼
Docker build no host → compose up → health check antigo
          │
          ▼
Nginx Proxy Manager → andersonmultimarcas-web:8080
```

O perfil [FolderProfile.pubxml](../Project/Properties/PublishProfiles/FolderProfile.pubxml) apontava para `C:\Release\AndersonMultiMarcas-Release`, com `FileSystem`, configuração Release e `DeleteExistingFiles=false`. Esse é o acoplamento local explícito entre fonte e publicação. Não foi encontrado workflow automatizando a cópia da fonte para o Release; o perfil demonstra o destino de publicação, não a autoria ou execução de cada build.

O Release guarda binários compilados, PDBs, arquivos estáticos e infraestrutura Docker. O commit `7335c08` adicionou Dockerfile/Compose em 2026-07-28; `941884f` criou o workflow e `938fd8e` ajustou SSH. Arquivos exclusivos relevantes: `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example`, `.github/DEPLOY.md` e `.github/workflows/deploy.yml`.

O [workflow legado](https://github.com/StartingWeb/AndersonMultiMarcas-Release/blob/main/.github/workflows/deploy.yml) responde a push em `main`, usa `appleboy/ssh-action@v1.2.5` e executa `./deploy.sh` em `/opt/startingweb/apps/anderson-multimarcas`. Referencia secrets `SERVER_HOST`, `SERVER_USER`, `SERVER_SSH_KEY` e `SERVER_SSH_FINGERPRINT`. Nenhum valor foi consultado. Não usa Environment com aprovação, build, testes ou rollback. Apenas imprime o SHA; o script escolhe a main corrente.

O script remoto não é rastreado no Git. Ele executa fetch, reset hard para `origin/main`, `docker compose up -d --build`, espera 15 segundos, faz curl de `http://localhost:8080/` e limpa imagens. Esse curl verifica uma Evolution API de outro projeto: o Anderson não publica porta 8080 no host. O healthcheck interno do container Anderson é distinto e estava saudável.

A execução GitHub mais recente observada foi [31761697263](https://github.com/StartingWeb/AndersonMultiMarcas-Release/actions/runs/31761697263), concluída com sucesso em 2026-08-14T01:48:10Z, para `fb8a205...`. O mecanismo antigo permanece ativo; não se deve fazer push no Release como parte desta migração.

## Divergência histórica e recuperação local

Há **26 commits no Release após o último commit da fonte**. Isso não equivale a 26 funcionalidades independentes: o inventário de blobs, assemblies, estáticos e classificação funcional está em [RECUPERACAO_RELEASE.md](./RECUPERACAO_RELEASE.md). São mudanças reais de blobs de DLL e alguns estáticos, sem commits correspondentes de fonte identificados:

| Grupo | Commits de publicação de referência |
| --- | --- |
| Performance, catálogo e revisão de capas | `655107c`, `a933040`, `1c7e5f0`, `b41a469`, `40242c8` |
| Páginas de lojas e dados legados | `bc167e9`, `3b1e240`, `43bf98d`, `63bb316`, `71911d2` |
| CNPJ opcional e validação | `77ddc2c`, `bb0c9d7`, `78554bb`, `5b21820`, `5f6d5a5`, `8de1fab` |
| Cadastro, abertura, salvamento e edição de lojas | `203e2d5`, `4a7b528`, `8e9dfdb`, `2613d45`, `f361135` |
| Compatibilidade de schema | `22358fb` |
| Upload e diagnóstico R2 | `bd00517`, `04a0d3e`, `e1d9953` |
| Tag 0 km para seminovos | `fb8a205` |

As DLLs mantêm `1.0.0+162602aa97c58c53ffbec2aad5120219a20de214` na versão informacional apesar das substituições posteriores. É compatível com builds de uma árvore local modificada sem commit correspondente; essa é uma inferência do histórico. O SHA embutido não prova paridade.

`wwwroot/css/components.css` diverge entre fonte e Release. `wwwroot/img/home-hero-poster.jpg` só existe no Release. `Project.styles.css` só existe no Release e é normalmente gerado no build. Quatro uploads antigos também estão versionados somente no Release e são excluídos da imagem pelo `.dockerignore` legado. Não sobrepor esses arquivos a mídias vivas automaticamente.

A recuperação local preserva os commits `bcabccd` e `cabe741` na branch `feat/unified-cicd-deployment`, reaproveita o executor e atualiza SDK/runtime para .NET 10 LTS. Evidências dos fluxos recuperados ficam em [RECUPERACAO_RELEASE.md](./RECUPERACAO_RELEASE.md) e [VALIDACAO_FUNCIONAL.md](./VALIDACAO_FUNCIONAL.md). Antes de ativar, concluir a revisão humana da matriz e registrar a aprovação para o SHA exato que será publicado. Compilação e semelhança entre métodos não comprovam equivalência funcional.

## Servidor realmente identificado

A identidade foi confirmada correlacionando os domínios no proxy, checkout Release, hashes de DLL do host e do container e imagem em execução. Não foi presumido que seria o mesmo host de outros projetos.

| Item | Estado observado |
| --- | --- |
| Acesso local existente | Alias SSH `startingweb-server`, host `187.77.35.76`, usuário `pedro`; BatchMode e StrictHostKeyChecking ativos |
| Sistema | Ubuntu 24.04.4 LTS, Linux 6.8.0-147-generic, x86_64 |
| Runtime operacional | Docker Engine 29.6.2, Compose 5.3.1; docker.service ativo |
| Aplicação | Container `andersonmultimarcas-web`, comando `dotnet Project.dll`, usuário app UID/GID 1654:1654 |
| Runtime .NET no container | .NET e ASP.NET 9.0.19; ambiente Production |
| Imagem legada | `startingweb/andersonmultimarcas-release:latest` |
| Digest/imagem atual | `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5` |
| Revisão Release do host | `fb8a205768dce239c960ffb1cfb043b2680de7b3` |
| Diretório legado | `/opt/startingweb/apps/anderson-multimarcas` |
| Rede Docker | `startingweb_network`; aplicação só expõe porta interna 8080 |
| Proxy | `nginx-proxy-manager`, `jc21/nginx-proxy-manager:latest`; portas host 80, 81 e 443 |
| SQL Server | Container compartilhado `sqlserver`, SQL Server 2022; banco `AndersonMultiMarcas` em `sqlserver,1433` |

O host não oferece `dotnet` ou `nginx` no PATH; ambos os serviços relevantes são operados em containers. O container da aplicação estava healthy, failing streak zero. A auditoria inspecionou os registros de health existentes e não executou uma publicação.

### Domínios, proxy e certificados

- `anderson.startingweb.com.br`: proxy host 4, certificado público `npm-9`, validade observada até 2026-12-25T04:12:14Z.
- `andersonmultimarcas.com.br` e `www.andersonmultimarcas.com.br`: proxy host 5, certificado público `npm-11`, validade observada até 2026-12-26T08:12:36Z.
- Upstream dos dois hosts: HTTP para `andersonmultimarcas-web:8080`, com HTTPS forçado no frontend.
- Configurações do proxy no host: `/opt/startingweb/compose/nginx-proxy-manager/data/nginx/proxy_host/4.conf` e `5.conf`; dados e certificados em diretórios próprios de NPM.

Somente certificados públicos foram inspecionados, nunca chaves privadas. DNS e HTTPS externos não foram revalidados neste subtrabalho. Antes da aceitação do novo fluxo, verificar os três domínios externamente e a renovação de certificados, sem mudar o proxy desnecessariamente.

### Persistência e segurança operacional

- O volume `andersonmultimarcas-dataprotection-keys` é o único mount atual da aplicação, em `/home/app/.aspnet/DataProtection-Keys`. Preservar volume, permissões e `DataProtection__ApplicationName=AndersonMultiMarcas`.
- O provider de mídia é R2 e dual read está ativo. Não foram acessados objetos R2 nem suas credenciais.
- O checkout legado contém quatro uploads, total 295486 bytes; `/app/wwwroot/uploads` não existe no container atual. Não há volume de uploads na instalação observada. Inventariar o uso desses arquivos antes de qualquer reorganização.
- O ambiente fica em `.env` no diretório legado, modo observado 0664. Valores não foram emitidos. Uma cópia protegida para o novo fluxo deverá usar modo 0600, mediante autorização.
- A conexão SQL observada usa `Encrypt=False` e `TrustServerCertificate=True`; revisar separadamente criptografia e confiança, sem mudar o contrato durante este trabalho.
- O SQL Server usa bind persistente `/opt/startingweb/data/sqlserver` e publica 1433 no host em IPv4/IPv6. Não foi comprovado o estado do firewall externo. Não recriar ou reiniciar esse container compartilhado.

### Automações e backups

Não foram encontrados units legíveis de systemd, processos de runner/webhook ou crontab do usuário responsáveis pelo deploy Anderson. O crontab de root e alguns diretórios protegidos não puderam ser auditados sem sudo; não houve contorno. Portainer e webhooks externos não foram exaustivamente inspecionados. Ausência de automação nesses locais não foi comprovada.

Existe limpeza diária de imagens Docker às 01:50 e semanal de builder cache. Avaliar a política antes de confiar em imagens locais para rollback. A configuração desses jobs não foi alterada.

Um backup isolado Anderson foi encontrado em `/opt/startingweb/backups/db_AndersonMultiMarcas_28_07_2026_01_17_00.bak`, 33907200 bytes, de julho. Seu conteúdo e restauração não foram validados. O tar diário mais recente de 2026-11-05 contém banco StartingConnect, PostgreSQL Evolution e configurações de outros serviços; não comprova cobertura atual do Anderson, seu diretório apps ou Data Protection.

Uma amostra de logs da aplicação foi resumida em memória sem expor linhas de requisição, SQL ou parâmetros. Não houve marcadores de erro na amostra; isso não comprova ausência de erros históricos ou prontidão funcional.

## Nova arquitetura e requisitos locais

A implementação foi adaptada para SDK .NET 10, dependências travadas, SQL Server descartável em CI, imagem oficial ASP.NET 10 Ubuntu 24.04 e ensaios Docker com falhas controladas. O pipeline novo está descrito em [CI_CD_DEPLOYMENT.md](./CI_CD_DEPLOYMENT.md): imagem construída no runner, pacote por SHA, SSH com host conhecido, configuração externa, lock, health com identidade de commit e rollback da aplicação. Docker é mantido para aproveitar o runtime, a rede e o proxy existentes.

A raiz proposta `/opt/startingweb/apps/anderson-multimarcas-unified` é separada do legado. Nenhum diretório foi criado no servidor. Não se muda o origin do checkout Release nem se repõe sua pasta por uma cópia da fonte.

O script recusa controlar um container legado não gerido. A primeira ativação requer um corte manual e autorizado do nome/alias `andersonmultimarcas-web`, preservando o container anterior para retorno. A publicação nova só pode ser aceita depois de validar a recuperação desse baseline.

## Procedimento de transição futuro

### 1. Aprovação e preparação fora de produção

1. Revisar a branch local e os commits; publicar branch e PR somente com autorização específica.
2. Revisar a classificação das diferenças dos 26 commits do Release e as correções de autorização/dependências, com as evidências da recuperação local.
3. Confirmar CI, testes funcionais, SQL revisável e pacote vinculado ao SHA correto em ambiente isolado.
4. Conferir os ensaios locais já realizados de deploy, falhas de startup/health, imagem inválida, adulteração, transferência/configuração e rollback manual/legado; repetir se o executor mudar. Não usar SQL ou R2 de produção.
5. Definir responsáveis, janela, critérios de aceitação, retenção e ponto de interrupção.

### 2. Backups e baseline antes de alterar qualquer serviço

Com autorização específica, preservar em destino protegido:

- Checkout Release no SHA identificado, incluindo `deploy.sh` não rastreado, Compose e metadados da imagem/container.
- Imagem atual por digest e arquivo offline `docker save`, com checksum conferido e teste de carga em ambiente isolado. A política de prune não deve destruir o único baseline.
- `.env` e demais configurações, sem imprimir conteúdo e com permissões restritas.
- Volume Data Protection, preservando proprietário e permissões; confirmar recuperação sem divulgar material criptográfico.
- Inventário e cópia dos arquivos locais de mídia relevantes; proteção/backup de R2 compatível com as capacidades do serviço e permissões disponíveis. Não alterar ou remover objetos.
- Configurações do NPM e certificados necessários à recuperação, sem registrar chaves privadas em documentos ou logs.
- Backup recente do banco **AndersonMultiMarcas**, identificação do arquivo, checksum, verificação e restauração efetiva em SQL Server isolado. Confirmar que o backup é do banco correto e que a conta autorizada consegue restaurá-lo.

Parar se qualquer baseline essencial não for recuperável. Um arquivo `.bak` antigo ou sucesso do backup de outro banco não satisfaz o requisito.

### 3. Preparar o novo destino sem substituir o legado

1. Criar a raiz separada e conta/permissões adequadas.
2. Preparar `shared/runtime.env` em modo 0600 e o contrato `shared/server.json`, usando os exemplos e o schema do script. Não gravar segredos no Git.
3. Reutilizar a rede existente e o volume DP; verificar UID/GID e acesso. Não recriar recursos de outros projetos.
4. Instalar apenas o executor revisado e os pacotes aprovados; conferir checksums e proteção de caminhos.
5. Configurar Environment `production`, revisores, prevenção de autoaprovação, branch main e chave SSH exclusiva. Conferir na interface do GitHub que o bypass de proteção por administradores está desabilitado; a API GET Environment não expõe esse estado. A Repository variable `PRODUCTION_ADMIN_BYPASS_REVIEWED=true` declara essa revisão humana, sem substituí-la por uma verificação automática. Manter essa variável, `PRODUCTION_DEPLOY_ENABLED`, `PRODUCTION_SOURCE_PARITY_CONFIRMED` e os gates locais desligados até a autorização final.
6. Planejar a liberação do nome/alias legado e registrar o identificador do container retido para rollback inicial.

### 4. Desativar mecanismos conflitantes e realizar o corte

Somente após aprovação explícita da ativação:

Conferir novamente as regras do Environment na interface, incluindo bypass por administradores desabilitado, e registrar a revisão antes de habilitar as três variáveis de ativação. Alterações posteriores nessas regras exigem nova conferência humana; a API valida somente os campos disponíveis de revisores, prevenção de autoaprovação e branches.

1. Suspender o workflow de deploy do Release e quaisquer webhooks, cron jobs ou operadores que possam executá-lo. Registrar o estado anterior e como reativá-lo. Isso não autoriza excluir ou arquivar o repositório.
2. Verificar que não há deploy antigo em andamento. O grupo de concurrency do novo repositório não trava o antigo.
3. Interromper o container antigo em janela controlada, preservando imagem, configuração e volumes; renomeá-lo/reter a referência conforme o contrato do executor. Não remover seu baseline.
4. Liberar o nome/alias e publicar o pacote autorizado da fonte única.
5. Conferir `/health`, `/health/release`, SHA esperado, logs sanitizados, proxy e HTTPS público.
6. Validar catálogo, detalhes, login/administração e acesso a mídias em escopo aprovado, sem criar dados de teste em produção por conta própria.
7. Em falha, executar o retorno previsto em [ROLLBACK_PRODUCAO.md](./ROLLBACK_PRODUCAO.md), sem migrations ou restauração de banco automática.

O primeiro corte pode causar breve indisponibilidade. Não prometer troca atômica de processo nem zero downtime apenas pela existência de pacotes imutáveis.

### 5. Encerramento do legado após estabilização

Depois do período de observação acordado e de autorização específica:

1. Confirmar que deploys novos usam exclusivamente artefatos do repositório único e que o retorno foi testado.
2. Confirmar ausência de referências operacionais ao Release em scripts, agendamentos e configurações externas.
3. Revogar/rotacionar credenciais exclusivas do mecanismo antigo, preservando as necessárias a outros serviços.
4. Manter o histórico e os backups conforme retenção definida; remover recursos operacionais somente com aprovação.
5. Opcionalmente documentar o encerramento e arquivar o Release. Não excluir o histórico para resolver a migração.
6. Manter o redirecionamento do antigo Code quando possível; revisar clones externos e documentação para usar o URL único.

## Estado desta entrega

Produção permaneceu inalterada. Não foram executados backups novos, conexão SQL/R2, migrations, reinicializações, alterações de proxy, desativação de automações, criação de Environment/secrets, push, PR ou merge. A auditoria remota tem limites explícitos; o plano não trata ausência de acesso como prova de ausência de configuração.
