# Auditoria final de produção do AndersonMultimarcas

Data: 09/11/2026. Horários em Brasília, salvo indicação UTC. Repositório: [StartingWeb/AndersonMultiMarcas](https://github.com/StartingWeb/AndersonMultiMarcas).

**Recomendação: CORRIGIR ANTES.** A aplicação candidata passou nos testes atuais, o banco mantém o contrato de escrita preparado e a VPS permite preparação paralela. A publicação unificada ainda depende de **quatro pendências de preparação**: integração e pacote do SHA final; fechamento da paridade dessa versão; suspensão do gatilho legado; backup final com restauração isolada. Depois disso, o primeiro corte exige autorização operacional e habilitação dos gates. Gates fechados são uma proteção intencional, não quatro falhas de código.

Não houve merge, push, disparo ou reexecução de workflow, deploy, parada/reinício de container produtivo, migration, DML, alteração de DNS/proxy, proteção ou segredo, nem escrita/exclusão de objeto no R2 produtivo. Consultas produtivas usaram SQL SELECT/metadados e HTTP GET/HEAD. Os testes de escrita da aplicação usaram SQL e arquivos sintéticos isolados no Mac; o teste R2 isolado está descrito adiante.

Evidência consolidada: [FINAL_AUDIT_2026-11-09.json](./evidence/FINAL_AUDIT_2026-11-09.json). Os recibos de 07/11 citados são históricos, com seus limites preservados; as conclusões atuais usam revalidação de 09/11.

## 1 Painel obrigatório

CONCLUÍDO significa que a verificação indicada passou no escopo descrito. Não significa que a nova aplicação esteja publicada.

| Componente | Status | Pendência ou evidência atual |
| --- | --- | --- |
| Repositório GitHub | CONCLUÍDO | Repositório unificado e referências remotas consultados. |
| Branch de produção | PENDENTE | `main` ainda não contém as PRs de preparação; falta o SHA final integrado. |
| Pull Requests | PENDENTE | PRs #2 e #3 abertas e draft; checks dos próprios tips aprovados. |
| CI/CD | BLOQUEADO | Deploy fechado por gates; verificador antigo em `main` incompatível com o Environment atual; correção na PR #2. |
| GitHub Environment | CONCLUÍDO | `production` restrito a `main`, sem aprovador por release e sem bypass administrativo, conforme política atual. |
| Secrets e variáveis | PENDENTE | Cinco nomes SSH presentes; três gates false. Valores criptografados do GitHub não são legíveis para validar equivalência. |
| VPS | CONCLUÍDO | Capacidade disponível e baseline preservado; nenhum preparo exige parar o legado. |
| Docker | CONCLUÍDO | Legado saudável, imagem imutável identificada; executor novo confere com o instalado. |
| Nginx Proxy Manager | CONCLUÍDO | Dois proxy hosts e três domínios apontam para o mesmo container; HTTPS aprovado. |
| SQL Server | CONCLUÍDO | ONLINE, A/B/C e permissões runtime confirmadas; contrato conhecido compatível com o código candidato. |
| Backup automático | CONCLUÍDO | Diário de 09/11 às 03h, integridade e monitor aprovados; novo backup restaurável necessário no corte. |
| Cloudflare R2 | CONCLUÍDO | Leitura real, configuração legado/futuro coincidente e isolamento da homologação verificados. |
| Imagens dos veículos | PENDENTE | 131 referências já ausentes; somente uma pertence a veículo ativo, que mantém 18 imagens disponíveis e capa válida. |
| Aplicação nova | PENDENTE | Testes atuais aprovados; falta validar e aprovar o conjunto final integrado para o primeiro corte. |
| Testes automatizados | CONCLUÍDO | 193 .NET, 111 Python, 56 navegador, 9 Docker e 5 imagem aprovados no candidato atual. |
| Rollback | PENDENTE | Imagem/snapshot e ensaios aprovados; retenção do container legado sob nome reservado ocorre somente no corte autorizado. |

## 2 GitHub e versão candidata

| Referência | SHA completo | Situação |
| --- | --- | --- |
| `main` remota | `1e5612276e0809166a2ea33a21deabee0bf0f768` | Fonte que o pipeline escolheria hoje. |
| `infra/production-preparation` | `edebcf16a7333602d239160adb09484eaee6a2d0` | Candidata mais atual de infraestrutura; [PR #2](https://github.com/StartingWeb/AndersonMultiMarcas/pull/2), 13 commits à frente de main. |
| `infra/schema-compatibility-review` | `8116388ca2eaac0ba7e9a0e473c9401b776f8912` | Complemento SQL/documental; [PR #3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3), 5 commits à frente. |
| `feat/unified-cicd-deployment` | `3feb08bb6faa5d38c4bfbfc0b7d589d24359a9fd` | Já incorporada pela PR #1. |
| Repositório legado Release | `fb8a205768dce239c960ffb1cfb043b2680de7b3` | Checkout de publicação legado; não atribuir este SHA ao binário somente por inferência. |

As branches candidatas e a main remota têm os mesmos arquivos da aplicação em Project/Core/Data/Domain; os avanços pendentes são principalmente infraestrutura, testes e documentação. A main local está atrasada e não foi usada como candidata. A combinação dos dois tips foi simulada sem conflitos, mas não foi integrada nem submetida a CI combinado.

Os checks `validate` dos tips exatos das PRs estão verdes, nos eventos push e pull_request. Exemplos atuais: [PR #2 CI](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42392591580) e [PR #3 CI](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42386554511). A proteção exige PR, `validate` strict vinculado ao app GitHub Actions, administradores sujeitos às regras e conversas resolvidas; proíbe force push e exclusão. Exige **zero aprovações**, sem code owner ou aprovação do último push obrigatórios. Não há ruleset adicional. Não existe bloqueio atual por falta de outro revisor.

O Environment `production` aceita somente `main`, sem wait timer/revisores e com `can_admins_bypass=false`. A política corresponde à publicação automática desejada. O verificador da main ainda exige revisores e recusou esses metadados em reprodução local; o verificador da PR #2 passou. Portanto, habilitar os gates antes de integrar a correção não resolve a publicação.

Os cinco secrets do Environment existem por nome: `PRODUCTION_SSH_HOST`, `PRODUCTION_SSH_PORT`, `PRODUCTION_SSH_USER`, `PRODUCTION_SSH_PRIVATE_KEY`, `PRODUCTION_SSH_KNOWN_HOSTS`. Seus valores criptografados não foram obtidos pela API e não constam dos entregáveis. A chave dedicada existente no Mac autenticou com host key fixada e respondeu ao preflight restrito; isso não demonstra que o conteúdo criptografado de cada secret GitHub seja idêntico.

As variables de repositório `PRODUCTION_DEPLOY_ENABLED`, `PRODUCTION_SOURCE_PARITY_CONFIRMED` e `PRODUCTION_ADMIN_BYPASS_REVIEWED` continuam `false`. O endpoint público configurado é `/health/release` no domínio www. O terceiro gate atesta revisão dos controles; não concede bypass.

**Nenhum commit novo seria publicado com a configuração atual.** Se o fluxo fosse reexecutado sem integrar as PRs, sua fonte seria o SHA de main acima, mas o job produtivo continuaria pulado. O SHA da futura combinação só existirá após a integração autorizada.

## 3 VPS Docker e proxy

A VPS tem 2 CPUs, aproximadamente 7,75 GiB de RAM total e 2,59 GiB de memória disponível no snapshot, com 67,8 GiB de disco livres. Havia 21 containers cadastrados, 20 em execução. O Anderson consumia aproximadamente 465 MiB; seu SQL compartilhado, 1,18 GiB. Esses números permitem preparar uma aplicação paralela limitada e identificada, sem nova instância SQL produtiva nem build na VPS. Não são um ensaio de carga.

O container `andersonmultimarcas-web` permanece saudável, ID `9842fa7e0f745dc589ddc9ee230813840524ff65c8aecb3d54cf6eaf7f0c2a78`, imagem `startingweb/andersonmultimarcas-release:latest`, digest `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5`. Seu início permanece em 23/10/2026, com um restart histórico. A tag latest sozinha não identifica o rollback; o digest e o arquivo offline identificam.

| Item | Estado verificado |
| --- | --- |
| Publicação legada | `/opt/startingweb/apps/anderson-multimarcas`, Compose com serviço `web`. |
| Publicação unificada | `/opt/startingweb/apps/anderson-multimarcas-unified`, diretórios bin/shared/incoming/releases/snapshots; releases ainda vazio. |
| Configuração futura | `shared/runtime.env` privado 0600, sem valores vazios; servidor/rede/DP explícitos. |
| Rede | `startingweb_network`; Anderson expõe 8080 somente internamente, sem porta de host. |
| Persistência | Volume `andersonmultimarcas-dataprotection-keys`, app name `AndersonMultiMarcas`; imagens em R2, sem mount local de uploads na política nova. |
| Gates locais | `deploy.enabled` e `source-parity.approved.json` ausentes; sem `current.json` ou transação de deploy pendente. |
| Executor | Hash do release.py e ssh_gate.py instalado coincide com o candidato. |
| Proxy | Hosts NPM 4 e 5, ativos, SSL forçado, upstream `http://andersonmultimarcas-web:8080`. |

Home, catálogo, empresa, login e health responderam 200 com TLS válido em `anderson.startingweb.com.br`, `andersonmultimarcas.com.br` e `www.andersonmultimarcas.com.br`. Admin anônimo redireciona ao login. `/health/release` retorna 404 no legado, comportamento esperado; não é prova de falha do site atual. Não houve login ou visita a detalhe produtivo para evitar escritas de autenticação/contadores.

**A VPS está pronta para receber a preparação paralela sem derrubar o atual. O primeiro corte implementado exige uma janela curta**, pois conserva o upstream e o nome do container, e o executor recusa substituir um legado vivo não gerido. Um segundo container não deve receber tráfego nem credenciais de escrita produtiva durante testes isolados.

O executor novo não executa prune global, compose down, restart SQL/NPM ou mudanças DNS/proxy. Os demais sistemas continuam compartilhando CPU, memória e parte da rede, portanto iniciar testes sem limites ou repetir builds pesados na VPS aumentaria risco. Na preparação atual, o risco concreto adicional é o gatilho legado ainda ativo.

## 4 SQL Server e backup

Produção usa `AndersonMultiMarcas`, SQL Server 2022 Express `16.0.4265.3`, ONLINE, recovery SIMPLE. A nova aplicação aponta para o mesmo banco. Foram encontrados 404 veículos, 4.458 mídias, 49 marcas, 3 lojas e 4 usuários, sem exposição de registros pessoais.

A/B/C permanecem aplicadas: 12 colunas ampliadas, somente LogoUrl com nulabilidade alterada, marca ID 4 com rótulo aprovado e `IX_Marca_Nome` único/habilitado. Não há grupos duplicados de Nome. O journal contém as três execuções e hashes de scripts esperados. Colunas, índices, FKs, constraints e histórico EF correspondem ao snapshot posterior a A/B/C, normalizando coleções vazias. As 22 FKs estão habilitadas e confiáveis.

A conexão futura autenticou realmente como `anderson_runtime`: SELECT permitido, permissão INSERT representativa presente, sysadmin e db_owner false, CREATE TABLE e INSERT no histórico EF negados. Nenhuma escrita foi tentada. Startup migrations, storage migration e recuperação automática de imports permanecem false.

Há 23 entradas no histórico EF produtivo. Duas migrations do código não constam nele: `20260530052000_RepairMissingAuditColumns` e `20261105185516_AlignRecoveredReleaseModel`. A primeira tem guards para colunas já existentes; a segunda alinha snapshot sem Up/Down estrutural. O ensaio histórico restaurado demonstrou que apenas o histórico seria acrescentado. Não é necessário executá-las para iniciar este candidato, nem inserir entradas para fabricar equivalência. A geração de SQL nesta auditoria foi offline; o modelo atual não apresenta mudanças pendentes.

**Não foi encontrada incompatibilidade no contrato de banco conhecido para o código candidato atual.** O checker real retornou `ready / backup_integrity=verified / schema_contract=compatible`, e os testes SQL locais passaram. Isso não equivale a uma garantia de 100% sobre todos os fluxos de um SHA integrado ainda inexistente. Revalidar o conjunto final; não repetir A/B/C nem executar o script histórico completo em produção.

O diário de **09/11 às 03:00** concluiu às 03:00:46, run `20261109T060000Z-ddddb768`, arquivo de 30.269.440 bytes. Tamanho e SHA-256 foram recalculados e conferem. VERIFYONLY WITH CHECKSUM foi aprovado; o repositório Restic foi autenticado e `check --read-data` passou nesta auditoria. Há seis snapshots verificados. A retenção permanece 7 diários, 4 semanais, 3 mensais.

Timer diário e monitor estão enabled/active/persistentes, linger habilitado; próxima execução em 10/11 às 03h. Monitor saudável às 10:35 de 09/11. Destinatário configurado: `startingwebltda@gmail.com`; o envio do diário foi aceito em DATA 250, TLS 1.3 com certificado validado. **Isso comprova aceite SMTP, não entrega na caixa de entrada.** Não foi enviada outra mensagem nesta auditoria.

A última restauração SQL real registrada terminou em 07/11 às 12:07:28, com DBCC, integridade referencial e comparação da cópia aprovados. O recibo foi relido hoje; não houve nova restauração nesta auditoria. O ensaio histórico dos scripts produtivos exatos e retorno ao binário legado tem 78 checkpoints e nove negativas aprovados. A restauração de 07/11 precede A/B/C; seu antigo campo `review_required` descreve o drift então encontrado, posteriormente tratado, não uma falha nova.

O procedimento está documentado em [BACKUP_ANDERSONMULTIMARCAS.md](./BACKUP_ANDERSONMULTIMARCAS.md) e [ROLLBACK_PRODUCAO.md](./ROLLBACK_PRODUCAO.md). Antes do corte, produzir nova cópia restaurável e validar sua restauração isolada. Todas as cópias atuais ficam na VPS: perda total do host pode perder banco e backups. O aceite temporário desse risco está registrado em [ACEITE_RISCO_BACKUP_LOCAL.md](./ACEITE_RISCO_BACKUP_LOCAL.md); cópia externa continua recomendação, não bloqueio técnico adicional sob esse aceite.

## 5 Cloudflare R2 e imagens

Legado e runtime futuro coincidem nos campos de endpoint, conta, bucket, base pública, região e credenciais, comparados sem revelar seus valores. Provider R2, bucket `startingweb-storage`, região auto, endpoint HTTPS correspondente à conta e dual read true. HEAD do bucket respondeu 200. A credencial S3 não comprova toda a política administrativa da conta ou exclusividade desse bucket; não se presumiu que o bucket pertença só a este projeto.

Foram auditadas por HEAD todas as **4.458 referências de mídia de veículos**: **4.327 disponíveis e 131 com 404 autenticado**, com confirmação pública das ausências. Quatro falhas transitórias de transporte público foram reconsultadas com 200. Os pares de download autenticado/público amostrados tiveram SHA-256 igual. Nenhuma mídia disponível foi declarada perdida por causa da nova versão.

As 131 referências ausentes distribuem-se por 12 veículos. **Nenhum dos 181 veículos ativos ficou sem imagem disponível no inventário; nenhuma capa ativa está ausente.** O único ativo afetado é o ID 707: uma referência ausente entre 19, com 18 disponíveis e capa disponível. Os outros 11 veículos afetados estão inativos. Os IDs 933 e 934 têm 18 e 20 referências ausentes, respectivamente, e ambos estão inativos. Todos os 131 caminhos também estão ausentes do wwwroot real do container legado.

Há objetos com outros UUIDs nos prefixos limitados de 933/934, mas não foi comprovado vínculo com as referências faltantes. Isso é um acervo potencial para investigação, não autorização para atribuir imagens por aproximação nem prova de recuperação. Não se declarou que esses originais sejam irrecuperáveis.

As oito fotos de vendedores continuam 404. Os 49 campos de logo de marcas apontam para duas URLs distintas também ausentes. Essas ausências são anteriores ao deploy unificado. Não foram apagadas URLs, preenchidas imagens fictícias ou alterados objetos para ocultar o problema.

O candidato aceita os dois prefixos existentes, `uploads/veiculos` e `anderson-multimarcas/veiculos`. Há 116 referências relativas, todas ausentes hoje; 19 referências não têm chave explícita. Entre as 4.439 com chave explícita, não houve divergência entre chave e caminho da URL. O resolver novo gera URLs sem HEAD/cache de existência; o legado consultava R2/local e podia filtrar referências relativas ausentes. A nova galeria pode apresentar mais placeholders e a lightbox merece tratamento de erro. Isso é diferença de apresentação de referências já quebradas; não se comprovou perda nova de bytes disponíveis. Principal e miniaturas possuem fallback.

Homologação usa `anderson-multimarcas-homologacao`, com credencial separada; controle `anderson-multimarcas-homologacao-negativa`. HEAD do bucket autorizado passou, prefixo de teste foi inspecionado e os testes de GET/LIST fora do bucket autorizado retornaram AccessDenied em produção e no controle. O ciclo sintético completo de 07/11 permanece como evidência histórica.

O inventário global de buckets com a credencial produtiva retornou 403. A existência/privacidade atual do controle negativo não foi confirmada independentemente pela API de gestão; seu 403 prova negação de acesso, não essas propriedades. O escopo administrativo e a permissão atual de escrita da credencial produtiva não foram testados. O teste de escrita a seguir comprova o serviço com a credencial isolada, sem estender essa conclusão à produção.

Em 09/11, o R2StorageService atual, compilado por referência ao Core.csproj, passou em PUT de chave UUID inédita, HEAD image/png e GET autenticado/público, ambos com SHA-256 igual ao original. O PNG final tem CRCs e deflate validados. Foram retidos **dois objetos sintéticos de 68 bytes cada**, sem overwrite ou DELETE: a primeira tentativa confirmou transporte, mas seu fixture tinha CRC inválido; a segunda usou fixture corrigido e outro UUID. As duas chaves e recibos estão na evidência consolidada para eventual limpeza autorizada posterior. Nenhum objeto produtivo foi criado ou alterado.

**Risco real para imagens:** existem referências órfãs no ambiente atual, mas nenhum veículo ativo está sem todas as imagens. Manter endpoint/bucket/credenciais e os objetos existentes preserva as imagens disponíveis. A publicação não recuperará arquivos ausentes. O aceite de paridade deve registrar esse baseline; eventual restauração de objetos ou alteração de dados produtivos exige lote identificado e autorização própria.

## 6 Comparação funcional e testes atuais

Todas as 24 páginas Razor identificadas na metadata do legado foram preservadas; o candidato acrescenta ResetPassword. Rotas antigas de login/admin/detalhe permanecem com redirecionamentos. As quatro escritas da API exigem autenticação, autorização por menu/perfil e antiforgery; consumidores que escreviam anonimamente precisam atender esse contrato. Não foi identificado consumidor externo produtivo dependendo de escrita anônima.

O clone Release e o material decompilado foram usados como evidência reconstruída. Dos estáticos, 70 compartilhados são idênticos por SHA-256; zero compartilhados diferem. Os exclusivos legados são CSS gerado e poster sem uso no Razor final; a fonte acrescenta JS de revisão de capas. Não se encontrou remoção de funcionalidade de negócio nessa comparação, dentro dos fluxos testados. Não se confundiu correspondência de DLLs com identidade OCI do container.

O candidato usa .NET 10/EF 10.0.13, SQL Server, AWS SDK S3 para R2 e Magick.NET para imagens. A conversão WebP nativa foi exercitada na imagem Linux. Links de WhatsApp não enviaram mensagens nos testes. O SMTP verificado pertence ao backup; não se presumiu um serviço de e-mail de aplicação. A autenticação Identity e a persistência de chaves DP foram preservadas como requisitos do corte.

| Teste executado em 09/11 | Resultado |
| --- | --- |
| Restore locked e build Release | Zero warnings e erros. |
| .NET com SQL Server descartável | 193 aprovados, zero falhas e zero ignorados. |
| Python CI, deploy e operações | 32 + 42 + 37 = 111 aprovados. |
| Chromium com SQL e conta sintéticos | 56 checkpoints, zero erros JavaScript, larguras 1440/768/390; capturas inspecionadas. |
| Imagem Linux amd64 do SHA edebcf16 | 5 cenários offline aprovados, usuário app, revisão exata, health/estático/WebP e recusa de migration Production. |
| Deploy e rollback Docker reais isolados | 9 cenários aprovados, incluindo falhas de health/startup/checksum/configuração/transferência e retorno ao mesmo legado simulado. |
| NuGet direto e transitivo | Zero advisories retornados. |
| Modelo EF e SQL | Sem alteração pendente de modelo; SQL idempotente gerado offline. |
| Workflow e shell | Validação e ShellCheck aprovados. |

Os 31 casos de segurança e os testes SQL são subconjuntos dos 193, não testes adicionais. A rodada inicial sem SQL ignorou 21; a rodada final executou todos. Containers, redes, volumes e processos de testes isolados foram removidos e ausência conferida; os recursos produtivos permaneceram ativos.

O navegador isolado cobriu catálogo, ordenação/filtros/paginação, detalhes, vendidos/inativos, contato/lojas, JSON-LD, login/logout, telas administrativas, CRUD sintético, criação de veículo, PNG convertido e renderizado, preço salvo e seleção de capa com CSRF. R2 em memória nesses testes não substitui o ensaio real isolado do serviço de storage.

Os logs legados de 48h contêm erros de storage ausente, Base64, LINQ e cancelamento/comando SQL. Foram contados 26 cabeçalhos SqlException, com duplicações adicionais de stack/ErrorModel; não equivalem a 73 incidentes únicos. Consultas públicas atuais passaram, mas os logs não estão sem erros. A causa/impacto por requisição precisa de triagem; não se atribuiu a genericidade desses logs ao candidato. A ordenação SQL por preço passou em testes reais, sem alegar que isso explica qualquer log LINQ antigo.

## 7 Processo exato do CI/CD

1. Push/PR executa CI; somente CI originado de push em main produz o artefato de aplicação apto a CD. CI de PR ou workflow_dispatch não serve como fonte produtiva nesse contrato.
2. CD é acionado por `workflow_run` de CI concluído e exige sucesso, evento push, main e mesmo repositório. Recusa fonte obsoleta comparando o SHA.
3. A imagem Linux amd64 é construída no runner a partir do artefato com checksums/proveniência. A PR #2 acrescenta teste da imagem final reconstruída no CD e vincula o resultado ao imageId exportado.
4. A entrega usa GitHub Actions Artifacts, **não um registry Docker**: image.tar.gz, release.json e SHA256SUMS. Artefatos de aplicação duram 30 dias; bundles/evidências CD, 90 dias.
5. O job production exige os três gates GitHub true e o Environment. SSH/SCP com host key fixada recebe o pacote em incoming dedicado; a chave não oferece shell/SFTP/leitura arbitrária.
6. O servidor confere hashes, executor instalado, lock, gates locais, aprovação de paridade, configuração privada e checker fixo de backup/schema. Snapshot da configuração fornece as variáveis ao container.
7. Não há migration no deploy. A atualização mantém rede/volume DP e nome de upstream, sem portas de host novas. O primeiro legado precisa ser parado/renomeado manualmente sob autorização; não é substituído automaticamente enquanto vivo.
8. Health interno exige processo e SHA em `/health/release`. A entrega também exige HTTPS público e o mesmo SHA.
9. Falha durante a transação do executor tenta retornar ao container anterior e conferir health. Falha do health público solicita rollback ao legado retido no primeiro corte. Falha na própria recuperação conserva journal e exige operador.
10. **Falhas anteriores à criação do journal, inclusive pacote/transferência, não prometem religar um legado que o operador já tenha parado.** O plano de primeiro corte precisa prever retorno manual pelo ID preservado nesse caso.

O [CD histórico](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42136110621) terminou success porque package passou; **production foi skipped**. Não há deployment unificado registrado. Portanto: **o disparo automático está configurado, mas a publicação automática GitHub até VPS ainda não foi comprovada de ponta a ponta**.

O [workflow Release legado](https://github.com/StartingWeb/AndersonMultiMarcas-Release/blob/main/.github/workflows/deploy.yml) continua ativo, push/main, sem lock entre os dois repositórios. Não havia run em andamento. O script real antigo faz git reset, compose build, health em `localhost:8080` e prune global. A porta 8080 do host pertence à Evolution API, e não ao Anderson: o health antigo pode dar falso positivo. Não usar esse script no corte ou no rollback. Suspender seu gatilho preservando os recursos é requisito antes da publicação nova.

## 8 Pendências reais classificadas

Há **quatro pendências de preparação que impedem declarar o corte seguro hoje**. A autorização e a abertura dos gates vêm depois dessas evidências; não são um quinto defeito técnico.

| ID | Categoria | Descrição e evidência | Impacto e risco | Solução | Estimativa | Autorização | Codex |
| --- | --- | --- | --- | --- | --- | --- | --- |
| B1 | BLOQUEANTE | PRs #3/#2 draft; main antiga; não existe CI/bundle do conjunto final; verifier antigo recusa Environment real. | Publicar fonte incompleta ou falhar antes da entrega. | Integrar #3, atualizar #2 sem force push, testar conjunto, integrar #2 e validar CI/CD package do SHA final com gates false. | 45–90 min sem novas falhas. | Sim para integrar/push. | Executável com acessos existentes. |
| B2 | BLOQUEANTE | Recibo de paridade final ausente; conjunto integrado ainda não validado/aceito; mídias órfãs e diferença de apresentação precisam constar no baseline. | Aprovar uma release sem vincular funcionalidades, configuração, imagens e rollback ao SHA final. | Validar SHA/artefato de aplicação/configuração e exigir evidência da imagem efetivamente entregue; registrar paridade e estado preexistente. Reparar mídias só se houver fonte/lote autorizado; sua recuperação não é requisito automático quando já ausentes no legado. | 15–30 min, além de eventual reparação opcional. | Sim para aceite e materializar gate; testes isolados já autorizados. | Produz evidências; aceite pertence ao proprietário. |
| B3 | BLOQUEANTE | Workflow Release antigo permanece active no mesmo aplicativo. | Concorrência pode sobrescrever nova aplicação e executar prune global. | Suspender gatilho no escopo legado e comprovar ausência de publicação concorrente antes do corte. | 5–10 min. | Sim. | Executável. |
| B4 | BLOQUEANTE | Backup diário atual íntegro, mas cópia/restauração imediatamente anterior ao primeiro corte ainda não realizada. | Voltar a uma cópia antiga pode descartar gravações recentes; integridade não substitui restore. | Novo backup preservando anteriores, CHECKSUM/VERIFYONLY/SHA/Restic e restauração isolada com DBCC/comparação. | 10–25 min, sujeito ao tempo real. | Sim para execução operacional final; nada feito nesta auditoria. | Executável. |
| R1 | RECOMENDADO | 131 referências de veículos, oito fotos de vendedores e duas URLs de logos ausentes. | Placeholders, miniaturas/lightbox quebradas e imagens ausentes em cadastros. | Investigar fontes próprias e correspondência; tratar fallback de apresentação; reparar somente lote autorizado. | 30–90 min de triagem; recuperação depende dos originais. | Sim para objetos/dados produtivos. | Investiga e corrige código isolado; não inventa originais. |
| R2 | RECOMENDADO | Logs produtivos atuais contêm erros classificados; dump duplicado legado prejudica contagem. | Erros intermitentes podem continuar depois do corte. | Correlacionar requisição/stack/impacto e corrigir somente causa reproduzida. | 30–60 min de triagem inicial. | Leitura e reprodução isolada já autorizadas; mudança produtiva exige rodada própria. | Sim para diagnóstico; correção depende da causa. |
| R3 | RECOMENDADO | Backups e Restic permanecem exclusivamente na VPS, com risco aceito registrado. | Perda total do host pode perder todas as cópias. | Implantar destino externo separado quando autorizado. | 1–2h após destino/credencial. | Sim. | Sim após acesso autorizado; não bloqueia este corte sob aceite vigente. |
| N1 | NÃO BLOQUEANTE | Textos históricos e descrições de PR ainda mencionam revisão obrigatória/política proposta/R2 não homologado. | Operador pode seguir estado antigo. | Atualizar documentos vivos e marcar o histórico pela data; preservar recibos. | 15–30 min. | Edição documental reversível. | Sim. |
| N2 | NÃO BLOQUEANTE após B3 | Script legado tem health errado e prune global. | Reativação acidental posterior pode causar incidente. | Registrar obsolescência e desativação; revisar sua retirada em rodada separada. | 15–30 min. | Sim para alterar automação/recurso remoto. | Sim. |

## 9 Procedimento de publicação definitiva

### Preparação sem indisponibilidade

1. Revalidar refs, checks, saúde e ausência de deploy concorrente. Manter gates false.
2. Sob autorização própria, retirar draft/integrar PR #3; seus SQLs são fonte auditável, não ordem de reaplicação. Atualizar #2 com main resultante, resolver qualquer diferença e exigir CI do novo tip; integrar #2.
3. Registrar SHA final de main, CI push/main e CD/package correspondentes. Conferir bundle, checksums, imageId, relatório da própria imagem final e executor instalado. Não usar o bundle antigo de main nem a imagem local desta auditoria como substituto.
4. Concluir paridade do SHA final, artefato da aplicação e configuração em ambiente isolado. Revalidar R2 por leitura e isolamento de teste. Registrar explicitamente o baseline das mídias preexistentes e não alterar dados/objetos para aparentar paridade. A reexecução do CD reconstrói a imagem e pode mudar seu imageId; exigir proveniência e relatório aprovado da imagem efetivamente entregue. Uma diferença relevante fora dessa evidência exige nova validação antes da ativação, sem presumir aprovação de um imageId diferente.
5. Executar backup final e restauração isolada com recursos limitados; conferir journal A/B/C/schema/runtime em somente leitura, DP, arquivo da imagem legada e rollback. Não correr migration por causa das duas entradas EF históricas.
6. Suspender somente o gatilho legado autorizado e comprovar ausência de execução ativa. Definir operador, limite de tempo e critérios de retorno. Nenhuma mudança NPM/DNS é necessária para o desenho atual.

### Janela de manutenção do primeiro corte

7. Confirmar autorização operacional e aceite da paridade. Materializar recibo com SHA final e digest legado exatos; habilitar os três gates GitHub e o arquivo local somente nesse escopo. Conservar proteções e Environment automático, sem bypass.
8. Preservar o mesmo container legado, parando e renomeando para `andersonmultimarcas-legacy-fb8a205768dc`, sem remover container, volume ou imagem. Conferir ID/digest/mounts antes e depois.
9. Reexecutar de forma autorizada o **CD já vinculado ao CI push/main do SHA final**, mantendo main inalterada. Uma mudança de variable não dispara workflow; CD não tem workflow_dispatch. A reexecução integral pode reconstruir o pacote antes de entregar, portanto esse tempo faz parte da janela. Registrar run/attempt reais.
10. Acompanhar package, preflight, transferência e executor. Antes de retornar, impedir que uma entrega pendente dispute o container: cancelar o run quando aplicável e confirmar estado do executor remoto, lock e journal. Não fechar gates locais necessários ao rollback antes de recuperá-lo. Em falha antes de existir journal, devolver manualmente o mesmo legado ao nome/upstream original e iniciá-lo; se houver transação/current geridos, usar o rollback revisado e confirmar seu resultado. Nunca usar deploy.sh legado para recuperar.
11. Exigir health interno, SHA final em `/health/release`, HTTPS nos três domínios, catálogo, login autorizado, lojas/páginas, galeria/capas e logs. CRUD/uploads devem usar registro sintético identificado e autorização operacional, sem editar cadastro de cliente para testar. O ensaio R2 isolado precede qualquer teste de upload produtivo.
12. Observar por 30–60 min, preservar legado/snapshots e registrar conclusão. Falha funcional material, SHA incorreto, indisponibilidade persistente ou falha de imagem disponível exigem retorno e suspensão de novas publicações.

A preparação pode ocorrer com o legado no ar. Estimativa do trabalho restante: **2–4 horas após autorização**, sem novas falhas e sem exigir recuperação opcional de mídias históricas. Reservar **10–20 minutos de manutenção** para o primeiro corte via workflow e um limite de retorno previamente combinado. Isso é estimativa, não duração comprovada do primeiro deploy produtivo.

Rollback de container volta binário/configuração e o mesmo upstream. **Não reverte banco, migrations ou R2.** A/B/C permanecem ampliadas; um restore produtivo separado pode descartar gravações posteriores e não está implicitamente autorizado por um rollback de aplicação.

## 10 Respostas obrigatórias

1. **Está tecnicamente pronto para produção?** O candidato passou nos testes e a infraestrutura base está preparada; o corte ainda não está pronto para liberação por B1–B4.
2. **O que impede agora?** Integração/pacote final, paridade vinculada a esse pacote, concorrência legada ainda habilitada e backup/restauração final. Os gates permanecem fechados até isso e a autorização do corte.
3. **Quantas bloqueantes?** Quatro pendências de preparação. A autorização operacional é condição separada, não um quinto defeito.
4. **Codex consegue resolver todas?** Consegue executar a parte técnica com os acessos atuais; o proprietário precisa autorizar integração/corte e aceitar a paridade. Recuperar arquivos históricos, se desejado, depende de originais ou vínculo comprovado, não de gerar imagens novas.
5. **Quanto falta?** Aproximadamente 2–4h após autorização, mais 30–60min de observação; manutenção estimada em 10–20min no primeiro corte.
6. **Se autorizar deploy agora, o que acontece?** Autorização verbal não muda configuração nem dispara CD. No estado atual production é skipped. Abrir gates sem corrigir main falha no verificador; mesmo depois disso, o executor recusa substituir o legado vivo. Deve-se executar a sequência preparada.
7. **Há risco de dados, imagens ou outros sistemas?** Sim: concorrência legada/prune, erro operacional e recursos compartilhados; o desenho novo evita esses comandos globais. Não se identificou perda nova de imagens disponíveis; há órfãos atuais. Backup só local mantém risco de perda total do host. Container rollback não desfaz escritas.
8. **Deploy automático por push configurado e funcional?** Encadeamento configurado e CI/package comprovados. Publicação produtiva ponta a ponta ainda não comprovada, com gates false e production skipped.
9. **Menor conjunto de ações?** Resolver B1–B4; autorizar/materializar gates e paridade; preservar/parar/renomear legado; reexecutar CD correto e validar/observar com rollback disponível. Não repetir SQL A/B/C, mudar DNS ou exigir revisor inexistente.
10. **Recomendação?** **CORRIGIR ANTES.** Não emitir o atestado de aptidão para deploy até concluir os quatro pré-requisitos e revalidar o estado final.

O [segundo prompt específico](./PROMPT_CORRECOES_E_DEPLOY_2026-11-09.md) prepara a próxima execução e não foi executado nesta auditoria.
