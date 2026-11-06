# Validação funcional e técnica

**05/11/2026 — execução local no MacBook Air, branch `feat/unified-cicd-deployment`.**

Resultado consolidado: **184 testes .NET aprovados, 55 testes Python aprovados, 56 verificações de navegação aprovadas**, sem falhas ou testes ignorados na execução final. Build Release: **zero warnings e zero erros**. Auditoria NuGet direta/transitiva: **zero advisories**. Não se utilizaram banco, uploads ou identidades reais como fixture.

A [matriz de recuperação](RECUPERACAO_RELEASE.md) contabiliza 22 grupos: 15 recuperados, 3 equivalentes e 4 desnecessários/substituídos; nenhum item de implementação recuperável permaneceu aberto. Isso não certifica configuração/permissões reais de um bucket R2 nem substitui ensaio com backup sanitizado de produção.

## Ambiente e evidências

- SDK .NET 10.0.402; ASP.NET Core/EF/Identity 10.0.13; SQL Server 2022 em container `linux/amd64` emulado no Mac ARM.
- Container SQL exclusivo desta atividade, senha aleatória, porta dinâmica no loopback. Cada teste SQL criou/removeu seu banco; o navegador utilizou um banco sintético separado.
- Aplicação Kestrel real em `127.0.0.1:5185`, storage local e recuperação automática de jobs desativada. Login efetuado com Identity/SQL reais e conta sintética opt-in.
- Chromium headless/Playwright 1.60.0, viewports de 1440, 768 e 390 pixels. Screenshots públicas e administrativas foram inspecionadas visualmente; isso não implica verificar o aplicativo em primeiro plano no macOS.
- Os resultados brutos e arquivos grandes permanecem ignorados pelo Git em `.cache/validation` e `.cache/cicd-results`; não contêm configuração produtiva.

| Verificação | Resultado e evidência |
|---|---|
| Restore completo | `--force --no-cache --locked-mode`, seis projetos, sem advisory/supressão; [log](../.cache/validation/restore-final.log) |
| Build Release | 0 warnings / 0 erros; [log](../.cache/validation/build-final.log) |
| Todos os testes .NET | 184 aprovados / 0 falhos / 0 ignorados; [TRX](../.cache/validation/final.trx), [log](../.cache/validation/tests-final.log) |
| Segurança | 31 casos da suíte Security; autenticação, perfis/claims, CSRF, bootstrap/reset, uploads e cabeçalho AJAX de capa; parte dos 184 |
| Banco isolado | 20 cenários SQL reais, incluindo 10 da recuperação, sem fallback para InMemory; parte dos 184 |
| Recuperação | 48 casos específicos, incluindo 10 SQL; parte dos 184 |
| Imagens | Decodificação JPEG/PNG/WebP, orientação EXIF, remoção de metadados, limites e compensação de lotes; parte dos 184 |
| Python | 23 testes CI/helpers/encoding + 32 executor/deploy = 55 aprovados; [CI](../.cache/validation/python-ci-final.log), [deploy](../.cache/validation/python-deploy-final.log) |
| Navegação | 56 verificações; zero erros JavaScript; [relatório](../.cache/validation/browser-results.json) |
| Vulnerabilidades | 0 advisories diretos/transitivos; [JSON NuGet](../.cache/validation/vulnerabilities-final.json) |
| Schema/modelo | `has-pending-model-changes`: nenhuma divergência; supressão de warning removida |
| SQL revisável | Geração sem conexão; [script idempotente](../.cache/validation/review-required-final.sql), criado/reexecutado duas vezes em banco vazio por teste SQL |
| YAML e scripts | Validador local, actionlint, ShellCheck e `bash -n` aprovados |
| Docker deploy/rollback | 9 cenários aprovados; [relatório](../.cache/cicd-results/deploy-smoke.json) |
| Imagem real Linux | Startup Production sem rede/DB, health de commit, estático, WebP nativo e recusa de migrations automáticas; cinco verificações aprovadas |
| Segredos | Gitleaks 8.30.1 no diff e em 5,47 MB de fontes versionáveis finais: zero achados, sem supressão; `.cache` deliberadamente excluída |
| Estáticos e proveniência | Produção/Release: 4/4 DLLs e 188/188 estáticos sem uploads com SHA-256 idêntico; [matriz](RECUPERACAO_RELEASE.md) |
| Git | Verificação de espaços, arquivos novos e commits locais; trabalho anterior preservado; nenhum push ou merge |

As quantidades de segurança/SQL/recuperação são subconjuntos dos 184 casos .NET; não se somam novamente ao total. Casos de falha de deploy são aprovados quando rejeitam a entrada ou restauram o container esperado.

## Site público

| Fluxo | Evidência e alcance |
|---|---|
| Home, catálogo e landing pages | HTTP 200, conteúdo sintético renderizado; zero km/seminovo por flag, vendido/inativo excluídos; paginação sem duplicação em testes de 42 veículos |
| Ordenação/filtros | Preço asc/desc, empates por ID, mínimo/máximo/faixa e paginação executados contra SQL Server; navegador percorre as ordenações e faixa |
| Detalhe | HTTP 200 para ativo; vendido/inativo 404; galeria e metadados renderizados; preço salvo conferido no navegador |
| Imagens | Upload PNG real convertido para WebP, persistência após recarregar, bytes HTTP e `naturalWidth > 0` da imagem específica na página pública; placeholder sem arquivo confirmado nos helpers |
| Empresa/contato | Lojas e vendedores sintéticos, tolerância a NULL legado e exclusão de loja inativa; nenhuma chamada de mensagem enviada |
| WhatsApp | Link/configuração de contato presentes; não se iniciou conversa nem se enviou mensagem externa |
| SEO | JSON-LD parseado no navegador, propriedades `@context`/`@type` testadas; sitemap/robots/redirects no código e testes HTTP pertinentes |
| Responsividade | Home, catálogo, detalhe e contato em 390/768 px sem overflow horizontal; screenshots inspecionadas. Não é auditoria completa de acessibilidade ou de todos os aparelhos |

