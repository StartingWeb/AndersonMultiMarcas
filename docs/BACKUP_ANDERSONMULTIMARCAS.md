# Backup do AndersonMultiMarcas

Atualizado em 07/11/2026, horário de Brasília. A/B/C autorizadas/aplicadas com backup restaurável. Primeiro corte tem autorização condicional, mas segue bloqueado e não executado.

O proprietário confirmou em 07/11/2026 o **aceite temporário de backup exclusivamente local na VPS**, com risco de perda das cópias em caso de perda total do servidor. O [registro formal do aceite](./ACEITE_RISCO_BACKUP_LOCAL.md) preserva todos os controles atuais e não estabelece data de revisão arbitrária. A ausência de cópia externa, isoladamente, não bloqueia a preparação técnica. A decisão não habilita deploy nem autoriza contratação ou transferência externa.

## Resultado comprovado

| Item | Evidência |
| --- | --- |
| Origem | `sqlserver`, SQL Server 2022 Express 16.0.4265.3, banco `AndersonMultiMarcas` ONLINE |
| Recuperação | SIMPLE; compatibilidade 160; MDF 72 MiB e LDF 72 MiB; AUTO_CLOSE existente habilitado, não alterado |
| Último diário comprovado | 07/11/2026 03:00:00–03:00:38 Brasília; run `20261107T060000Z-ab4e6a7b`, 30.072.832 bytes, SHA `9e269b9b8f698334a092a49218f8cb11b7c67b445e3a7cda67e405f794bac400` |
| Pré-migration novo | `20261107T150452Z-c36eef7e`, 12:04:52–12:05:00 Brasília, COPY_ONLY, 30.334.976 bytes |
| SHA-256 pré-migration | `15b714753be43bba0336877b863d86d46c93d8fd90e897f61545ee52df725370` |
| Verificação | CHECKSUM, RESTORE VERIFYONLY, SHA e autenticação aprovados |
| Restic | `28278403757ac1386d8a5c2e15821b200d04d423eaf2eb8a5c0da98a4b24df4b`; restore --verify e check --read-data aprovados |
| Restauração SQL real | Run `46fe30bdb760`, concluída 07/11/2026 12:07:28 Brasília; DBCC/constraints/schema/contagens aprovados na cópia isolada |
| Próximo diário | **08/11/2026 03:00 America/Sao_Paulo**, timer ativo/persistente e habilitado |
| SMTP | Backup e confirmação da restauração aceitos em uma tentativa, DATA 250, certificado validado, TLS 1.3; destinatário startingwebltda@gmail.com |
| Caixa de entrada | Entrega não comprovada |
| Preservação | Três cópias nativas presentes (diários 06/07 e extra); cinco snapshots presentes, nenhum anterior removido nesta rodada |
| Armazenamento/monitor | 65.336.471.552 bytes livres na auditoria de 07/11 12:57; monitor saudável às 12:35; operação somente local com risco aceito |

[Recibo consolidado](./evidence/PRODUCTION_SQL_2026-11-07.json). O campo backup.last_restore retrata o instante anterior ao restore novo; o campo restore e a confirmação SMTP complementar registram a restauração efetiva desta rodada.

[Auditoria operacional de 07/11 às 12:57–12:59](./evidence/BACKUP_AUDIT_2026-11-07.json): leitura por SSH confirmou o diário, timers, retenção e último restore já existente. O helper `deployment-check` revalidou tamanho/SHA nativo, identidade/snapshot Restic e `check --read-data`, retornando `ready`, com contrato de escrita conhecido compatível. Cinco snapshots e três nativos permaneceram idênticos antes/depois. Não houve novo backup, restore, envio SMTP, limpeza ou alteração de configuração/serviços nesta auditoria.

## Arquitetura reutilizada e isolamento

