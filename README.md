# AndersonMultiMarcas

Sistema de catálogo e gestão de uma revenda de veículos: site público, filtros, detalhes, contato/WhatsApp e administração de veículos, lojas, marcas, vendedores, usuários, perfis e importação de imagens.

Última atualização: **07/11/2026**. Preparação nas branches `infra/production-preparation` (PR #2) e `infra/schema-compatibility-review` (PR #3). A aplicação publicada e o mecanismo legado permanecem preservados; a identidade SQL dedicada foi instalada apenas na configuração do destino futuro. Recuperação técnica referenciada no Release `fb8a205768dce239c960ffb1cfb043b2680de7b3`; código descompilado foi tratado como evidência, não como fonte original.

Banco produtivo corrigido em 07/11: A/B/C aplicadas, Marca 4 **Jaguar (cadastro adicional)**, LogoUrl NULL e índice único. Backup restaurado, DBCC e compatibilidade antigo/novo aprovados; não repetir migrations. A operação temporária com [backup exclusivamente local foi aceita](docs/ACEITE_RISCO_BACKUP_LOCAL.md). Publicação depende da política GitHub individual aprovada e aplicada, homologação R2, decisão sobre as mídias ausentes e paridade. Nesta rodada, **não executar merge, deploy ou ativação de gates**. [Estado e próximas ações](docs/PENDENCIAS_PRODUCAO.md), [recibo SQL](docs/evidence/PRODUCTION_SQL_2026-11-07.json).

## Tecnologias e requisitos

- .NET **10 LTS**, SDK **10.0.402**, ASP.NET Core e EF Core/Identity **10.0.13**.
- SQL Server 2022, Razor Pages, MediatR 11, FluentValidation 11.
- Armazenamento local ou Cloudflare R2/S3; Magick.NET 14.17.2 para JPEG/PNG/WebP.
- Docker Desktop no Mac; em Apple Silicon o SQL Server usa emulação `linux/amd64`, exclusivamente para desenvolvimento. Produção usa Linux x64.
- Python 3 para CI/CD e ensaios; ShellCheck para os wrappers Bash. Playwright é opcional para navegação local.

As versões NuGet estão fixadas em lockfiles. O restore audita dependências transitivas e falha com qualquer advisory, sem supressões. ImageSharp foi substituído porque a versão 3.1.12 continuava vulnerável e as versões posteriores exigiram licença indisponível nesta sessão.

## Executar no Mac

Na worktree desta branch:

```sh
dotnet tool restore
dotnet restore Project/AndersonMultiMarcas.slnx --locked-mode
dotnet build Project/AndersonMultiMarcas.slnx --configuration Release --no-restore
python3 scripts/dev/local_sql.py start
python3 scripts/dev/local_sql.py run
```

Abra `http://127.0.0.1:5185`. O auxiliar cria **seu próprio container** `anderson-local-sql` com porta dinâmica no loopback e senha aleatória. A conexão e a senha de login local ficam em `.cache/local-sql/config.json`, com permissão 600, ignorado pelo Git. Usuário: `local.developer`; e-mail: `developer@example.invalid`. Nunca reutilize estes dados fora do ambiente descartável.

O bootstrap de desenvolvimento é opt-in por configuração e só cria uma conta ausente; nunca redefine usuários existentes. A execução local habilita migrations somente nesse banco descartável. Para cadastrar manualmente uma conexão externa, configure `ConnectionStrings__DefaultConnectionDev`, `Database__ApplyMigrationsOnStartup=false` e `Storage__Provider=Local`; não versione segredos. A conexão de produção chama-se `DefaultConnectionProd`.

Depois de iniciar a aplicação e criar o schema, em outro terminal:

```sh
python3 scripts/dev/local_sql.py seed
python3 scripts/dev/local_sql.py test
```

O seed contém exclusivamente loja, vendedor e 42 veículos fictícios, incluindo vendido/inativo para testar exclusão do catálogo. O comando `stop` remove somente o container descartável identificado pelo auxiliar e seus arquivos locais de conexão:

```sh
python3 scripts/dev/local_sql.py stop
```

Interrompa a aplicação com Ctrl+C antes de remover o banco.

## Testes e build

```sh
dotnet test Project.Tests/Project.Tests.csproj --configuration Release
python3 -m unittest discover -s scripts/tests -p 'test_*.py' -v
python3 -m unittest discover -s scripts/deploy/tests -p 'test_*.py' -v
python3 scripts/ci/validate_workflows.py
shellcheck scripts/deploy/release.sh scripts/deploy/rollback.sh
dotnet publish Project/Project.csproj --configuration Release --output artifacts/publish
```

Os testes de SQL usam `ANDERSON_TEST_SQL` e criam bancos `AndersonTest_*` exclusivos, removidos ao final; sem essa variável aparecem explicitamente como ignorados. `local_sql.py test` e o CI fornecem uma instância dedicada e executam esses testes. Os testes HTTP exercitam cookies Identity, autorização e antiforgery reais.

Para navegar com Chromium headless, mantenha a aplicação e o seed local em execução:

```sh
python3 -m venv .cache/browser-venv
.cache/browser-venv/bin/pip install playwright==1.60.0
.cache/browser-venv/bin/python -m playwright install chromium
python3 scripts/dev/local_sql.py browser
```

O teste aceita apenas loopback, usa credenciais locais e grava relatório e screenshots em `.cache/validation`. Consulte [validação funcional](docs/VALIDACAO_FUNCIONAL.md) para resultados e limites comprovados.

## Banco e SQL revisável

As migrations recuperadas mantêm os identificadores do Release. A correção de bootstrap Identity valida GUIDs antes de alterar constraints e preserva os vínculos; chaves inválidas interrompem a operação. Um snapshot atual alinha o modelo sem repetir alterações já presentes no Release. Consulte [auditoria do banco](docs/BANCO_DE_DADOS.md).

```sh
dotnet ef migrations script --idempotent --project Data/Data.csproj --startup-project Project/Project.csproj --configuration Release --output artifacts/review-required.sql
```

**Não execute esse SQL em produção sem revisão, backup e autorização específicos.** A aplicação de produção, o deploy e o rollback não executam migrations. `Database__ApplyMigrationsOnStartup=true` é recusado em produção.

## CI/CD, publicação e rollback

O [CI](.github/workflows/ci.yml) compila, testa .NET/Python/SQL, audita vulnerabilidades, gera SQL revisável e manifest, constrói a imagem Linux x64 e valida inicialização e mídia nativa. Os ensaios Docker verificam deploy/rollback e falhas de integridade, configuração e saúde.

O [CD](.github/workflows/cd.yml) prepara um bundle imutável após CI aprovado na `main`; a publicação continua **desabilitada**. A imagem final reconstruída no CD deve passar pelo ensaio offline sem rede, e o empacotador confere o ID da imagem e o relatório aprovado antes de exportá-la. Uma futura ativação exige variáveis explícitas, paridade aprovada, Environment `production` protegido, revisão de bypass administrativo, SSH com host key fixada e configuração local no servidor. As PRs #2/#3 permanecem sem merge.

A entrega verifica checksums e identidade do commit, faz health check e retorna à imagem/container anterior se falhar. O rollback manual reutiliza o artefato aprovado; não reverte banco automaticamente.

- [Operação do CI/CD](docs/CI_CD_DEPLOYMENT.md)
- [Migração entre repositórios](docs/MIGRACAO_REPOSITORIOS.md)
- [Rollback](docs/ROLLBACK_PRODUCAO.md)
- [Matriz de recuperação do Release](docs/RECUPERACAO_RELEASE.md)
- [Arquitetura atual](docs/ARQUITETURA_ATUAL.md)
- [Segurança e regressões](docs/SEGURANCA_E_REGRESSOES.md)
- [Pendências para produção](docs/PENDENCIAS_PRODUCAO.md)

## Estado operacional — 07/11/2026

A aplicação pública ainda usa o container legado. O destino unificado está preparado, com deploy desabilitado e PRs de infraestrutura/schema em revisão. `anderson_runtime` foi instalado com permissões limitadas para o ambiente futuro, sem reiniciar o SQL compartilhado nem alterar a conexão do legado. As migrations comerciais A/B/C foram aplicadas em 07/11, incluindo o rótulo aprovado do ID 4 e LogoUrl NULL. O corte continua bloqueado pelas dependências externas registradas.

O backup diário de 07/11 às 03h foi concluído e verificado, com notificação aceita pelo SMTP; a próxima execução é 08/11 às 03h, America/Sao_Paulo. A retenção permanece 7 diários/4 semanais/3 mensais, com monitor ativo e nenhuma cópia excluída nesta auditoria. Backup local não oferece recuperação após perda total da VPS. O aceite dessa limitação está registrado sem prazo arbitrário de revisão.

A [política individual proposta](docs/GITHUB_POLITICA_REPOSITORIO_INDIVIDUAL.md) mantém PR e CI obrigatórios, admins sujeitos às regras, conversas resolvidas e proibição de force push/exclusão; só os requisitos de aprovação humana incompatíveis com mantenedor único dependem de alteração autorizada. A proteção atual ainda exige outro revisor. [R2](docs/VALIDACAO_R2.md) e [recuperação de mídias](docs/RECUPERACAO_MIDIAS_R2.md) têm diagnósticos separados: acesso de leitura não comprova escrita, e objetos ausentes precisam de decisão própria. Nenhum destino Cloudflare foi criado.

Referências: [pendências atuais](docs/PENDENCIAS_PRODUCAO.md), [identidade SQL](docs/USUARIO_SQL_RUNTIME.md), [primeiro corte](docs/ATIVACAO_PRODUCAO.md), [CI/CD](docs/CI_CD_DEPLOYMENT.md), [rollback](docs/ROLLBACK_PRODUCAO.md), [backup](docs/BACKUP_ANDERSONMULTIMARCAS.md) e [R2](docs/VALIDACAO_R2.md). As versões de migration e suas evidências permanecem na [PR #3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3); a infraestrutura/runtime na [PR #2](https://github.com/StartingWeb/AndersonMultiMarcas/pull/2).
