# Prompt para correções finais e deploy do AndersonMultimarcas

Este texto é uma proposta para a próxima execução. **Ele não foi executado.** Seu envio como nova instrução autoriza o escopo abaixo, inclusive a janela de manutenção e o aceite explícito das mídias preexistentes. Revise essas condições antes de enviá-lo.

---

Execute a finalização da migração e o primeiro deploy controlado do AndersonMultimarcas no Mac e na VPS já configurados, usando o repositório `StartingWeb/AndersonMultiMarcas`.

Leia [a auditoria de 09/11/2026](./AUDITORIA_FINAL_PRODUCAO_2026-11-09.md) e [a evidência consolidada](./evidence/FINAL_AUDIT_2026-11-09.json). Revalide o estado real antes de agir; os SHAs e números abaixo são o baseline auditado, não autorização para ignorar mudanças posteriores.

## Autorizações desta execução

Autorizo:

- Corrigir somente problemas reproduzidos, testar localmente e em ambientes isolados e atualizar a documentação operacional.
- Atualizar as branches de preparação sem force push, retirar draft e integrar as PRs #3 e #2 respeitando proteção/CI.
- Executar CI e CD autorizados, inclusive a reexecução do CD correto para o SHA final, sem alterar a política de proteção.
- Criar backup final preservando as cópias anteriores e validar restauração em SQL isolado com recursos limitados.
- Suspender somente o gatilho de publicação do repositório `StartingWeb/AndersonMultiMarcas-Release`, após conferir execuções ativas, preservando seus arquivos e recursos de rollback.
- Materializar aprovação de paridade com SHA final/digest legado exatos e habilitar os gates GitHub e locais somente quando os pré-requisitos passarem.
- Parar e renomear o container legado do Anderson, preservando o mesmo ID, imagem, mounts e volume, e publicar pelo executor revisado.
- Executar rollback da aplicação se necessário, inclusive retorno manual ao legado preservado quando a falha ocorrer antes do journal.

Aceito, para este corte, manter o estado preexistente das mídias descrito na auditoria: 131 referências de veículos ausentes, sendo somente uma em veículo ativo com outras 18 imagens e capa válidas; oito fotos de vendedores e duas URLs de logos também ausentes. Aceito a diferença conhecida de apresentação dessas referências quebradas, inclusive placeholders e eventual falha de lightbox, como pendência posterior. **Não autoriza apagar referências, inventar imagens, associar arquivos por aproximação ou restaurar objetos produtivos.** Se a revalidação mostrar perda nova de imagens disponíveis ou capa ativa ausente, pare antes do corte e investigue.

Mantenho o aceite temporário registrado de backups exclusivamente na VPS. Não contratar nem configurar destino externo, alterar retenção ou limpar backups nesta execução.

Não autoriza novas migrations ou DML administrativo no banco produtivo, reaplicação de A/B/C, alteração de usuários/senhas/secrets, DNS/Nginx Proxy Manager, reinício de SQL/NPM/outros projetos, prune global, exclusão de volumes/containers legados ou desativação de proteções. Testes de CRUD/upload continuam em ambiente isolado; um teste produtivo que crie dados/objetos exige autorização específica adicional. Valide login produtivo somente com acesso legítimo já disponível, sem resetar conta para obter acesso.

## Baseline que deve ser revalidado

- Main: `1e5612276e0809166a2ea33a21deabee0bf0f768`.
- PR #2: `infra/production-preparation`, `edebcf16a7333602d239160adb09484eaee6a2d0`.
- PR #3: `infra/schema-compatibility-review`, `8116388ca2eaac0ba7e9a0e473c9401b776f8912`.
- Legado vivo: `andersonmultimarcas-web`, ID `9842fa7e0f745dc589ddc9ee230813840524ff65c8aecb3d54cf6eaf7f0c2a78`.
- Imagem legada: `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5`.
- Nome de retenção: `andersonmultimarcas-legacy-fb8a205768dc`, ainda não materializado no baseline.
- Rede/upstream: `startingweb_network`, `andersonmultimarcas-web:8080`.
- Volume DP: `andersonmultimarcas-dataprotection-keys`, application name `AndersonMultiMarcas`.
- Banco: `AndersonMultiMarcas`; runtime `anderson_runtime`, sem privilégios administrativos.
- R2 produtivo: `startingweb-storage`; testes somente em `anderson-multimarcas-homologacao` com credencial isolada.
- Executor e forced command instalados devem coincidir com os arquivos revisados. Não instalar executor divergente silenciosamente.

## Ordem de execução

