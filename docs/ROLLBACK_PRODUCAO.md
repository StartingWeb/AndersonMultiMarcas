# Rollback da aplicação em produção

Data: 2026-11-05. O mecanismo foi preparado localmente. **Não executar estes procedimentos em produção sem autorização explícita.** Nenhum rollback, restart ou alteração de banco foi realizado nesta atividade.

## Escopo e limites

Rollback restaura a imagem e a configuração da aplicação anterior. Não desfaz SQL, alterações de dados, importações de mídia ou objetos R2. Uma restauração de banco tem impacto próprio, pode descartar gravações posteriores ao backup e exige autorização e procedimento específicos.

A publicação só deve admitir mudanças de schema compatíveis com a versão anterior. Se esse requisito não puder ser demonstrado, interromper antes da publicação ou preparar um plano separado de recuperação. O executor não tenta tornar um schema incompatível seguro ao voltar a imagem.

O estado saudável de `/health` significa liveness. A confirmação do retorno exige também `/health/release`, identidade esperada quando disponível, HTTPS público e validação operacional dos fluxos pertinentes.

## Baseline legado da auditoria histórica

Antes da primeira release do novo mecanismo, reconfirmar os valores observados abaixo. O retorno precisa funcionar mesmo sem uma release unificada anterior:

| Identificador | Valor observado |
| --- | --- |
| Container antigo | `andersonmultimarcas-web` |
| Imagem | `startingweb/andersonmultimarcas-release:latest` |
| Digest atual | `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5` |
| Commit Release | `fb8a205768dce239c960ffb1cfb043b2680de7b3` |
| Diretório | `/opt/startingweb/apps/anderson-multimarcas` |
| Rede | `startingweb_network` |
| Upstream do proxy | `andersonmultimarcas-web:8080` |
| Volume DP | `andersonmultimarcas-dataprotection-keys` |

Hashes de assemblies do checkout e do container foram correlacionados:

| Arquivo | SHA-256 |
| --- | --- |
| Project.dll | `d5785c1a5fda6c94473c0a90ea181cd37d87b839db6fe25f08dd9cef56695a8f` |
| Core.dll | `c5b73f63e3c619f779f59ce9210c28f81a73cb4ec1c185d69331ac95288d31c3` |
| Data.dll | `9f0ee59eff1755afdb284313d4c4442dcd985b9d78f0a1e9929d8cc03fbba51f` |
| Domain.dll | `351cc864850c26042435010565068716c5c150e03aa43f987cdbb5877a1a0c13` |

Não confiar exclusivamente na tag `latest` nem no SHA de fonte embutido nas DLLs. Os 26 commits posteriores de publicação não têm commits de fonte correspondentes; a matriz de recuperação em [RECUPERACAO_RELEASE.md](./RECUPERACAO_RELEASE.md) registra a reconciliação local. Reter a imagem pelo conteúdo e o pacote offline, junto à configuração correspondente.

## Preparação obrigatória antes do corte

1. Registrar imagem, container, rede, aliases, mounts e revisão publicada; fazer backup protegido da configuração real.
2. Guardar a imagem legada em arquivo offline verificado. A limpeza diária do host pode remover imagens não usadas; um container retido e um arquivo protegido reduzem esse risco.
3. Preservar o checkout e o `deploy.sh` não rastreado, sem executar seu fetch/reset ou build para retornar a um baseline específico.
4. Preservar o volume Data Protection e o nome da aplicação. Não gerar chaves novas nem trocar o volume durante rollback.
5. Preservar mídias locais e R2. O diretório de uploads do host não é um volume da instalação atual; verificar sua função antes de qualquer cópia.
6. Dispor de backup recente do banco correto e restauração testada isoladamente; o rollback de aplicação não usa esse backup automaticamente.
7. Suspender os mecanismos legados somente com autorização de corte, para que não iniciem deploy concorrente durante a recuperação.
8. Garantir que o operador conhece o alvo de retorno e o health esperado. Não considerar `localhost:8080` do host como health do Anderson; essa porta pertence a outro projeto.
9. Antes de habilitar publicações, conferir revisores, prevenção de autoaprovação e regra main do Environment `production`, além de verificar manualmente na interface que o bypass por administradores está desabilitado. A variável `PRODUCTION_ADMIN_BYPASS_REVIEWED=true` é uma declaração dessa revisão humana; o GET Environment não comprova esse estado. Manter as três variáveis de ativação desligadas até a autorização final e reconferir regras alteradas antes de reativar o pipeline após um incidente.

## Rollback automático preparado

O executor [release.py](../scripts/deploy/release.py) valida o pacote e o contrato do servidor antes de assumir a operação. Container existente sem identidade do novo mecanismo é recusado; isso impede que um primeiro deploy se aproprie silenciosamente do legado.

Na primeira ativação, o corte manual autorizado deve reter o container legado e registrar sua referência no contrato do servidor. Se a primeira release falhar após a troca, o mecanismo restaura esse container e seu upstream. A presença e a recuperabilidade do legado precisam ser verificadas antes de iniciar.

Em releases posteriores, o executor preserva imagem e snapshot de configuração da aplicação anterior. Uma falha de inicialização, health ou identidade do commit deve causar retorno à versão anterior e registro do incidente no histórico, sem apagar os pacotes envolvidos. O script usa lock para impedir duas mutações simultâneas.

Se o retorno automático também falhar, a operação deve terminar como falha e demandar intervenção; não marcar deploy como sucesso nem executar migrations para tentar reparar. Guardar os detalhes sanitizados e preservar os estados para diagnóstico.

## Rollback manual futuro

