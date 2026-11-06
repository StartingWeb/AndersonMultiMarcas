# Backup do AndersonMultiMarcas

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Resultado comprovado

| Item | Evidência |
| --- | --- |
| Origem | `sqlserver`, SQL Server 2022 Express 16.0.4265.3, banco `AndersonMultiMarcas` ONLINE |
| Recuperação | SIMPLE; compatibilidade 160; MDF 72 MiB e LDF 72 MiB; AUTO_CLOSE existente habilitado, não alterado |
| Backup inicial | `2026-11-05T21:38:24.922235-03:00` até `2026-11-05T21:38:51.266851-03:00` |
| Duração | 26.34 segundos |
| Tamanho | 30,400,512 bytes, 30,40 MB |
| SHA-256 | `3e9e3cbebc2eed2c60dfd669f271e70aed3971a288d531c721dc0b283c237119` |
| Verificação | `RESTORE VERIFYONLY WITH CHECKSUM, STOP_ON_ERROR`: aprovado |
| Restic | Snapshot autenticado restaurado com `--verify`, SHA-256 reconferido e `check --read-data` aprovado |
| Restauração SQL real | Aprovada em instância isolada; `DBCC CHECKDB` e constraints aprovados |
| Comparação | Schema e contagens da cópia iguais à origem; nenhum registro real foi exibido |
| Aplicação | .NET 10 funcionou sobre a cópia original, sem migrations e com usuário SQL somente de leitura |
| Última restauração completa | `2026-11-05T22:14:57.850389-03:00`, execução `a2483165e38b` |
| Próximo backup | **06/11/2026 às 03:00 America/Sao_Paulo**, equivalente observado a 06:00 UTC |
| E-mail | Transporte preparado; destinatário autorizado ainda pendente; nenhum e-mail Anderson foi enviado |

## Arquitetura reutilizada e isolamento

A auditoria identificou **TS Compro Ouro** como o projeto com backup diário por systemd, Restic criptografado, política 7/4/3, restauração, monitor de atraso e Gmail SMTP com STARTTLS. O SistemaSTW tem outro backup por cron; StartingConnect/Evolution usa tar diário. Nenhum desses agendamentos foi modificado.

O crontab root foi inspecionado por leitura restrita e continha somente a chamada ao backup StartingConnect/Evolution, sem referência ao Anderson. Não se encontrou outro agendamento concorrente para este banco. O histórico SQL Anderson mostrava somente o full antigo de julho antes da nova cópia.

O Anderson usa os mesmos componentes já instalados: systemd, Docker, SQL Server, Restic 0.16.4 e o provedor Gmail existente. Possui diretórios, chave de criptografia, repositório Restic, lock, status e timers próprios. Não usa o repositório de backup de outro projeto. Os scripts estão em [backup.py](../scripts/ops/backup.py) e [restore_check.py](../scripts/ops/restore_check.py).

Diretório privado: `/opt/startingweb/backups/anderson-multimarcas`, proprietário `pedro`, modo 0700. Configuração e chave Restic são 0600 e permanecem no servidor. A cópia nativa inicial está em `/opt/startingweb/backups/anderson-multimarcas/native/20261106T003824Z-51435d6a/database.bak`. O repositório criptografado está em `/opt/startingweb/backups/anderson-multimarcas/repository`; recibos em `state/` e relatórios por execução em `logs/`.

Os dois backups nativos mais recentes são mantidos com proteção de acesso; esses `.bak` não têm criptografia SQL nativa. As cópias históricas no Restic são criptografadas. Tanto os `.bak` quanto o Restic estão no disco da VPS: **não oferecem recuperação de perda total da VPS**. Nenhum destino externo já autorizado foi encontrado no mecanismo reutilizado. Cópia protegida fora da VPS continua pendente; não usar o bucket produtivo de mídias para isso.

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

`anderson-backup-monitor.timer` verifica de hora em hora, aos 35 minutos, a idade do backup, existência do snapshot verificado, identidade do repositório, espaço e notificação. O limite de atraso é 27 horas; relógio divergente também alerta. A notificação de alerta é limitada a uma por dia até recuperação. O estado atual aponta alerta por destinatário pendente, embora o backup e o snapshot estejam válidos. Monitor na própria VPS não detecta sozinho perda total do host.

Retenção Restic: **7 diários, 4 semanais, 3 mensais**. O plano `forget --dry-run` precisa conservar o snapshot mais recente e cobrir exatamente os snapshots verificados do Anderson. A leitura autenticada precede qualquer remoção. A chave é exclusiva, não foi copiada para o Git nem para GitHub secrets.

Havia cerca de 69 GB livres na auditoria inicial; o backup tem cerca de 30 MB. Foi aplicado limite de repositório de 8 GiB e reserva mínima de disco de 2 GiB, além de margem proporcional ao banco. Se faltar capacidade, a execução falha preservando cópias anteriores; não remove backups de outros projetos para liberar espaço.

## E-mail: pendência explícita

A configuração SMTP é reutilizada de `/etc/tscomproouro-backup/config.json`, sem duplicar serviço ou copiar sua senha. Um helper Docker sem rede, sem logs e com mount somente de leitura acessa apenas essa árvore privada; a senha permanece em memória no envio. Gmail exige STARTTLS validado, TLS mínimo 1.2, senha de aplicativo e timeout de 15 segundos. Há até três tentativas, com backoff; recusa permanente de autenticação não é repetida. Falha após aceite DATA ou no QUIT não duplica mensagens.

O destinatário Anderson não estava definido. O campo `recipient` permanece nulo. Não se reutilizou automaticamente o destinatário de outro projeto. Após o usuário indicar o endereço, configurar somente o arquivo privado e realizar novo backup manual controlado para testar o envio real.

Assuntos implementados:

- `[AndersonMultiMarcas] Backup diário concluído com sucesso`
- `[AndersonMultiMarcas] ALERTA — Falha no backup diário`

O corpo informa sistema, banco, servidor, horário de Brasília, início/fim, duração, tamanho, verificação, manifesto SHA-256 protegido, destino lógico, último backup válido, última restauração e status. Não inclui anexos, registros, senhas, conexões ou caminhos sensíveis. `accepted_by_smtp` significa aceite do provedor, **não entrega na caixa de entrada**. O teste real segue pendente de destinatário; os testes automatizados de formato, falha e duplicação passaram.

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
