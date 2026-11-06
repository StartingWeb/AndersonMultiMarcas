# Migração dos repositórios e da publicação

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Repositórios e código

| Papel | Estado confirmado |
| --- | --- |
| Repositório único | [StartingWeb/AndersonMultiMarcas](https://github.com/StartingWeb/AndersonMultiMarcas), main `1e5612276e0809166a2ea33a21deabee0bf0f768`, PR #1 integrada |
| Release antigo | [StartingWeb/AndersonMultiMarcas-Release](https://github.com/StartingWeb/AndersonMultiMarcas-Release), checkout `fb8a205768dce239c960ffb1cfb043b2680de7b3` |
| Preparação | Branch `infra/production-preparation` |
| Correção SQL em revisão | Branch independente `infra/schema-compatibility-review` |

O histórico da recuperação dos 26 commits de publicação fica em [RECUPERACAO_RELEASE.md](./RECUPERACAO_RELEASE.md). O antigo Code foi renomeado para o repositório único; seu histórico foi preservado. Não houve exclusão/arquivamento do Release nem merge automático nesta preparação.

O workflow legado [deploy.yml](https://github.com/StartingWeb/AndersonMultiMarcas-Release/blob/main/.github/workflows/deploy.yml) responde a push main e executa `./deploy.sh` no checkout antigo. Esse script faz fetch/reset, build no servidor e Compose. Seu curl `localhost:8080` alcança outro serviço do host; não o usar para aceitar o Anderson nem para rollback. O workflow antigo continua ativo e só será suspenso na janela de corte autorizada.

## Preservação real

O container atual continua `andersonmultimarcas-web`, ID `9842fa7e0f745dc589ddc9ee230813840524ff65c8aecb3d54cf6eaf7f0c2a78`, iniciado em `2026-10-23T20:43:11.986710244Z`, imagem `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5`. O caminho `/opt/startingweb/apps/anderson-multimarcas` não foi substituído. Seu env-file teve permissão restringida a 0600, sem mudança de conteúdo ou reinício.

O novo destino `/opt/startingweb/apps/anderson-multimarcas-unified` foi criado isoladamente. Contém configuração privada, executor revisado, locks, pacotes recebidos e snapshots. Não contém aprovação de paridade nem habilitação de deploy. O checkout Release, seus uploads, rede, volume DP e configurações NPM permanecem preservados.

O inventário protegido registrou IDs, imagem/digest, rede, aliases, mounts, nomes de variáveis, revisão e proxy. A imagem atual foi exportada para arquivo offline e verificada, junto ao snapshot privado da configuração. Os 20 containers existentes mantiveram IDs/imagens/horários de início/RestartCount; hashes dos dois proxy hosts Anderson permaneceram iguais.

Existe limpeza automática de imagens não usadas e cache de builder no host. Sua política não foi alterada. A recuperação agora dispõe de arquivo fora do armazenamento de imagens Docker; não depende de latest nem de imagem não referenciada sobreviver ao prune.

## Arquitetura de destino

Fonte única → CI/testes/segurança/SQL → artefato e imagem por commit no runner → SSH com host conhecido → executor local bloqueado por gates → health interno/commit e HTTPS público → rollback da aplicação em falha.

O SQL Server compartilhado não será recriado. A aplicação utilizará a mesma rede, endpoint SQL, bucket R2 e DP, com configuração externa. Migrations de produção não fazem parte do deploy; qualquer correção SQL exige decisão, janela e autorização próprias.

O artefato de referência `release-1e5612276e0809166a2ea33a21deabee0bf0f768-42136110621-1` foi ensaiado com a cópia SQL original. A preparação encontrou necessidade de corrigir o transporte SCP e identidade OCI dos scripts; revisar/mergir essas mudanças de forma explícita e gerar novo pacote do commit final antes de habilitar o fluxo automático.

## Condições para transição

1. Revisar as duas branches/PRs, a paridade funcional e o relatório de drift SQL. Decidir tratamento de marcas duplicadas e alterações de capacidade de colunas.
2. Concluir destinatário/e-mail real, revisor independente, proteção de produção, R2 exclusivo de teste e cópia de backup fora da VPS ou decisão operacional explícita sobre essa limitação.
3. Após merge manual aprovado, confirmar CI/CD do SHA final, checksums, imagem, SQL e novo ensaio necessário. Não habilitar gates como consequência automática do merge.
4. Registrar janela, operadores, critérios de aceite e autorização explícita do corte pelo usuário.
5. Só nessa janela suspender workflow Release e demais gatilhos de publicação identificados, garantindo que não haja execução antiga em andamento. Concurrency de um repositório não trava o outro.
6. Confirmar backup recente/restauração, preservar o legado parado sob o nome planejado e liberar nome/upstream de forma controlada. Não excluir o baseline.
7. Publicar e aceitar somente após health/commit, HTTPS, catálogo, login administrativo e imagens. Em falha, voltar a aplicação, mantendo banco e R2.
8. Manter Release, imagem/volume e snapshots pelo período de observação acordado. Arquivamento, revogação de credenciais antigas e remoção de recursos precisam de autorização posterior.

Procedimento completo e ponto de parada: [ATIVACAO_PRODUCAO.md](./ATIVACAO_PRODUCAO.md). Retorno: [ROLLBACK_PRODUCAO.md](./ROLLBACK_PRODUCAO.md).

**Estado final desta preparação:** backup/retorno isolado e infraestrutura preparados; produção segue no Release, automação antiga preservada, gates desligados e pendências explícitas. Não declarar migração produtiva concluída.