A auditoria identificou **TS Compro Ouro** como o projeto com backup diário por systemd, Restic criptografado, política 7/4/3, restauração, monitor de atraso e Gmail SMTP com STARTTLS. O SistemaSTW tem outro backup por cron; StartingConnect/Evolution usa tar diário. Nenhum desses agendamentos foi modificado.

O crontab root foi inspecionado por leitura restrita e continha somente a chamada ao backup StartingConnect/Evolution, sem referência ao Anderson. Não se encontrou outro agendamento concorrente para este banco. O histórico SQL Anderson mostrava somente o full antigo de julho antes da nova cópia.

O Anderson usa os mesmos componentes já instalados: systemd, Docker, SQL Server, Restic 0.16.4 e o provedor Gmail existente. Possui diretórios, chave de criptografia, repositório Restic, lock, status e timers próprios. Não usa o repositório de backup de outro projeto. Os scripts estão em [backup.py](../scripts/ops/backup.py) e [restore_check.py](../scripts/ops/restore_check.py).

Diretório privado: `/opt/startingweb/backups/anderson-multimarcas`, proprietário `pedro`, modo 0700. Configuração e chave Restic são 0600 e permanecem no servidor. A última cópia nativa está em `/opt/startingweb/backups/anderson-multimarcas/native/20261107T150452Z-c36eef7e/database.bak`; cópias nativas antigas são rotacionadas conforme retenção. O repositório criptografado está em `/opt/startingweb/backups/anderson-multimarcas/repository`; recibos em `state/` e relatórios por execução em `logs/`.

No padrão diário, os dois backups nativos mais recentes são mantidos com proteção de acesso; a execução extra usou --preserve-existing, mantendo todos os anteriores sem mudar a política; esses `.bak` não têm criptografia SQL nativa. As cópias históricas no Restic são criptografadas. Tanto os `.bak` quanto o Restic estão no disco da VPS: **não oferecem recuperação de perda total da VPS**. Esse risco local foi expressamente aceito em 07/11/2026. Cópia protegida fora da VPS continua como melhoria recomendada, dependente de autorização de destino e credenciais; não usar o bucket produtivo de mídias para isso. Não é bloqueio isolado à preparação técnica sob o aceite atual.

## Contrato de cada execução

1. Aplicar umask 0077 e lock exclusivo não bloqueante por projeto.
2. Conferir espaço livre, limite do repositório e sua identidade criptográfica.
3. Criar backup nativo em nome exclusivo `.partial`, com CHECKSUM. Não usar compressão nativa, indisponível na edição Express.
4. Executar VERIFYONLY com checksum, copiar para staging privado, fsync e calcular SHA-256.
5. Guardar manifesto protegido; enviar ao Restic inicialmente como candidato.
6. Restaurar esse snapshot com autenticação, conferir novamente SHA-256 e ler integralmente os dados do repositório.
7. Marcar o snapshot como verificado e publicar a cópia nativa em diretório final. Somente então atualizar `last-valid.json`.
8. Aplicar retenção validada; remover somente os temporários desta execução no SQL e manter os dois `.bak` mais recentes confirmados.
9. Registrar tamanho, duração, verificação, armazenamento e estado. Enviar o relatório imediatamente após as verificações, se houver destinatário autorizado.

Falhas ficam em staging com `failure.json`, sem serem anunciadas como cópias válidas. Saídas SQL/SMTP potencialmente sensíveis não são impressas. Nenhuma rotina modifica schema ou dados da origem.

O histórico anterior apresentava somente um full de julho, sem checksum, e nenhuma cadeia diferencial/log Anderson identificada. A cópia inicial usou COPY_ONLY para preservar qualquer base operacional anterior. O timer diário executa full normal com CHECKSUM; não usa COPY_ONLY indiscriminadamente. Mudanças futuras no plano de recuperação exigem revisar essa decisão.

## Agendamento, retenção e monitoramento

