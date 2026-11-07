# Estado de produção — 07/11/2026

**Rodada de auditoria/preparação: nenhum merge, deploy, alteração de proteção GitHub ou escrita no R2 produtivo.** Gates GitHub false; arquivos locais de habilitação/paridade ausentes. A aplicação permanece no legado.

## Decisões confirmadas e estado preservado

- A/B/C já foram aplicadas: 12 ampliações, LogoUrl NULL, Marca 4 `Jaguar (cadastro adicional)` e índice único. Histórico EF preservado, DBCC e ensaio com 78 checkpoints/nove negativas aprovados. Não repetir migrations, recriar runtime, rotacionar credenciais ou modificar dados comerciais. [Recibo SQL](./evidence/PRODUCTION_SQL_2026-11-07.json).
- [Backup exclusivamente local aceito temporariamente](./ACEITE_RISCO_BACKUP_LOCAL.md), inclusive risco de perda dos backups com a VPS. Não é mais uma decisão pendente nem equivale a disaster recovery externo. Sem contratação, transferência, redução de retenção ou limpeza de snapshots.
- Diário de 07/11 às 03h concluído; próximo 08/11 às 03h America/Sao_Paulo. Timers/monitor ativos, retenção 7/4/3, integridade verificada e aceite SMTP comprovado. [Auditoria de backup](./BACKUP_ANDERSONMULTIMARCAS.md).
- Legado e SQL permanecem ativos; runtime existente preservado. Não houve troca de upstream, container, DNS ou gate. [Auditoria de pipeline](./evidence/PIPELINE_AUDIT_2026-11-07.json).

## Aprovações ainda necessárias

1. **Política GitHub individual:** aprovar os três valores propostos de review: quantidade 1→0, code owner true→false e último push true→false. Manter PR obrigatório, `validate` strict associado ao GitHub Actions, admins sujeitos às regras, conversas resolvidas e proibição de force push/exclusão. CODEOWNERS permanece no conteúdo da PR #2. [Valores exatos, payload, riscos e reversão](./GITHUB_POLITICA_REPOSITORIO_INDIVIDUAL.md). Nenhuma regra foi modificada. Se a proposta não for aprovada, a alternativa é definir outro revisor elegível autorizado.
2. **Homologação R2 isolada:** autorizar os buckets sintéticos `anderson-multimarcas-homologacao` e `anderson-multimarcas-homologacao-negativa` (controle vazio privado), token Object Read & Write limitado exclusivamente ao primeiro, sem acesso ao controle ou produção, e URL de teste exclusiva. Um prefixo com credenciais amplas não basta. Não criar recursos remotos nem executar PUT/DELETE até existir destino autorizado e isolamento comprovado. [Configuração e ciclo de testes](./VALIDACAO_R2.md).
3. **Mídias ausentes:** decidir a origem dos arquivos não recuperados e autorizar eventual restauração por objeto, depois de validar correspondência/hash/formato/dimensões/ausência de colisão. Não apagar URLs nem atribuir imagens de terceiros. [Inventário e fontes pesquisadas](./RECUPERACAO_MIDIAS_R2.md).

## Preparação técnica desta rodada

O CD reconstruía a imagem após o CI sem testar esse novo resultado. A correção executa o ensaio de runtime na imagem final sem rede/SQL/R2, exige relatório completo com mesmo commit e ID imutável, verifica usuário não root e arquiva a evidência. O empacotamento falha antes de exportar uma imagem com evidência ausente, incompleta ou divergente. Não muda gates, secrets ou executor produtivo.

Testes de CI/segurança, operação, integridade/proveniência e rollback permanecem obrigatórios. O ciclo real R2 e o CI do futuro SHA integrado continuam necessários; mocks e a simulação de combinação das branches não os substituem. [CI/CD](./CI_CD_DEPLOYMENT.md).

## PRs e sequência futura

Main revalidada: `1e5612276e0809166a2ea33a21deabee0bf0f768`.

- [PR #3](https://github.com/StartingWeb/AndersonMultiMarcas/pull/3): aberta/draft, tip auditado `8116388ca2eaac0ba7e9a0e473c9401b776f8912`, CI aprovado, sem conflito com main. Contém scripts/recibos das migrations já executadas, não uma ordem de reaplicação.
- [PR #2](https://github.com/StartingWeb/AndersonMultiMarcas/pull/2): aberta/draft, tip inicial auditado `3f319d4de6cdc287fa9b2730bb413ac54b1be518`, CI aprovado. Esta rodada prepara correções/documentação na mesma branch; exigir CI do novo tip, sem aproveitar aprovação do anterior.
- A simulação `git merge-tree` das duas referências auditadas resultou sem conflitos. Ela não integrou branches nem executou CI combinado.

Somente em rodada autorizada: satisfazer a política vigente e retirar draft → PR #3 → atualizar PR #2 com main resultante, sem force push → CI combinado → PR #2 → CI final main → pacote com proveniência e runtime final aprovados → homologação R2/mídias/paridade → novo backup restaurável imediatamente antes do primeiro corte → autorização operacional e gates. Nada dessa sequência implica autorização de merge/deploy nesta rodada.

[Ativação](./ATIVACAO_PRODUCAO.md) e [rollback](./ROLLBACK_PRODUCAO.md) descrevem o corte futuro; não criar `deploy.enabled` nem `source-parity.approved.json` antecipadamente.