1. Confira refs/checks/gates, ambiente/proteção, saúde, recursos, baseline Docker, SQL A/B/C e ausência de publicação concorrente. Se os SHAs mudaram, audite a diferença e obtenha CI correspondente. Preserve o arquivo untracked preexistente e os relatórios desta auditoria.
2. Mantenha os gates false durante a integração. Integre #3, atualize #2 com a main resultante sem force push, resolva diferenças e exija CI do conjunto. Integre #2 somente depois dos checks obrigatórios. Não executar os SQLs das PRs em produção.
3. Registre o SHA final de main e os IDs reais de CI push/main e CD/package. Exija build/testes, SQL isolado, runtime da própria imagem final, checksums/proveniência e pacote íntegros. Não aproveitar CI de SHA anterior ou do mero merge-tree.
4. Valide paridade do SHA final, artefato de aplicação e configuração, registrando fluxos aprovados, limites e baseline de imagens aceito. Uma reexecução do CD reconstrói a imagem e pode mudar imageId; exija proveniência e relatório aprovado da imagem efetivamente entregue e revalide diferença relevante não coberta antes de ativar. Não trate aprovação de um imageId como aprovação automática de outro. Faça leituras R2 e teste isolado de escrita/leitura se a evidência estiver vencida ou a integração mudar. Não reutilize os objetos sintéticos retidos de 09/11 como destino de overwrite; limpeza deles permanece fora deste escopo.
5. Execute novo backup imediatamente antes da publicação com preservação das cópias existentes; confira CHECKSUM/VERIFYONLY/SHA/Restic e restaure em ambiente descartável limitado. Exija DBCC/comparação e limpeza somente dos recursos que essa restauração criar. Revalide schema/journal/runtime e o checker fixo em modo de leitura. Não preencher histórico EF nem reaplicar A/B/C.
6. Confira a imagem legada offline, seu SHA, snapshot privado, DP, config futura e roteiro de rollback. Suspenda o workflow Release antigo e prove que não há run ativo. Não executar seu deploy.sh, cujo health usa porta de outro sistema e contém prune global.
7. Apresente uma atualização objetiva com SHA final, CI/pacote, backup/restauração, paridade e estado do legado antes de abrir a janela. Esta instrução já autoriza o corte quando todos esses critérios passarem; não criar aprovação extra por release. Se surgir necessidade fora do escopo, preserve o legado e descreva a pendência concreta.
8. Materialize a paridade formal e os gates, mantendo PR/CI strict, restrição main, ausência de bypass e demais controles. Pare/renomeie somente o legado Anderson para o nome reservado, preservando o ID e mounts.
9. Reexecute o CD já associado ao CI push/main do SHA final com main inalterada, usando o run correto e registrando o novo attempt. CD não possui workflow_dispatch, e apenas mudar variables não dispara deploy. A reexecução integral pode reconstruir a imagem; contabilize isso na janela. Não usar CI workflow_dispatch como fonte, porque o contrato exige evento push.
10. Acompanhe todas as etapas. Antes de um retorno manual ou rollback, impeça uma entrega pendente de disputar o container: cancele o run quando aplicável e confirme que o executor remoto não está em ação concorrente, verificando lock e journal. Não feche gates locais de que o rollback ainda depende. Se package/transferência/preflight falhar antes do journal e o legado já estiver parado, confira que o nome vivo está livre, devolva o mesmo legado ao nome original e inicie-o. Se houver transação/current geridos, use o rollback revisado e confirme o resultado. Não apagar journal nem abrir bypass para forçar avanço.
11. Exija `/health` interno, SHA final em `/health/release` interno/público, HTTPS nos três domínios, catálogo, páginas, capas/galeria e autenticação legítima. Valide CRUD/upload no ambiente isolado; registre explicitamente qualquer limite de autenticação ou escrita produtiva sem declarar que passou. Não envie WhatsApp/e-mail de aplicação como teste.
12. Observe logs/saúde por 30–60 minutos e conserve legado, imagem, configuração e backups. Registre commit, digests, runs, backup/restauração, verificações reais, limitações e caminho de rollback. Mantenha o gatilho legado suspenso e a automação nova habilitada somente se o corte tiver sido confirmado.

## Critérios de retorno

Reserve janela de 10–20 minutos para o primeiro corte. Se a aplicação não ficar saudável e com SHA correto até 20 minutos após parar o legado, ou surgir falha material de login/catálogo/imagens disponíveis, inicie o retorno seguro ao legado e suspenda novas tentativas para diagnóstico. Cancele a entrega pendente e confirme executor/lock/journal antes de manipular o container; o limite de 20 minutos inicia o retorno, sem prometer sua duração.

Rollback da aplicação não reverte banco, migrations ou R2. Preserve A/B/C e gravações legítimas. Restore produtivo exige decisão separada porque pode descartar dados posteriores.

Não declare publicação concluída por código de saída ou success agregado de CD. Exija job production concluído, container/digest corretos, SHA público e verificações reais. Ao final, informe o que está publicado, o que foi testado e qualquer limite restante.