Os units de [deploy/systemd/](../deploy/systemd/) foram instalados em `~pedro/.config/systemd/user/`. `anderson-backup.timer` está habilitado/ativo, com `OnCalendar=*-*-* 03:00:00 America/Sao_Paulo`, `Persistent=true`, precisão de um segundo e sem atraso aleatório. `loginctl show-user pedro` confirmou `Linger=yes`, permitindo execução após reboot sem uma sessão SSH aberta. Não há cron Anderson concorrente.

`anderson-backup-monitor.timer` verifica de hora em hora, aos 35 minutos, a idade do backup, existência do snapshot verificado, identidade do repositório, espaço e notificação. O limite de atraso é 27 horas; relógio divergente também alerta. O alerta é deduplicado pela identidade do incidente até recuperação. Uma falha já notificada pelo backup não provoca outro e-mail pelo monitor para a mesma execução. Tentativas sem aceite ficam registradas e podem ser retomadas pelo monitor. O estado atual é saudável. Monitor na própria VPS não detecta sozinho perda total do host.

Retenção Restic: **7 diários, 4 semanais, 3 mensais**. O plano `forget --dry-run` precisa conservar o snapshot mais recente e cobrir exatamente os snapshots verificados do Anderson. A leitura autenticada precede qualquer remoção. A chave é exclusiva, não foi copiada para o Git nem para GitHub secrets.

Havia cerca de 69 GB livres na auditoria inicial; o backup tem cerca de 30 MB. Foi aplicado limite de repositório de 8 GiB e reserva mínima de disco de 2 GiB, além de margem proporcional ao banco. Se faltar capacidade, a execução falha preservando cópias anteriores; não remove backups de outros projetos para liberar espaço.

## E-mail configurado e testado

Somente o `recipient` do arquivo privado Anderson foi configurado como `startingwebltda@gmail.com`, de forma idempotente, preservando modo 0600. Nenhum destinatário de outro projeto foi alterado.

O SMTP é reutilizado de `/etc/tscomproouro-backup/config.json`, sem copiar senha ou criar outro serviço. O helper Docker não tem rede nem logs e monta a árvore SMTP somente para leitura. A senha permanece em memória. O envio usa Gmail/STARTTLS, certificado validado, TLS mínimo 1.2 e timeout de 15 segundos. Há até três tentativas com backoff e histórico sanitizado. Falha permanente de autenticação não é repetida. Se a conexão cair durante DATA, registra `delivery_unknown` sem repetir um envio de resultado ambíguo. Falha após aceite ou no QUIT não duplica a mensagem.

Dois backups controlados foram enviados: às 23:19 e às 23:22 de 05/11/2026 (Brasília), ambos aceitos na primeira tentativa com DATA 250/TLS 1.3. O segundo validou o relatório final com estado e espaço do armazenamento. Não houve anexos nem confirmação de entrega na caixa de entrada.

Assuntos implementados:

- `[AndersonMultiMarcas] Backup diário concluído com sucesso`
- `[AndersonMultiMarcas] ALERTA — Falha no backup diário`

O corpo informa sistema, banco, servidor, horário de Brasília, início/fim, duração, tamanho, verificação, manifesto SHA-256 protegido, destino lógico, último backup válido, última restauração e status. Não inclui anexos, registros, senhas, conexões ou caminhos sensíveis. `accepted_by_smtp` significa aceite do provedor, **não entrega na caixa de entrada**. O teste real foi aceito pelo provedor. Os recibos privados registram resultado e histórico de tentativas sem senha ou resposta textual SMTP.

## Operação e restauração periódica

Consultas seguras no servidor:

```sh
systemctl --user list-timers 'anderson*' --all
systemctl --user status anderson-backup.timer anderson-backup-monitor.timer
python3 -I -B /opt/startingweb/backups/anderson-multimarcas/bin/backup.py status
```

Backup manual autorizado, sem migrations:

```sh
systemctl --user start anderson-backup.service
```

