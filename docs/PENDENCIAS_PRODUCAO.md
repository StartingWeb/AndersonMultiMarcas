# Estado de produção — 07/11/2026

**Política individual do GitHub aplicada e R2 isolado homologado sob autorização expressa. Nenhum merge, deploy ou alteração de objetos produtivos.** Gates GitHub false; arquivos locais de habilitação/paridade ausentes. A aplicação permanece no legado.

## Decisões confirmadas e estado preservado

- A/B/C já foram aplicadas: 12 ampliações, LogoUrl NULL, Marca 4 `Jaguar (cadastro adicional)` e índice único. Histórico EF preservado, DBCC e ensaio com 78 checkpoints/nove negativas aprovados. Não repetir migrations, recriar runtime, rotacionar credenciais ou modificar dados comerciais. [Recibo SQL](./evidence/PRODUCTION_SQL_2026-11-07.json).
- [Backup exclusivamente local aceito temporariamente](./ACEITE_RISCO_BACKUP_LOCAL.md), inclusive risco de perda dos backups com a VPS. Não é mais uma decisão pendente nem equivale a disaster recovery externo. Sem contratação, transferência, redução de retenção ou limpeza de snapshots.
- Diário de 07/11 às 03h concluído; próximo 08/11 às 03h America/Sao_Paulo. Timers/monitor ativos, retenção 7/4/3, integridade verificada e aceite SMTP comprovado. [Auditoria de backup](./BACKUP_ANDERSONMULTIMARCAS.md).
- Legado e SQL permanecem ativos; runtime existente preservado. Não houve troca de upstream, container, DNS ou gate. [Auditoria de pipeline](./evidence/PIPELINE_AUDIT_2026-11-07.json).

## Etapas concluídas e pendência de mídias

1. **Política GitHub individual aplicada:** quantidade 0, code owner false e último push false; comparação integral confirmou PR/validate strict app 15368/admins/conversas/force push/exclusão e todos os demais controles preservados. [Antes/depois](./GITHUB_POLITICA_REPOSITORIO_INDIVIDUAL.md).
2. **R2 isolado homologado:** dois buckets autorizados criados, token somente no primeiro, oito negativas AccessDenied, upload/HEAD/download/hash/sobrescrita/exclusão/limpeza aprovados e HTTPS r2.dev validado. Controle negativo privado/vazio. Zero objetos produtivos alterados. [Recibo e limites](./VALIDACAO_R2.md).
3. **Mídias ausentes ainda pendentes:** obter os oito originais não recuperados e autorizar eventual reparação por objeto após validar correspondência/hash/formato/dimensões/ausência de colisão. Não apagar URLs, criar imagens fictícias ou atribuir imagens de terceiros. [Inventário](./RECUPERACAO_MIDIAS_R2.md).

## Preparação técnica desta rodada

O CD reconstruía a imagem após o CI sem testar esse novo resultado. A correção executa o ensaio de runtime na imagem final sem rede/SQL/R2, exige relatório completo com mesmo commit e ID imutável, verifica usuário não root e arquiva a evidência. O empacotamento falha antes de exportar uma imagem com evidência ausente, incompleta ou divergente. Não muda gates, secrets ou executor produtivo.

Testes de CI/segurança, operação, integridade/proveniência e rollback permanecem obrigatórios. O ciclo real R2 foi aprovado; o CI do futuro SHA integrado e a paridade continuam necessários. A homologação sintética e a simulação das branches não aprovam o conjunto integrado. [CI/CD](./CI_CD_DEPLOYMENT.md).

## PRs e sequência futura

Main revalidada: `1e5612276e0809166a2ea33a21deabee0bf0f768`.

- [PR #3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3): aberta/draft, tip auditado `8116388ca2eaac0ba7e9a0e473c9401b776f8912`, CI aprovado, sem conflito com main. Contém scripts/recibos das migrations já executadas, não uma ordem de reaplicação.
- [PR #2](https://github.com/StartingWeb/AndersonMultiMarcas/pull/2): aberta/draft, tip auditado antes destes recibos `32ffadfa3ab78a8cbed148fff5ea541d03e041cc`, CI aprovado. Esta rodada prepara correções/documentação na mesma branch; exigir CI do novo tip, sem aproveitar aprovação do anterior.
- A simulação `git merge-tree` das duas referências auditadas resultou sem conflitos. Ela não integrou branches nem executou CI combinado.

Somente em rodada autorizada: satisfazer a política vigente e retirar draft → PR #3 → atualizar PR #2 com main resultante, sem force push → CI combinado → PR #2 → CI final main → pacote com proveniência e runtime final aprovados → decisão/reparação autorizada das mídias e paridade → novo backup restaurável imediatamente antes do primeiro corte → autorização operacional e gates. Nada dessa sequência implica autorização de merge/deploy nesta rodada.

[Ativação](./ATIVACAO_PRODUCAO.md) e [rollback](./ROLLBACK_PRODUCAO.md) descrevem o corte futuro; não criar `deploy.enabled` nem `source-parity.approved.json` antecipadamente.

## Produção e backups revalidados após a homologação

[Comparação operacional](./evidence/POLICY_R2_OPERATIONS_2026-11-07.json): mesmos IDs/imagens/início/restarts do legado, SQL e NPM; site e /health 200, /health/release 404 esperado no legado. Timers/monitor ativos, retenção 7/4/3 e último snapshot/restore preservados; gates false/ausentes. Sem comandos SQL, restart, forget/prune, cópia externa ou mudança de alertas nesta rodada.
