# Política GitHub para mantenedor individual

Auditoria em 07/11/2026, por consultas de leitura ao GitHub real. **A mudança de política está somente proposta; depende da confirmação específica do proprietário. Nenhuma proteção, ruleset, permissão, secret ou gate foi alterado. Não houve merge nem deploy.**

O repositório público [StartingWeb/AndersonMultiMarcas](https://github.com/StartingWeb/AndersonMultiMarcas) pertence a uma conta pessoal (`User`). Só `StartingWeb`, com papel `admin`, consta como colaborador. O connector GitHub forneceu metadados, PRs, revisões, threads e execuções; `gh` local consultou por GET/GraphQL os controles administrativos não expostos pelo connector. A [evidência sanitizada](./evidence/GITHUB_AUDIT_2026-11-07.json) registra os resultados e as limitações.

## Estado atual e proposta exata

A `main` permanece em `1e5612276e0809166a2ea33a21deabee0bf0f768`, protegida. Existe uma proteção clássica com padrão `main`. A listagem de rulesets, incluindo herdadas, retornou `[]`; GraphQL retornou zero conflitos e zero atores com bypass de PR/force push.

| Controle | Valor atual confirmado | Valor proposto |
| --- | --- | --- |
| PR antes da integração | Obrigatória; `requiresApprovingReviews=true`, objeto `required_pull_request_reviews` presente | Manter PR obrigatória e o objeto de proteção de PR |
| Aprovações obrigatórias | `required_approving_review_count=1` | **`0`** |
| Revisão obrigatória de code owner | `require_code_owner_reviews=true` | **`false`** |
| Aprovação por outra pessoa do último push | `require_last_push_approval=true` | **`false`** |
| Descartar aprovações antigas | `dismiss_stale_reviews=true` | Manter `true`; preserva a configuração para revisões opcionais |
| Check obrigatório | `validate`, GitHub Actions `app_id=15368` | Manter exatamente |
| Branch atualizada antes da integração | `required_status_checks.strict=true` | Manter `true` |
| Conversas resolvidas | `required_conversation_resolution.enabled=true` | Manter `true` |
| Regras valem para admins | `enforce_admins.enabled=true` | Manter `true` |
| Force push | `allow_force_pushes.enabled=false` | Manter `false` |
| Exclusão da branch | `allow_deletions.enabled=false` | Manter `false` |
| Bypass de PR/force push | Zero allowances | Manter zero |
| Restrição adicional por lista de atores de push | `restrictsPushes=false`, zero push allowances | Manter; a exigência de PR continua impedindo push direto |
| Histórico linear obrigatório | `required_linear_history.enabled=false` | Manter `false` |
| Assinatura obrigatória | `required_signatures.enabled=false` | Manter `false` |
| Bloqueio total/criação/sincronização de fork | `lock_branch=false`, `block_creations=false`, `allow_fork_syncing=false` | Manter |
| Rulesets | Nenhuma | Não criar nem substituir |

O delta de comportamento consiste em três campos: quantidade de aprovações de `1` para `0`, code owner obrigatório de `true` para `false` e aprovação do último push de `true` para `false`. O proprietário integra sua própria PR sem simular aprovação independente. A exigência de PR, o CI obrigatório e os demais controles permanecem.

A proteção de revisão atual impede o único mantenedor de integrar o próprio trabalho. O próprio autor não pode fornecer a aprovação independente exigida. Desabilitar somente a quantidade, mantendo code owner ou aprovação do último push, continuaria impedindo a integração.

### CODEOWNERS

A árvore remota foi consultada nos três locais reconhecidos pelo GitHub: `.github/CODEOWNERS`, `CODEOWNERS` e `docs/CODEOWNERS`. Não existe arquivo em nenhum deles na `main` nem no tip atual da PR #3. A PR #2 adiciona [CODEOWNERS](../.github/CODEOWNERS), apontando `/.github/`, `/scripts/ci/`, `/scripts/deploy/`, `/scripts/ops/` e `/deploy/` para `@StartingWeb`.

O GitHub usa o arquivo da branch base para solicitar code owners. Portanto, o arquivo introduzido na PR #2 ainda não constitui um gate efetivo para as PRs atuais. Também não cria um segundo revisor: `StartingWeb` continua sendo seu autor/proprietário.

A proposta **preserva o arquivo e todos os seus mapeamentos**; muda somente `require_code_owner_reviews` na proteção. Após aprovação/aplicação, os comentários descritivos que hoje exigem mantenedor independente devem acompanhar a política aprovada, sem apagar as atribuições. Até essa decisão, os comentários refletem a regra vigente. CODEOWNERS continuará documentando responsabilidade e solicitando revisão quando aplicável, sem tornar aprovação humana requisito de integração.

### Operação proposta, ainda não executada

A [API oficial de atualização da proteção de revisões](https://docs.github.com/en/rest/branches/branch-protection#update-pull-request-review-protection) oferece `PATCH` apenas para essa parte da regra. O [OpenAPI oficial consultado](https://github.com/github/rest-api-description/blob/main/descriptions/api.github.com/api.github.com.json) descreve `required_approving_review_count` como `1` a `6` ou `0` para não exigir revisores; `dismiss_stale_reviews` permanece um booleano, sem dependência da quantidade. O recurso é suportado em repositórios públicos, inclusive no plano Free; a conta/plano privado não foi inferida. Não foi necessário mudar o plano, criar organização, conceder escrita a terceiros ou habilitar bypass.

**Operação exata a aprovar:**

```http
PATCH /repos/StartingWeb/AndersonMultiMarcas/branches/main/protection/required_pull_request_reviews
Content-Type: application/json

{
  "dismiss_stale_reviews": true,
  "require_code_owner_reviews": false,
  "required_approving_review_count": 0,
  "require_last_push_approval": false
}
```

Não enviar `DELETE`, não substituir a proteção inteira e não acrescentar allowances. O PATCH evita reescrever checks, admins, force push, exclusão e conversas. Os parâmetros de listas de usuários/equipes específicos de organização não são necessários ao repositório pessoal.

Antes de uma aplicação futuramente autorizada, reler a proteção e comparar com esta evidência; qualquer drift material exige reapresentar o delta. Depois, reler REST e GraphQL, confirmar PR ainda obrigatória (`requiresApprovingReviews=true` e objeto presente), quantidade `0`, demais controles idênticos e gates `false`. Não validar a regra tentando push direto, merge ou deploy. A aceitação do payload em uma alteração real não foi testada nesta rodada, pois isso já modificaria a política.

## Environment, secrets, permissões e gates

| Recurso | Estado real confirmado e preservado |
| --- | --- |
| Environment | `production`, `can_admins_bypass=false` |
| Proteção de publicação | `protected_branches=false`, `custom_branch_policies=true`; única policy `name=main`, `type=branch` |
| Aprovação humana por execução | Nenhuma regra de required reviewers ou wait timer |
| Variable do repositório | `PRODUCTION_DEPLOY_ENABLED=false` |
| Variable do repositório | `PRODUCTION_SOURCE_PARITY_CONFIRMED=false` |
| Variable do repositório | `PRODUCTION_ADMIN_BYPASS_REVIEWED=false` |
| Variable do Environment | `PRODUCTION_PUBLIC_HEALTH_URL=https://www.andersonmultimarcas.com.br/health/release` |
| Secrets do Environment, somente nomes | `PRODUCTION_SSH_HOST`, `PRODUCTION_SSH_USER`, `PRODUCTION_SSH_PORT`, `PRODUCTION_SSH_PRIVATE_KEY`, `PRODUCTION_SSH_KNOWN_HOSTS` |
| Secrets de repositório | Zero nomes retornados |
| Permissão padrão do token de Actions | `default_workflow_permissions=read` |
| Actions aprovarem PRs | `can_approve_pull_request_reviews=false` |
| Política global de actions | `allowed_actions=all`, `sha_pinning_required=false`; as actions dos workflows inspecionados estão fixadas por SHA |
| Permissões declaradas no CI | `contents: read` |
| Permissões declaradas no CD | `contents: read`, `actions: read` |
| Auto merge | `allow_auto_merge=false` |
| Deployments registrados no Environment | Zero retornados |

A listagem de secrets informa existência por nome, sem revelar ou validar valores. Este levantamento não os leu nem os rotacionou. Não há proposta de alterar Environment, secrets, tokens ou permissão global de actions. A ausência de reviewer em `production` corresponde ao fluxo automático pretendido após a primeira ativação autorizada; não é uma aprovação independente.

O nome `PRODUCTION_ADMIN_BYPASS_REVIEWED` é um gate existente; não concede bypass. Continuará `false` nesta rodada. A proposta também não autoriza criar os arquivos privados `shared/deploy.enabled` ou `shared/source-parity.approved.json`; sua verificação no servidor consta da documentação operacional da rodada.

O fluxo do [CI](../.github/workflows/ci.yml) conserva testes, SQL descartável, auditoria NuGet e geração de artefato da identidade exata. O [CD](../.github/workflows/cd.yml) aceita somente CI bem sucedido de evento `push`, branch `main`, repositório idêntico e SHA atual, confere manifesto/checksums e mantém concorrência serial. O Environment limita a entrega à `main`. Alterar a quantidade de revisões não muda esses critérios nem habilita produção.

## Riscos, controles compensatórios e reversão

O risco novo é perder a revisão humana independente para mudanças do proprietário, inclusive alterações de CI, permissões, infraestrutura e deploy. O mesmo mantenedor pode errar no código ou nos próprios testes, ou ter a conta comprometida. Um CI verde não prova ausência de vulnerabilidades e não substitui uma segunda pessoa na análise. CODEOWNERS deixa de ser uma aprovação obrigatória.

Permanecem como controles compensatórios: PR auditável com diff/commits/checks, `validate` obrigatório vinculado ao app correto, branch atualizada, conversas resolvidas, aplicação das regras aos admins, ausência de bypass, bloqueio de force push/exclusão, tokens de leitura e secret scanning/push protection habilitados. Actions fixadas por SHA, dependências travadas, checks de proveniência, SHA atual de `main`, artefatos imutáveis, limites de Environment e gates locais/GitHub continuam vinculando publicação ao resultado testado. Nenhum acesso adicional será concedido.

Esses controles reduzem erros técnicos e exposição de credenciais; não impedem o proprietário de propor uma mudança defeituosa que também modifique os testes. Mudanças de segurança devem receber revisão cuidadosa do diff, e revisões externas opcionais continuam possíveis sem conceder escrita. A proteção de pinning é hoje do código de workflow; a plataforma não a exige globalmente. Não foi afirmado que exista CodeQL/code scanning ativo ou alertas Dependabot habilitados.

A reversão é outro PATCH no mesmo endpoint, após autorização específica:

```json
{
  "dismiss_stale_reviews": true,
  "require_code_owner_reviews": true,
  "required_approving_review_count": 1,
  "require_last_push_approval": true
}
```

Isso restaura os quatro valores atuais sem tocar os demais controles. Volta também a exigir um revisor independente para PRs do proprietário; sem ele, o bloqueio reaparece. Repor a regra não desfaz merges/deploys eventualmente realizados no intervalo e não reverte banco, arquivos ou objetos R2.

## PRs revalidadas e sequência de integração

| Evidência remota | PR #2 — infraestrutura | PR #3 — banco |
| --- | --- | --- |
| Estado | Aberta, draft, não integrada | Aberta, draft, não integrada |
| Head SHA | `3f319d4de6cdc287fa9b2730bb413ac54b1be518` | `8116388ca2eaac0ba7e9a0e473c9401b776f8912` |
| Base SHA | `1e5612276e0809166a2ea33a21deabee0bf0f768` | `1e5612276e0809166a2ea33a21deabee0bf0f768` |
| Distância da main | 11 commits à frente, zero atrás; 52 arquivos | 5 commits à frente, zero atrás; 22 arquivos |
| Conflito com a main | `mergeable=true`; não equivale a permissão de merge | `mergeable=true`; não equivale a permissão de merge |
| Check exigido | `validate`, app `15368`, sucesso no SHA acima | `validate`, app `15368`, sucesso no SHA acima |
| CI pull_request | [42386551265](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42386551265), success | [42386554511](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42386554511), success |
| CI push | [42386548809](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42386548809), success | [42386549835](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42386549835), success |
| Revisões/threads | Zero revisões e zero threads | Zero revisões e zero threads |
| Decisão de revisão | `REVIEW_REQUIRED`, integração bloqueada | `REVIEW_REQUIRED`, integração bloqueada |
| Manifestos de dependências no diff | Nenhum `.csproj`, lockfile ou manifesto de pacotes alterado | Nenhum `.csproj`, lockfile ou manifesto de pacotes alterado |

Os dois eventos de CI produzem o mesmo nome de job no mesmo app; não são proteções conflitantes. Ambos os checks estão aprovados. As etapas remotas de auditoria de dependências e testes de segurança passaram nesses jobs, conforme a evidência de etapas. A API de secret scanning retornou zero alertas abertos. O endpoint Dependabot respondeu `403`, `Dependabot alerts are disabled for this repository`; code scanning respondeu `404`, `no analysis found`. Portanto, os dois últimos não fornecem uma conclusão de zero vulnerabilidades. Não foram habilitados serviços nem ampliadas permissões para contornar as respostas.

Gitleaks local examinou os 11 commits da PR #2 e os cinco da PR #3, a partir da `main` auditada: zero achados e exit code `0` em ambas as faixas, com saída redigida. Isso é detecção automatizada, sem garantia de ausência de todo segredo. A combinação dos dois tips por `git merge-tree --write-tree --messages` também terminou com código `0`, sem conflitos, gerando somente a árvore local `1088e7251b3d337b1e5de401acebd6a361115639`; refs, índice e arquivos de trabalho foram preservados. Não se criou commit de merge nem se executou CI do conjunto.

Os checks listados cobrem somente os SHAs remotos acima. Qualquer correção local desta rodada, inclusive a validação da imagem reconstruída no CD, exige CI do próximo tip antes de integração. O resultado histórico não aprova arquivos ainda não publicados. Sucesso remoto de cada PR separadamente também não aprova automaticamente a combinação futura.

Sequência preparada, **sem executá-la nesta rodada**:

1. O proprietário decide a proposta exata de política. Aplicá-la somente se houver confirmação específica; depois verificar integralmente os controles. Os drafts continuam exigindo passagem legítima para ready antes de uma integração futura.
2. Com integração futuramente autorizada, integrar [PR #3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3). O SQL A/B/C já executado não deve ser repetido como consequência do merge; CI/CD não executa migrations produtivas.
3. Atualizar a branch da [PR #2](https://github.com/StartingWeb/AndersonMultiMarcas/pull/2) com a `main` resultante e resolver qualquer drift/conflito sem force push; conferir especialmente a evidência compartilhada das duas PRs e as referências de documentação.
4. Executar CI do conjunto e conferir testes SQL, segurança, R2 simulado, deploy/rollback e proveniência; só então integrar a PR #2 sob autorização própria.
5. Exigir CI final de push da `main` resultante e novo pacote do mesmo SHA. Completar R2 isolado, decisão das mídias, paridade e backup restaurável novo antes da futura primeira ativação. Aprovar governança não aprova merge, gates ou deploy.

PRs futuras seguem o mesmo fluxo de PR/checks/branch atualizada/conversas; não dependem de um segundo colaborador. Após a primeira ativação específica e o atendimento dos gates, cada nova `main` testada poderá publicar automaticamente, sem revisão humana por release. Essa automação amplia a consequência de integrar um erro: revisar a PR continua sendo responsabilidade do proprietário.

O aceite temporário de backup apenas local trata outra decisão operacional e não justifica relaxar CI, backups ou proteção. Consultar [pendências](./PENDENCIAS_PRODUCAO.md), [CI/CD](./CI_CD_DEPLOYMENT.md), [ativação](./ATIVACAO_PRODUCAO.md) e [rollback](./ROLLBACK_PRODUCAO.md) para os demais bloqueios. A análise atual está concluída; a aplicação da política aguarda confirmação do delta de três campos acima.