Realizar restauração real **mensalmente e antes de uma publicação com alteração de schema**, em janela de recursos disponível. VERIFYONLY diário não substitui esse teste. A rotina de [restore_check.py](../scripts/ops/restore_check.py) exige pacote/SQL revisados, mínimo de memória disponível, instância nova, rede interna, volumes novos e labels exclusivos. Aplica migrations somente na segunda cópia; preserva a primeira para comparação e leitura da aplicação. Todo recurso removido tem sua label de execução conferida. Nenhum dado real saiu da VPS.

O comando de ensaio e os critérios de aceite estão em [VALIDACAO_SCHEMA_PRODUCAO.md](./VALIDACAO_SCHEMA_PRODUCAO.md). Restauração do banco produtivo exige autorização própria e avaliação de perda de gravações posteriores. Os scripts de deploy/rollback nunca restauram banco.

## Bloqueio de publicação por backup/schema

`backup.py deployment-check` é uma operação de leitura sob o mesmo lock do backup: exige sucesso, idade entre zero e 27 horas, restauração SQL verificada/DBCC de até 35 dias, tamanho/SHA do nativo, snapshot verificado e leitura autenticada integral do Restic. Em seguida consulta somente metadados SQL para os 12 limites/nulabilidade de LogoUrl e índice único de Marca.Nome conhecidos pela auditoria. Recusa incompatibilidade; não executa DDL/DML/migrations. Esse contrato conhecido não substitui revisão de paridade ou novos testes após mudanças no modelo.

O executor instalado chama esse helper fixo antes de qualquer ativação. Rollback de aplicação dispensa esse preflight para permitir recuperação durante incidentes de backup. O backup operacional reutiliza o acesso administrativo já existente no container SQL; a aplicação usa a configuração runtime privada, cujos privilégios devem ser revistos antes da ativação. A preparação anterior não criou credenciais. Nesta rodada foi instalado o runtime dedicado conforme autorização, sem mudar a identidade do legado.

## Histórico da revalidação de 06/11/2026

Timers de backup e monitor enabled/active, configuração 0600, destinatário e fuso preservados. A execução diária de 06/11 às 03:00 ainda estava no futuro durante a preparação; não se esperou artificialmente nem se enviou outro teste SMTP. O ensaio cruzado usou a última cópia válida e restaurou somente instância/volumes novos, com DBCC e limpeza aprovados. A rotina agendada e seu recibo operacional não foram substituídos pelos relatórios de compatibilidade.

A configuração TS Compro Ouro reutilizada foi reauditada sem copiar credenciais: repositório local, sem destino externo autorizado identificado. [Preparação da segunda cópia](./BACKUP_EXTERNO_PROPOSTA.md). Nenhum snapshot/dado real saiu da VPS e nenhuma cobrança/serviço novo foi criado. O contrato externo continua disabled e não foi instalado.

## Histórico: execução agendada comprovada em 06/11/2026

O timer executou de fato às 03:00:00.722334, encerrando às 03:00:44.006428 (Brasília). Run `20261106T060000Z-ae701b0b`, 43,28 segundos, 29.941.760 bytes; SHA-256 `3b70dfbc209964d4c606499957c4f22b4facc24815375888a02a62d802396a32`, VERIFYONLY WITH CHECKSUM aprovado. Snapshot Restic `e1eab760351046e4aadc2ea32a231c1c84be8a21de4ca4018a93d46ff2b9bfa2`. Recibo de SMTP: aceito na primeira tentativa, DATA 250, TLS 1.3 e certificado validado, sem prova de entrega na caixa de entrada.

Nesta rodada foi reconferido o hash do nativo, a presença/identidade do snapshot e o Restic check/read-data, sem criar backup duplicado, enviar outro e-mail ou excluir cópias. Retenção 7/4/3 e monitor saudável. Próximo timer: **07/11/2026 03:00 America/Sao_Paulo (06:00 UTC)**, com Persistent e linger preservados. Esta evidência substitui a indicação histórica de que a execução de hoje ainda estaria no futuro.