Após autorização, o operador deve:

1. Confirmar que não há outra publicação em andamento e suspender novos gatilhos enquanto houver incidente.
2. Consultar o histórico e escolher uma release local com pacote, imagem e configuração recuperáveis.
3. Confirmar compatibilidade do banco atual com a release escolhida e integridade dos arquivos.
4. Executar o comando de rollback com o executor revisado, no host correto.
5. Conferir a versão que efetivamente ficou ativa, health interno, proxy, HTTPS e fluxos principais.
6. Registrar o incidente, commit retirado, commit restaurado, horário, resultado e responsável, sem segredos ou dados de clientes.

Comandos preparados, não executados em produção:

```sh
# Retorno ao alvo anterior registrado pelo mecanismo.
python3 scripts/deploy/release.py rollback \
  --root /opt/startingweb/apps/anderson-multimarcas-unified

# Retorno a uma release específica já instalada e preservada.
python3 scripts/deploy/release.py rollback \
  --root /opt/startingweb/apps/anderson-multimarcas-unified \
  --release SHA_COMPLETO_DA_RELEASE
```

Usar o caminho efetivo do executor aprovado no servidor. Não escolher um SHA só porque existe no Git: o alvo precisa ter o pacote e a configuração preservados localmente. Não executar `docker image prune`, `docker compose down -v` ou remoções de releases como parte da recuperação.

Se existe uma transação interrompida, `rollback` sem `--release` recupera o baseline registrado no journal antes de permitir outra publicação. Esse baseline pode ser o legado, que não tem `/health/release`: validá-lo por imagem, hashes/revisão registrados e health apropriado; não inventar um SHA da fonte para o binário antigo.

Depois de uma **primeira publicação concluída com sucesso interno**, ainda não há release unificada anterior em `current.json`. Nesse estado, `rollback` sem alvo restaura o container legado explicitamente retido e confere seu ID de imagem contra a aprovação de paridade antes da troca. Isso permite o retorno automático se a validação HTTPS pública reprovar após o health interno. O legado precisa permanecer parado e recuperável com o nome configurado. Se a tentativa de restaurá-lo falhar, o executor tenta recuperar a aplicação gerida e mantém um journal para intervenção caso a recuperação também falhe.

A partir da segunda publicação bem-sucedida, o alvo anterior gerido está disponível para o comando de rollback. O procedimento manual abaixo fica reservado a situações em que o estado ou os recursos exigidos pelo executor não permitem recuperação normal.

## Recuperação de emergência do legado

Se o novo estado estiver indisponível ou corrompido e o executor não conseguir realizar o retorno:

1. Interromper novas tentativas e preservar evidências, sem imprimir arquivos `.env` ou logs potencialmente sensíveis.
2. Identificar o container legado retido pelo inventário do corte, seu digest e os mounts originais.
3. Planejar a liberação do nome/alias do container novo e a reativação do antigo, preservando a rede e o volume DP. Executar apenas com autorização operacional.
4. Confirmar o upstream do NPM e os domínios. Não modificar o proxy se a restauração do nome/alias original resolver o encaminhamento.
5. Se a imagem legada estiver ausente, carregar o arquivo offline previamente verificado e reconstruir somente a configuração documentada, sem novo build e sem atualizar o checkout Release.
6. Se o banco estiver incompatível, parar e acionar o plano específico de banco; não realizar restore ou downgrade por conta própria.

O script antigo de deploy não é um mecanismo seguro de rollback: ele busca a main corrente, reconstrói a imagem, não fixa a revisão e seu curl usa a porta errada do host.

## Retenção e aceitação

Manter a release ativa, ao menos um baseline anterior conhecido e o legado inicial pelo período acordado. A quantidade e o prazo finais precisam considerar espaço em disco, dados sensíveis nos snapshots, auditoria e a política de prune existente. Proteger os snapshots de configuração como segredos, mesmo quando ficam fora do Git.

Antes de considerar o novo fluxo operacional, testar isoladamente:

- Pacote válido e identidade do commit correto.
- Checksum inválido, manifesto inconsistente e caminhos maliciosos recusados antes da troca.
- Ausência de autorização, configuração insegura e tentativa de assumir container não gerido recusadas.
- Sucesso de publicação e histórico persistido.
- Falha de health da aplicação nova com recuperação da versão anterior.
- Falha na primeira publicação com retorno ao legado retido.
- Retorno manual a release preservada e falha explícita para alvo inexistente.
- Configuração/volume DP preservados e nenhum comando de banco executado.

Foram executados **32 testes unitários do executor**, **18 testes dos helpers CI/entrega** e **nove cenários com Docker real local**: deploy válido; falhas de inicialização e HTTP503 com recuperação automática; imagem inválida; pacote adulterado; configuração com migrations habilitadas; transferência interrompida; rollback manual por arquivo; retorno ao mesmo container legado simulado. Falha de SSH/SCP foi injetada na fronteira de processo, sem contato remoto, com verificação posterior da aplicação no Docker real. Todos os recursos tinham prefixo/label exclusivo e a ausência de containers, redes, volumes e tags foi conferida ao final. Os testes cobrem resposta pública inválida, solicitação de rollback e destruição das chaves temporárias. Veja resultados/comando em [CI_CD_DEPLOYMENT.md](./CI_CD_DEPLOYMENT.md). O smoke usa uma aplicação HTTP descartável: não comprova paridade do Anderson nem integração com NPM/SQL/R2 de produção. A validação operacional com proxy real, cópia restaurada do banco e configuração de R2 permanece pendente para uma etapa autorizada; o ensaio isolado não habilita a publicação.
