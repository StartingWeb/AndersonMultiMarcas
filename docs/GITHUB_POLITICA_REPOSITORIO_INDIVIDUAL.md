# Política GitHub para mantenedor individual

Aplicada em **07/11/2026 às 16:26:44 UTC (13:26:44 de Brasília)**, sob autorização expressa do proprietário. Consultas REST e GraphQL confirmaram o resultado. Nenhum merge, deploy, alteração da main, conversão de draft, mudança de CODEOWNERS ou ativação de gates.

O [recibo antes/depois](./evidence/GITHUB_POLICY_APPLIED_2026-11-07.json) contém o payload efetivo, proteção integral e controles administrativos preservados. A [auditoria anterior](./evidence/GITHUB_AUDIT_2026-11-07.json) é histórica; sua proposta já foi autorizada e executada nesta rodada.

## Alteração mínima executada

Usada a [API oficial de proteção de reviews](https://docs.github.com/en/rest/branches/branch-protection#update-pull-request-review-protection), com somente três campos:

```http
PATCH /repos/StartingWeb/AndersonMultiMarcas/branches/main/protection/required_pull_request_reviews
```

```json
{
  "required_approving_review_count": 0,
  "require_code_owner_reviews": false,
  "require_last_push_approval": false
}
```

| Controle | Antes | Depois |
| --- | --- | --- |
| Aprovações obrigatórias | 1 | **0** |
| Code owner obrigatório | true | **false** |
| Aprovação do último push | true | **false** |
| PR obrigatória | Objeto REST presente; requiresApprovingReviews=true | Preservada; mesmo objeto e flag |
| Descartar aprovações antigas | true | true |
| Required status check | validate, GitHub Actions app_id=15368 | Idêntico |
| Branch atualizada / strict | true | true |
| Conversas resolvidas | true | true |
| Aplicação a administradores | true | true |
| Force push / exclusão | false / false | false / false |
| Bypass de PR / force push | Zero atores | Zero atores |
| Lista adicional de restrição de push | Ausente; restrictsPushes=false | Idêntica; PR obrigatória preservada |
| Histórico linear / assinatura | false / false | Idênticos |
| lock_branch / block_creations / allow_fork_syncing | false / false / false | Idênticos |
| Rulesets adicionais/herdados | [] | [] |
| main | 1e5612276e0809166a2ea33a21deabee0bf0f768 | Mesmo SHA; protegida |

O PATCH omitiu dismiss_stale_reviews; a releitura confirmou true. A comparação do objeto integral da proteção aceitou exatamente o delta autorizado, sem necessidade de restauração. Nenhuma proteção inteira foi substituída.

## Governança verificada sem integrar alterações

GraphQL confirmou requiresApprovingReviews=true, requiredApprovingReviewCount=0, revisão de code owner desativada, conversas/checks/strict/admins ativos e nenhuma allowance de bypass. A PR #2 retornou reviewDecision=null, sem exigir outro revisor. As duas PRs continuam abertas e draft.

A avaliação não destrutiva dos controles exige bloquear validate pendente/falhando, branch desatualizada e conversa não resolvida; permite a condição de revisão humana com zero aprovações quando os outros critérios forem atendidos. Isso é conferência da política efetiva por API, **não teste de merge real nem dry-run nativo do GitHub**. Não se injetou check falhando ou conversa artificial. Não se tentou push direto. O draft permanece um impedimento próprio, mesmo quando mergeStateStatus=CLEAN.

## Environment, secrets e workflows preservados

Environment production: can_admins_bypass=false, custom_branch_policies=true, protected_branches=false, única policy main do tipo branch. Sem reviewers/wait timer por execução. Names/updated_at dos cinco secrets SSH e a variável de health permaneceram iguais; valores dos secrets não foram lidos. Nenhum secret novo de R2 foi enviado ao GitHub.

PRODUCTION_DEPLOY_ENABLED, PRODUCTION_SOURCE_PARITY_CONFIRMED e PRODUCTION_ADMIN_BYPASS_REVIEWED permanecem false. No servidor, shared/deploy.enabled e shared/source-parity.approved.json continuam ausentes. O nome do último gate não concede bypass.

Permissões padrão de Actions read; can_approve_pull_request_reviews=false. CI contents: read; CD contents: read/actions: read. Workflows, colaboradores e permissões não foram alterados. O CI obrigatório continua associado ao app 15368. [CODEOWNERS](../.github/CODEOWNERS) permanece versionado na branch da PR #2, sem alteração de conteúdo ou mapeamentos; ainda não existe na main legada.

## PRs e CI conferidos antes da publicação desta documentação

| PR | SHA auditado | CI pull_request | Resultado |
| --- | --- | --- | --- |
| [#2](https://github.com/StartingWeb/AndersonMultiMarcas/pull/2) | 32ffadfa3ab78a8cbed148fff5ea541d03e041cc | [42390020907](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42390020907) | 193 .NET e 111 Python aprovados |
| [#3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3) | 8116388ca2eaac0ba7e9a0e473c9401b776f8912 | [42386554511](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42386554511) | 187 .NET e 59 Python aprovados |

Cada check corresponde ao SHA indicado. A publicação desta documentação na PR #2 gera outro tip e requer o CI desse novo SHA; não herda aprovação dos runs acima. Integração futura exige main atualizada e CI combinado. Não se declarou testado o conjunto integrado.

## Risco e reversão

A política dispensa revisão independente obrigatória, inclusive de mudanças em infraestrutura/segurança. O único mantenedor continua responsável pela revisão do diff. Permanecem PR auditável, CI obrigatório, strict, conversas resolvidas, admins sujeitos às regras, bloqueio de force push/exclusão, ações fixadas por SHA, permissões mínimas e gates. CI verde não garante ausência de defeitos ou vulnerabilidades.

Reversão técnica: PATCH no mesmo endpoint com quantidade 1, code owner true e último push true, preservando os demais campos. Isso volta a exigir revisão independente e pode bloquear o mantenedor único. Uma reversão operacional futura deve respeitar a autorização aplicável; não desfaz merges, banco ou mídia.

A política individual está concluída. Os bloqueios restantes são integração autorizada, CI final, mídias, paridade e novo backup restaurável. [Pendências](./PENDENCIAS_PRODUCAO.md), [CI/CD](./CI_CD_DEPLOYMENT.md), [ativação](./ATIVACAO_PRODUCAO.md).
