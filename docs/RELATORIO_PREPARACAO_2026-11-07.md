# Relatório de preparação — 07/11/2026

Auditoria executada no Mac associado ao projeto e por SSH já existente ao `startingweb-server`. As evidências abaixo são snapshots datados; checks precisam corresponder ao tip da PR e ao futuro SHA integrado. Nesta rodada, não houve merge, deploy, ativação de gates, modificação de proteção, PUT/DELETE produtivo ou repetição de migrations.

## A. GitHub

Main confirmada: `1e5612276e0809166a2ea33a21deabee0bf0f768`. Proteção clássica ativa, nenhuma ruleset adicional/conflitante, admins sujeitos às regras, validate strict app 15368, PR obrigatória, uma aprovação, code owner/último push obrigatórios, conversas resolvidas, force push e exclusão proibidos. Só StartingWeb é colaborador.

**Proposta exata, não aplicada:** aprovações 1→0, code owner obrigatório true→false e último push true→false. Manter descarte de aprovações antigas true, PR/CI e demais controles; preservar CODEOWNERS proposto na PR #2. PATCH parcial e reversão estão em [GITHUB_POLITICA_REPOSITORIO_INDIVIDUAL.md](./GITHUB_POLITICA_REPOSITORIO_INDIVIDUAL.md). O risco novo é perda do veto humano independente; PR/diff/checks auditáveis, testes/segurança/proveniência, tokens mínimos e controles de branches/deploy compensam parte do risco, sem substituir outra pessoa.

Environment production permite apenas branch main, sem aprovador por release e sem bypass. Cinco nomes de SSH secrets presentes, valores não consultados; três gates false. Token padrão read e Actions não podem aprovar PR. Secret scanning/push protection habilitados, zero alertas abertos retornados. Dependabot desabilitado (403), code scanning sem análise (404): não há conclusão de zero vulnerabilidades por esses serviços; não foram ativados.

PR #3 aberta/draft, tip `8116388ca2eaac0ba7e9a0e473c9401b776f8912`; PR #2 aberta/draft, tip inicial auditado `3f319d4de6cdc287fa9b2730bb413ac54b1be518`. Validate aprovado nos eventos push/PR desses tips, sem conflitos, sem revisões e bloqueio REVIEW_REQUIRED. Esta rodada atualiza somente a branch de preparação da PR #2 com correção/documentação; o novo tip exige CI próprio. [Estado ao vivo da PR #2](https://github.com/StartingWeb/AndersonMultiMarcas/pull/2/checks) e [PR #3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3/checks). Simulação combinada sem conflitos não é CI do conjunto. [Evidência](./evidence/GITHUB_AUDIT_2026-11-07.json).

**Aprovação pendente:** somente o PATCH exato de governança descrito, sem autorização de merge/gates/deploy.

## B. Backup

[Aceite temporário exclusivamente local](./ACEITE_RISCO_BACKUP_LOCAL.md) registrado em 07/11, sem data arbitrária de revisão. Perda total da VPS pode perder todas as cópias; não é disaster recovery externo. Nenhuma contratação/transferência externa executada.

Diário `20261107T060000Z-ab4e6a7b`: 07/11 03:00:00–03:00:38, unidade concluída 03:00:45, sucesso. Próximo: 08/11 03h America/Sao_Paulo. Timer e monitor enabled/active/persistentes, linger=yes, último monitor saudável. Retenção 7 diários/4 semanais/3 mensais inalterada.

Último backup válido extra pré-migration `20261107T150452Z-c36eef7e`: COPY_ONLY, SQL CHECKSUM/VERIFYONLY, SHA-256 e Restic criptografado. Helper atual reautenticou repositório e leitura integral, backup_integrity verified e schema compatible/ready. Restore real existente `46fe30bdb760` aprovado com DBCC e integridade; não foi repetido. Cinco snapshots e três nativos preservados, inventário antes/depois idêntico. Espaço livre na auditoria: 65.336.471.552 bytes (~60,85 GiB). SMTP DATA250/TLS1.3 aceito, sem comprovação de entrega na caixa.

Nenhum backup duplicado, exclusão, prune/forget, mudança de retenção ou novo envio de e-mail. Antes do futuro primeiro deploy, exigir novo backup restaurável. [Operação](./BACKUP_ANDERSONMULTIMARCAS.md), [evidência](./evidence/BACKUP_AUDIT_2026-11-07.json).

## C. Cloudflare R2

Endpoint R2 HTTPS, bucket `startingweb-storage`, região auto e base pública r2.dev; provider R2 e dual read habilitados, configurações legado/futuro idênticas por comparação privada. HEAD bucket 200. Segredos não registrados. Política de conta e isolamento do bucket não são comprováveis com as credenciais S3 existentes; o bucket não deve ser presumido exclusivo do Anderson.

