# Primeiro corte — autorização condicional e ponto de parada

Atualizado em 06/11/2026. O usuário autorizou o primeiro deploy **somente depois** de confirmar o novo nome da Marca 4, resolver dependências obrigatórias e satisfazer checks/política de revisão. Essa autorização não precisa ser solicitada novamente para o mesmo escopo quando todas as condições forem cumpridas. Não é autorização para contornar qualquer uma delas. Não haverá aprovação humana por release depois da ativação.

## Executado

Runtime `anderson_runtime` e conexão do destino futuro instalados, com senha privada 0600, permissões mínimas e acesso efetivo verificado. O SQL compartilhado não foi reiniciado; somente a segurança autorizada desse runtime foi acrescentada. Conexão/identidade do legado preservadas. Backup real de 06/11 às 03h concluído com SHA/VERIFYONLY/Restic e aceite SMTP; timers/monitor saudáveis. Nenhum deploy, merge, alteração comercial ou migration A/B/C foi executado.

## Ponto de parada atual

- Nome proposto **Jaguar (cadastro adicional)** para o ID 4; falta confirmação explícita. ID 1 permanece Jaguar; veículos 1/0; zero exclusões e zero relações alteradas.
- A preserva nulabilidade por padrão. Falta decidir a exceção específica de LogoUrl, necessária ao contrato atual do modelo/preflight.
- Falta destino R2 de homologação previamente autorizado com isolamento comprovado e ciclo sintético real. Não usar prefixo arbitrário ou mídias de clientes.
- Falta destino externo protegido autorizado, ou decisão explícita sobre essa limitação antes do corte. Não transferir dados nem contratar serviço por inferência.
- Falta revisor independente elegível. Main exige um aprovador, aprovação do último push por outro ator, code owner aplicável, conversas resolvidas e CI validate strict; admins não têm bypass. Definir o mantenedor autorizado também no CODEOWNERS da PR #2. Não convidar usuários nem alterar proteções sem autorização.

Referência completa: [PENDENCIAS_PRODUCAO.md](./PENDENCIAS_PRODUCAO.md). Os três gates GitHub continuam false e os arquivos locais de habilitação/paridade estão ausentes.

## Ordem de integração e preparação

1. Satisfazer legitimamente a revisão/checks da PR #3 e integrá-la; nenhuma integração foi feita nesta rodada. Ela contém A/B/C produtivos candidatos e os testes SQL, separados por responsabilidade.
2. Atualizar PR #2 com a main resultante sem force push, conferir conflitos/semântica e CI do SHA combinado. Integrar somente após nova revisão/checks da política vigente. Nenhum merge direto ou bypass.
3. Gerar CI aprovado e pacote imutável do SHA final em main; verificar proveniência, SHA, ciRunId, arquitetura e identidade da imagem. CD pode empacotar com gates false; production deve continuar ignorado até o corte.
4. Conferir R2 real, nome/nulabilidade aprovados, permissões runtime, backup externo/limitação deliberada, paridade formal e [rollback](./ROLLBACK_PRODUCAO.md). Registrar janela, operador, monitoramento e critérios de retorno.

## Janela do primeiro corte — somente com todas as condições satisfeitas

5. Executar um backup adicional pelo serviço Anderson e aguardar seu recibo completo: SHA, VERIFYONLY, Restic autenticado e status SMTP. Registrar o recibo imediatamente antes de qualquer SQL comercial/corte; não tratar timer como prova de execução.
6. Revalidar identidade imutável do container SQL, nome da instância, GUID do banco, checksums dos SQLs e estado dos IDs 1/4. Aplicar somente as versões A → B → C revisadas/aprovadas, com os contextos específicos; sem migrations automáticas. Recusar nomes/referências/índices inesperados. Conferir journal, IDs/demais campos/FKs/dados, unicidade e preflight depois da execução.
7. Revalidar pacote/configuração/volume DP/rede, imagem offline e snapshot do legado. Preservar o publicador antigo até a janela e confirmar que não há outra execução concorrente. Suspender seu gatilho conforme a autorização do corte, sem apagá-lo.
8. Apenas então preparar o bootstrap do executor: preservar/parar/renomear o legado para o nome reservado, criar aprovação local de paridade e habilitação e habilitar as três variáveis conforme o SHA/escopo aceitos. Não alterar NPM/DNS para esconder falhas.
9. Acionar o CD de um CI de push main aprovado e ainda atual (por exemplo, rerun do CD correspondente). Source/proveniência são reconferidos; não publicar de branch de preparação ou executar pacote local sem a mesma cadeia. O Environment é automático, sem aprovadores por execução.
10. Conferir health interno, `/health/release` com commit exato, HTTPS público, catálogo/contato/admin/imagens e persistência DP. Falha interna/externa exige rollback automático e confirmação do retorno; falha de rollback preserva journal e exige recuperação do operador. Não restaurar banco, reduzir colunas ou desfazer nomes automaticamente.
11. Registrar imagem/commit/pacote/recibos, resultado, horários e eventual retorno. Observar antes de retirar o legado/publicador; essa retirada definitiva não está autorizada por esta preparação.

O bootstrap pode causar indisponibilidade enquanto o nome do container é trocado; não se promete zero downtime. O executor continua recusando o container legado vivo/não gerido, mesmo se alguém preparar gates indevidamente. Os deploys seguintes serão automáticos após atualizações válidas da main, serializados por concorrência e lock, sem aprovação manual por publicação.
