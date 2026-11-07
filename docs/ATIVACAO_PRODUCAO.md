# Primeiro corte — preparação e ponto de parada

Atualizado em 07/11/2026. A instrução vigente desta rodada é **não executar merge, deploy, troca de container/upstream/DNS ou ativação de gates**. A autorização condicional histórica não sobrepõe esse limite. Depois da primeira ativação autorizada, a política desejada é publicação automática da main validada, sem aprovação humana por release.

## Já concluído

A/B/C aplicadas e validadas: 12 ampliações, somente LogoUrl NULL, Marca 4 `Jaguar (cadastro adicional)`, IX_Marca_Nome único. Backup pré-migration restaurado em instância isolada, DBCC e comparações aprovados. Ensaio dos scripts exatos com ambos os binários e retorno ao antigo: 78 checkpoints/nove negativas. Runtime existente validado sem recriação/rotação. [Recibo](./evidence/PRODUCTION_SQL_2026-11-07.json). Não repetir migrations ou alterar dados comerciais.

O [risco temporário de backup exclusivamente local](./ACEITE_RISCO_BACKUP_LOCAL.md) foi aceito. Não solicitar novamente essa decisão nem tratar backup externo como bloqueio técnico obrigatório. Agendamento, criptografia, integridade, restauração, alertas e retenção continuam exigidos. Perda total da VPS pode perder todas as cópias.

## Ponto de parada

- [Política individual](./GITHUB_POLITICA_REPOSITORIO_INDIVIDUAL.md) preparada, ainda sem autorização de aplicação. A regra atual exige revisão independente; a proposta dispensa apenas requisitos humanos incompatíveis com único mantenedor, preservando PR/CI e demais controles. Não usar bypass.
- [R2 isolado](./VALIDACAO_R2.md) depende de autorização para bucket/credencial/política restrita e de ciclo real sintético aprovado. Não usar o bucket produtivo para testes de escrita.
- [Fotos ausentes](./RECUPERACAO_MIDIAS_R2.md) exigem decisão sobre fonte e reparação específica. Nenhum upload/substituição/remoção de URL autorizado.
- Integração legítima das PRs, CI do SHA final e paridade ainda não concluídos. Testes locais e evidência de SHAs anteriores não aprovam um novo commit.

As três variáveis GitHub permanecem false e `shared/deploy.enabled`/`shared/source-parity.approved.json` ausentes. [Pendências](./PENDENCIAS_PRODUCAO.md).

## Ordem futura de integração

1. Sob autorização própria, satisfazer a política vigente, remover draft e integrar PR #3. Scripts SQL já executados permanecem como fonte auditável; não reaplicá-los.
2. Atualizar PR #2 com a main resultante sem force push. Resolver semântica/conflitos e obter CI combinado aprovado no novo tip antes de integrá-la.
3. Exigir CI aprovado do SHA final em main. CD aceita somente push/main do próprio repositório, recusa fonte obsoleta, verifica manifest/checksums, testa a imagem final sem rede e liga o relatório aprovado ao ID exportado. Production continua ignorado com gates false.
4. Concluir R2 real, decisão das mídias, paridade, rollback e janela/operador/monitoramento/critérios de retorno. Não conceder acesso de escrita a terceiros para contornar governança.

## Janela futura — após nova autorização operacional

5. Exigir **novo backup restaurável imediatamente antes do primeiro deploy**: CHECKSUM/VERIFYONLY/SHA-256/Restic, restauração real isolada com DBCC e comparação, recibo/SMTP. Preservar cópias existentes; não basta reutilizar o recibo de 07/11 ou constatar timer ativo.
6. Revalidar schema/journal A/B/C/runtime e preflight somente leitura. Drift exige avaliação; não executar migrations no startup, repetir DML ou reduzir colunas.
7. Revalidar pacote imutável do SHA final, snapshot/configuração/DP/rede, imagem offline do legado e [rollback](./ROLLBACK_PRODUCAO.md). Conferir ausência de publicação concorrente. Suspender gatilho legado apenas no escopo autorizado, sem apagá-lo.
8. Preparar aprovação formal de paridade e gates GitHub/locais apenas sob autorização específica. O gate `PRODUCTION_ADMIN_BYPASS_REVIEWED` significa revisão dos controles, não concessão de bypass. Environment continua restrito à main, sem aprovação por release e sem bypass administrativo.
9. Parar/renomear o legado preservando ID/imagem/mounts e publicar pelo executor revisado. Não remover container antigo ou volume. O executor recusa substituir automaticamente um legado vivo.
10. Exigir health interno, commit exato em `/health/release`, HTTPS público, catálogo/login/mídias e observação. Falha retorna ao container/configuração anteriores pelo journal; nunca reverte banco ou R2 automaticamente. Tráfego NPM só pode mudar conforme a autorização do corte.

## Depois da ativação

Pushes válidos da main passam por CI, pacote, validações e deploy serializado, health/rollback. Não há aprovação humana por release. PR obrigatório/CI/sem force push/exclusão continuam necessários mesmo com único mantenedor. Incidentes preservam snapshots/journal e suspendem novas publicações até diagnóstico.
