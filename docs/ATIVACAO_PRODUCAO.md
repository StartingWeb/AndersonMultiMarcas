# Primeiro corte — autorização condicional e ponto de parada

Atualizado em 06/11/2026. O usuário autorizou o primeiro deploy **somente depois** de confirmar o novo nome da Marca 4, resolver dependências obrigatórias e satisfazer checks/política de revisão. Essa autorização não precisa ser solicitada novamente para o mesmo escopo quando todas as condições forem cumpridas. Não é autorização para contornar qualquer uma delas. Não haverá aprovação humana por release depois da ativação.

## Executado em 07/11/2026

A/B/C aplicadas e validadas sob autorização explícita: 12 ampliações, somente LogoUrl NULL, Marca 4 Jaguar (cadastro adicional), IX_Marca_Nome único. Novo backup COPY_ONLY/CHECKSUM/VERIFYONLY/Restic restaurado em instância isolada, DBCC e comparações aprovados. Ensaio dos scripts exatos com ambos os binários e retorno ao antigo: 78 checkpoints, nove negativas, limpeza aprovada. Runtime existente validado sem recriação/rotação. Legado healthy e domínios HTTPS 200, nenhum restart/corte/merge. [Recibo](./evidence/PRODUCTION_SQL_2026-11-07.json).

## Ponto de parada atual

- Falta R2 de homologação expressamente autorizado, isolado e validado por ciclo real sintético.
- Falta destino externo de backup autorizado e validado, ou aceitação explícita do risco temporário de perda total da VPS.
- Falta revisor independente elegível. Só StartingWeb tem acesso; definir mantenedor autorizado e CODEOWNERS aplicável, obter revisão real do último push. Não conceder acesso por inferência, convidar ou usar bypass.
- Faltam integração legítima/CI do SHA final e paridade comprovada. Nome e nulabilidade já foram aprovados e executados; não pedir novamente.

Referência completa: [PENDENCIAS_PRODUCAO.md](./PENDENCIAS_PRODUCAO.md). Os três gates GitHub continuam false e os arquivos locais de habilitação/paridade estão ausentes.

## Ordem de integração e preparação

1. Satisfazer legitimamente a revisão/checks da PR #3 e integrá-la; nenhuma integração foi feita nesta rodada. Ela contém A/B/C já executadas e seus recibos/testes, separados por responsabilidade.
2. Atualizar PR #2 com a main resultante sem force push, conferir conflitos/semântica e CI do SHA combinado. Integrar somente após nova revisão/checks da política vigente. Nenhum merge direto ou bypass.
3. Gerar CI aprovado e pacote imutável do SHA final em main; verificar proveniência, SHA, ciRunId, arquitetura e identidade da imagem. CD pode empacotar com gates false; production deve continuar ignorado até o corte.
4. Conferir R2 real, recibos A/B/C, permissões runtime, backup externo/limitação deliberada, paridade formal e [rollback](./ROLLBACK_PRODUCAO.md). Registrar janela, operador, monitoramento e critérios de retorno.

## Janela do primeiro corte — somente com todas as condições satisfeitas

5. Executar um backup adicional pelo serviço Anderson e aguardar seu recibo completo: SHA, VERIFYONLY, Restic autenticado e status SMTP. Registrar o recibo imediatamente antes de qualquer SQL comercial/corte; não tratar timer como prova de execução.
6. Revalidar identidade imutável do SQL, journal A/B/C e contrato atual; as migrations já foram aplicadas em 07/11. Não repetir DML nem usar propostas de ensaio. Qualquer drift requer avaliação; não executar migrations no startup. Confirmar preflight/DBCC/backup e contagens atuais.
7. Revalidar pacote/configuração/volume DP/rede, imagem offline e snapshot do legado. Preservar o publicador antigo até a janela e confirmar que não há outra execução concorrente. Suspender seu gatilho conforme a autorização do corte, sem apagá-lo.
8. Apenas então preparar o bootstrap do executor: preservar/parar/renomear o legado para o nome reservado, criar aprovação local de paridade e habilitação e habilitar as três variáveis conforme o SHA/escopo aceitos. Não alterar NPM/DNS para esconder falhas.
9. Acionar o CD de um CI de push main aprovado e ainda atual (por exemplo, rerun do CD correspondente). Source/proveniência são reconferidos; não publicar de branch de preparação ou executar pacote local sem a mesma cadeia. O Environment é automático, sem aprovadores por execução.
10. Conferir health interno, `/health/release` com commit exato, HTTPS público, catálogo/contato/admin/imagens e persistência DP. Falha interna/externa exige rollback automático e confirmação do retorno; falha de rollback preserva journal e exige recuperação do operador. Não restaurar banco, reduzir colunas ou desfazer nomes automaticamente.
11. Registrar imagem/commit/pacote/recibos, resultado, horários e eventual retorno. Observar antes de retirar o legado/publicador; essa retirada definitiva não está autorizada por esta preparação.

O bootstrap pode causar indisponibilidade enquanto o nome do container é trocado; não se promete zero downtime. O executor continua recusando o container legado vivo/não gerido, mesmo se alguém preparar gates indevidamente. Os deploys seguintes serão automáticos após atualizações válidas da main, serializados por concorrência e lock, sem aprovação manual por publicação.