O backup adicional pré-SQL foi concluído/restaurado em 07/11 conforme recibo acima. Criar outro backup restaurável imediatamente antes do corte futuro; o backup desta janela não é autorização permanente para publicar. Segunda cópia externa continua dependendo de destino autorizado; nenhum dado foi transferido para destino novo.

## Revalidação operacional e aceite em 07/11/2026

O diário de 07/11 executou às 03:00:00.766526 e concluiu às 03:00:38.601179 (Brasília); o serviço terminou às 03:00:45, com `Result=success` e código zero. O próximo disparo confirmado é **08/11/2026 03:00 America/Sao_Paulo (06:00 UTC)**. O host usa Etc/UTC com NTP sincronizado; os calendários systemd declaram explicitamente America/Sao_Paulo. Os timers de backup e monitor seguem enabled/active, `Persistent=yes`, `Linger=yes`.

O último monitor salvo às 12:35:36 apontou `healthy`, snapshot verificado presente e notificação aceita, sem arquivo de incidente pendente. O próximo monitor era 13:35 de Brasília no instante auditado. Não se chamou a operação `monitor`, que pode gravar status ou emitir alerta, nem se injetou falha. Diário e extra registram SMTP aceito em uma tentativa com DATA 250, certificado validado e TLS 1.3; o aceite não comprova caixa de entrada.

O último restore real permanece `46fe30bdb760`, concluído às 12:07:28, com DBCC, restauração nativa e integridade referencial aprovados, zero violações e comparação da cópia com a origem aprovada. Seu campo `schema_compatibility_status=review_required` é histórico da cópia pré-A/B/C comparada ao modelo futuro; não se deve interpretar essa cópia como novo restore posterior às alterações. O checker de leitura atual retornou contrato conhecido compatível, e continua necessário novo backup restaurável antes do futuro primeiro deploy.

Foram verificados somente metadados do banco Anderson: ONLINE, login runtime habilitado, usuário correspondente presente e 23 registros no histórico EF. Não se repetiram A/B/C, provisionamento de usuário, rotação de credenciais ou gravação comercial. As configurações/chave mantêm modo 0600 e diretórios privados 0700. Não se leu configuração SMTP nem conteúdo de chave. As cinco identidades de snapshots verificados e as três cópias nativas foram comparadas antes/depois, sem qualquer exclusão nesta auditoria.

A operação temporária somente local está formalizada no [aceite de risco](./ACEITE_RISCO_BACKUP_LOCAL.md). Agendamento, verificações, restauração, criptografia Restic, retenção 7/4/3 e relatórios/alertas permanecem exigidos; nenhuma data de revisão foi inventada. A melhoria externa continua recomendada e requer decisão própria, sem bloquear sozinha a preparação técnica.

## Backup extra sem exclusão e recuperação

Para uma nova janela autorizada, a opção explícita mantém todas as cópias existentes, sem executar forget/prune ou rotação nativa nessa execução. O diário continua com retenção 7/4/3 e duas cópias nativas.

```sh
python3 -I -B /opt/startingweb/backups/anderson-multimarcas/bin/backup.py backup --initial --preserve-existing
```

Antes da recuperação, conferir manifesto/SHA do nativo ou autenticar o snapshot com chave privada local, restaurar em diretório 0700 e reconferir SHA. Fazer RESTORE VERIFYONLY e restaurar primeiro em instância isolada com nomes físicos novos; executar DBCC/constraints e comparar schema/contagens. O caminho privado e o run ID acima identificam o backup pré-A/B/C; recuperá-lo em produção pode descartar gravações posteriores. Restauração produtiva exige decisão própria, nunca é parte automática do rollback. Nenhum dado real foi enviado para outro destino.

O checker instalado foi corrigido para selecionar o banco com sqlcmd -d, sem mensagem de USE no escalar; teste de regressão e execução real retornam ready. A cópia anterior do helper foi preservada. Timer, recipient, credenciais, repositório e demais projetos não foram alterados.