Recomendam-se dois buckets sintéticos: `anderson-multimarcas-homologacao` para o ciclo e `anderson-multimarcas-homologacao-negativa` privado/vazio para negativas. Token novo Object Read & Write restrito apenas ao primeiro, sem acesso ao controle ou produção, e URL de teste própria. Prefixo com credencial ampla não é isolamento. Configuração local preparada; nenhuma criação remota. Ciclo preparado: upload → HEAD → GET autenticado/público → SHA-256 → inexistente → sobrescrita → exclusão do sintético → confirmação → negativas fora do escopo. O ciclo permanece não executado até destino isolado autorizado.

**Aprovação pendente:** criar/configurar o destino e suas credenciais/política estritamente limitadas conforme [VALIDACAO_R2.md](./VALIDACAO_R2.md). Não usar credencial administrativa ampla ou bucket produtivo em teste destrutivo.

## D. Mídias

**Oito referências de fotos de vendedores verificadas (IDs 3–10); oito 404 público e HEAD autenticado. Zero originais recuperados.** As três referências da amostra anterior eram IDs 5, 6 e 7. A consulta por prefixos exatos conhecidos não encontrou variantes de extensão; não se listou o bucket inteiro. Três imagens de veículos responderam 200 em leituras pública/autenticada, com SHA-256 coincidente.

Fontes próprias pesquisadas: container/volume Anderson, checkout e histórico Git legado, arquivos versionados, diretórios de uploads, releases/snapshots de aplicação, cinco snapshots Restic, quatro .bak locais próprios e camadas das imagens legada/futura arquivadas. Restic contém banco/manifesto, não bytes de fotos. Busca exata nos .bak encontrou referências aos UUIDs, inclusive em julho, sem restaurar banco; isso comprova referência histórica, não arquivo recuperável ou vínculo histórico de vendedor. Não foram acessados dados de outros projetos.

Cada ID/chave/status/fonte/ação está em [RECUPERACAO_MIDIAS_R2.md](./RECUPERACAO_MIDIAS_R2.md) e na [evidência](./evidence/R2_MEDIA_AUDIT_2026-11-07.json). As fotos são **não recuperadas nas fontes examinadas**; não se afirma inexistência em dispositivos externos não fornecidos. Nenhuma imagem inventada, atribuída a outra pessoa, URL apagada ou objeto alterado.

Plano de reparação: obter originais do proprietário/fonte autorizada, comprovar pessoa/arquivo, formato/dimensões/integridade/hash, validar chave e ausência de colisão e apresentar lote exato para autorização antes de restaurar qualquer objeto produtivo.

## E. CI/CD

Fluxo auditado: main → CI → artefato imutável/proveniência → preflight → deploy → health/rollback. Triggers, permissões mínimas, actions por SHA, concorrência serial/lock, checksums/commit/run e fallback preservados. As três variáveis continuam false; habilitação/paridade locais ausentes.

Lacuna corrigida na preparação: a imagem reconstruída pelo CD agora precisa passar pelo teste offline final sem rede, e o empacotador exige relatório completo do mesmo commit/imageId e usuário app antes de exportar. Evidência é preservada inclusive em falhas iniciais de inspeção; nenhum executor/serviço produtivo foi atualizado.

Validação local: **111 testes Python aprovados** (32 CI/segurança, 42 deploy/rollback, 37 operação); .NET **172 aprovados, 21 SQL ignorados** pela ausência de conexão descartável configurada nesta execução; zero falhas. Actionlint/ShellCheck aprovados; auditoria NuGet zero advisories. Ensaio Docker real local: cinco cenários de runtime e limpeza aprovados, exportação pelo novo empacotador aprovada, com imagem preexistente `3feb08bb6faa5d38c4bfbfc0b7d589d24359a9fd` como fixture. Esse ensaio não aprova a release final integrada. O CI obrigatório executa SQL descartável e precisa passar no novo tip publicado.

Gitleaks sem achados nos tips inicialmente auditados e na revisão dos arquivos novos/staged antes do commit/push, com saída redigida. Não foram adicionadas exceções ao scanner. [Evidência de pipeline](./evidence/PIPELINE_AUDIT_2026-11-07.json), [operação detalhada](./CI_CD_DEPLOYMENT.md).

Próxima sequência: aprovar/aplicar política → integração autorizada PR #3 → atualizar PR #2 com main resultante → CI combinado → PR #2 → CI final main/pacote → R2/mídias/paridade → novo backup restaurável → autorização operacional/gates/primeiro deploy. Nenhum merge/deploy nesta rodada.

## F. Produção

Site legado, três domínios, login e health responderam HTTP200. /health/release continua 404 próprio do legado. IDs/imagens/StartedAt/restarts de web, SQL e NPM idênticos ao baseline. SQL ONLINE, runtime existente habilitado/presente e histórico EF23 preservado. Backups/monitoramento íntegros.

Não houve alteração comercial/SQL, recriação/rotação de runtime, migrations A/B/C, troca/restart/remoção de container, alteração NPM/DNS, merge, deploy, modificação de branch protection/rulesets, criação de gates ou PUT/DELETE no R2 produtivo. As ações remotas foram de leitura; tráfego legítimo da aplicação permaneceu ativo.
