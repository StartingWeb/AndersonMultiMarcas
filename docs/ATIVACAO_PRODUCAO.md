# Primeira publicação controlada

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Identidade do ensaio e ponto de parada

Commit de referência: `1e5612276e0809166a2ea33a21deabee0bf0f768`. CI [42135590822](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42135590822), CD [42136110621](https://github.com/StartingWeb/AndersonMultiMarcas/actions/runs/42136110621), artefato `release-1e5612276e0809166a2ea33a21deabee0bf0f768-42136110621-1`, não expirado na auditoria. Os hashes estão em [CI_CD_DEPLOYMENT.md](./CI_CD_DEPLOYMENT.md).

Esse artefato foi usado para o ensaio real de banco/aplicação. A branch de infraestrutura corrige SCP restrito, verificação OCI e proteções. **Antes da publicação por Actions, revisar e integrar as correções manualmente, obter CI/CD e novo artefato do SHA resultante e reapresentar esse SHA exato para aprovação.** A preparação não aprova merge nem corte.

**PARADA ATUAL: faltam destinatário/teste SMTP, revisor independente, decisão de schema, R2 de teste, proteção fora da VPS e autorização explícita. Não iniciar o corte com esta lista aberta.** O plano não presume uma janela nem inventa aprovadores.

## Checklist anterior à autorização

1. Confirmar backup do banco correto com idade adequada à janela, SHA/VERIFYONLY/snapshot existente. Registrar o último válido, sem imprimir configurações.
2. Confirmar restauração recente com DBCC CHECKDB, constraints, comparação e aplicação sobre cópia sem migrations. Refazer após mudança relevante.
3. Aprovar [VALIDACAO_SCHEMA_PRODUCAO.md](./VALIDACAO_SCHEMA_PRODUCAO.md) e a proposta da branch SQL separada. Resolver a decisão sobre duplicidade e limites antes de aceitar gravações. Qualquer SQL produtivo exige autorização própria; o deploy nunca o executa.
4. Aprovar [VALIDACAO_R2.md](./VALIDACAO_R2.md), incluindo ciclo sintético em bucket exclusivo e avaliação das referências legadas. Não testar gravações em bucket produtivo.
5. Conferir imagem legada, SHA do arquivo e snapshot privado recuperável; manter container, configurações, rede, aliases e volume DP.
6. Conferir [ROLLBACK_PRODUCAO.md](./ROLLBACK_PRODUCAO.md), operador e critérios objetivos para voltar à versão antiga.
7. Conferir Environment: reviewer independente, prevent_self_review, main-only, can_admins_bypass=false, cinco secrets, known hosts e política main. Não confundir variável de autorização com proteção GitHub real.
8. Confirmar CI do commit final, todos os testes/segurança aprovados, SQL revisado e source parity formalmente aceita. Conferir que main ainda aponta ao SHA autorizado.
9. Confirmar artefato, origem, ciRunId, arquitetura linux/amd64, configurações/rootfs, SHA do bundle e do executor instalado.
10. Definir explicitamente janela de manutenção, responsáveis, testes sem criação indiscriminada de dados e período de observação/retenção. Apresentar ao usuário SHA, artefato, backup, ensaio, riscos e plano de retorno para autorização explícita.

## SOMENTE DEPOIS DA AUTORIZAÇÃO DO CORTE

Os itens seguintes **não foram executados**:

11. Suspender de forma controlada o workflow `Deploy Production` do Release e outros gatilhos identificados; registrar estado anterior. Verificar ausência de execução antiga em andamento. Não excluir/arquivar o repositório.
12. Reconfirmar backup/health e preservar o legado. Na janela, parar e renomear o mesmo container para `andersonmultimarcas-legacy-fb8a205768dc`, conferindo imagem/DP/rede. Preparar arquivos locais de aprovação e habilitação e as três Repository variables somente conforme autorização registrada. Publicar exclusivamente o pacote do SHA aprovado, com aprovação real do Environment.
13. Conferir `/health` e `/health/release` dentro do novo container; o commit precisa coincidir com o manifesto. Falha exige rollback, não migração improvisada.
14. Conferir home/catálogo/filtros/detalhe e comportamento de dados legados. Evitar operações de escrita que não façam parte do roteiro aprovado.
15. Conferir login administrativo com operador autorizado e permissões/menu; não expor credenciais em logs ou relatório.
16. Conferir imagens/otimização e referências legadas no aceite operacional aprovado. Qualquer escrita R2 produtiva precisa respeitar o escopo autorizado desse aceite.
17. Conferir HTTPS e encaminhamento nos três domínios, certificado e ausência de redirect inesperado em health. NPM deve continuar no mesmo upstream; não alterar proxy para esconder falha da release.
18. Observar logs sanitizados, erros SQL/R2, reinícios, recursos e sessões/DP durante o período acordado.
19. Se falhar health, commit, HTTPS, catálogo, autenticação ou persistência, suspender novas tentativas e voltar à aplicação anterior pelo executor/journal. Não reverter banco automaticamente. Confirmar retorno e preservar evidências.
20. Registrar resultado, SHA/imagem/artefato, horários, aprovação, validações e eventual rollback. Só após estabilização planejar retirada do mecanismo antigo, com autorização separada.

## Critérios de recusa

Recusar o início com backup não restaurável, integridade incerta, reviewer ausente, bypass habilitado/desconhecido, artefato obsoleto, diferença de imagem/configuração, schema de escrita não aprovado, R2 sem validação ou baseline de rollback não recuperável. Não resolver recusas habilitando migrations no startup, removendo o container antigo, desabilitando proteção GitHub ou aceitando apenas liveness.

O primeiro corte pode produzir indisponibilidade durante troca do nome/upstream. Não se promete zero downtime. A parada do legado, suspensão de seu workflow e habilitação de gates fazem parte do corte autorizado, não desta preparação.

## Estado que deve continuar até nova autorização

`andersonmultimarcas-web` segue com a imagem legada `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5` e início `2026-10-23T20:43:11.986710244Z`. SQL compartilhado e NPM não foram reiniciados. Os três gates GitHub estão false; arquivos locais de ativação/aprovação estão ausentes. A nova aplicação só foi iniciada em containers isolados, sem acesso à origem produtiva.