## Administração

| Fluxo | Evidência e alcance |
|---|---|
| Login/logout/sessão | Cookie Identity com SQL real no navegador; logout POST remove acesso; usuário anônimo redirecionado ao login |
| Perfis e permissões | HTTP com usuários anônimos, autenticados sem perfil/claim e autorizados; API 401/403, CSRF ausente/incorreto 400; consultas públicas permanecem acessíveis |
| Usuários/perfis | Páginas autenticadas abertas no navegador; criação/reset/expiração/single-use e seed sem alterar conta existente cobertos por testes de segurança |
| Veículos | Cadastro zero km, dados e preço persistidos, upload e exibição pública conferidos no navegador; edição comercial, omissões e limpeza explícita em SQL/regressões |
| Lojas | Criar/editar sem CNPJ, recarregar e confirmar persistência no navegador; máscara, dígitos verificadores, duplicidade, limpeza e falha SQL testados |
| Marcas/vendedores | Criação real pelo formulário e conferência nas listagens; exclusão de vendedores de loja inativa coberta por três regressões |
| Capas | Página, filtros, navegação e transação SQL; salvamento AJAX com cookie/token reais, HTTP 200, além do teste HTTP que rejeita o cabeçalho CSRF antigo |
| Importações/R2 | Páginas abertas após login; retry/relatórios/locks/URL guard e requisições S3 testados com fontes controladas. Nenhuma importação real de clientes ou escrita em bucket produtivo |
| Vendas/relatórios | Páginas de vendas e relatórios de importação acessíveis; serviços de relatório/CSV e estado de venda cobertos nos testes existentes e regressões. Não foi simulada venda de cliente real |

## Falhas encontradas e resolvidas

As primeiras execuções reproduziram problemas reais: escrita anônima, senhas fixas, advisories, preço não traduzível, perda de campos comerciais, retry sem orçamento novo, JSON-LD inválido, erro de chave Identity/constraints, GUID truncado pelo SQL Server, índice de CNPJ bloqueando DDL repetido, drift com erro SQL 207, duas capas em upload, exclusão prematura de blob, cabeçalho CSRF incorreto, texto CP1252 e paths Unix tratados como URI de arquivo. As regressões foram acrescentadas e reexecutadas com sucesso.

O harness de navegação também foi corrigido: aguardar readiness após reinício, ignorar o `<li>` vazio/oculto do resumo de validação e a imagem vazia do lightbox oculto, conferir o arquivo efetivamente enviado e gravar relatório de falha para impedir sucesso antigo. Nenhuma dessas falhas foi ocultada por supressão de segurança ou por desativar testes.

## CI/CD e ensaios

Nove cenários Docker locais: deploy saudável; falha de health com rollback automático; falha de startup com rollback; imagem inválida; artefato adulterado; configuração insegura/migrations; transferência interrompida; rollback manual do arquivo preservado; retorno ao mesmo container legado simulado. Containers/redes/volumes/tags temporários desses ensaios foram removidos e a ausência conferida.

A interrupção de SCP foi simulada no transporte controlado e nunca usou SSH real para entrega. A imagem real foi construída como Linux x64 e executada como usuário `app`, com rede desabilitada no teste de startup. O decoder nativo produziu WebP válido. O empacotamento foi verificado por manifest, checksums, arquitetura, rótulo de revisão e image ID. Os testes locais não equivalem a uma execução remota do GitHub Actions.

O CI mantém gates de build/testes/SQL/vulnerabilidades/integridade ativos. O CD exige habilitação explícita, paridade aprovada e Environment com aprovação. Não foi habilitado deploy real. Rollback de aplicação nunca executa migration nem reverte schema.

## Limites externos e estado final

A versão publicada foi identificada por hashes sem alterar o container produtivo, seu horário de início ou imagem. A validação não utilizou uma restauração sanitizada do banco real nem credenciais de bucket R2 exclusivo para homologação. São necessárias essas entradas, revisão e configuração do GitHub/servidor antes de afirmar prontidão produtiva completa; ações detalhadas em [PENDENCIAS_PRODUCAO](PENDENCIAS_PRODUCAO.md).

A implementação recuperável está pronta para revisão local e ensaio de migração controlada. A ativação da publicação e o corte do mecanismo legado continuam pendentes de autorização específica.

## Organização local do Git

Os commits anteriores `bcabccd` e `cabe741` foram preservados como ancestrais. As mudanças foram organizadas em commits locais de atualização .NET/imagens, segurança, regressões funcionais, recuperação do Release, banco, normalização de SQL, CI/CD e documentação. O README e estes relatórios descrevem o estado integrado. A worktree isolada fica ao lado do clone principal; o relatório de auditoria já existente na `main` permaneceu intocado.

Nenhum comando de push, criação de PR, merge, arquivamento de repositório ou ativação remota foi executado. O histórico exato da entrega pode ser consultado com `git log --oneline cabe741..HEAD`; os arquivos alterados, com `git diff --stat cabe741..HEAD`.
