# Backup do AndersonMultiMarcas

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Resultado comprovado

| Item | Evidência |
| --- | --- |
| Origem | `sqlserver`, SQL Server 2022 Express 16.0.4265.3, banco `AndersonMultiMarcas` ONLINE |
| Recuperação | SIMPLE; compatibilidade 160; MDF 72 MiB e LDF 72 MiB; AUTO_CLOSE existente habilitado, não alterado |
| Último backup controlado | `2026-11-05T23:22:25.723668-03:00` até `2026-11-05T23:22:54.024640-03:00` |
| Duração / tamanho | 28,30 segundos / 30.007.296 bytes |
| SHA-256 | `a566d73fa4526ea9a940c8c2ea0805422e2963cdd61df0a9ca96af2c2ab19ecf` |
| Verificação | `RESTORE VERIFYONLY WITH CHECKSUM, STOP_ON_ERROR`: aprovado |
| Restic | Snapshot `d1cb75d9bc24553cc0ccad91b3ab169be8e95c78ea9f6c1436a8b927fa264747`, restaurado com `--verify`, SHA reconferido e `check --read-data` aprovado |
| Restauração SQL real | `2026-11-05T22:14:57.850389-03:00`, execução `a2483165e38b`; DBCC CHECKDB/constraints e comparação aprovados na instância isolada |
| Próximo backup | **06/11/2026 às 03:00 America/Sao_Paulo**, 06:00 UTC |
| E-mail | `startingwebltda@gmail.com`; Gmail aceitou em uma tentativa, resposta DATA 250, certificado validado e TLS 1.3 |
| Entrega na caixa de entrada | Não comprovada; SMTP não fornece confirmação de entrega neste mecanismo |
| Armazenamento | Disponível; 71.687.589.888 bytes livres; somente VPS, sem cópia externa autorizada |
| Monitor | Saudável após configuração e envio; timers habilitados/persistentes |

## Arquitetura reutilizada e isolamento

A auditoria identificou **TS Compro Ouro** como o projeto com backup diário por systemd, Restic criptografado, política 7/4/3, restauração, monitor de atraso e Gmail SMTP com STARTTLS. O SistemaSTW tem outro backup por cron; StartingConnect/Evolution usa tar diário. Nenhum desses agendamentos foi modificado.

O crontab root foi inspecionado por leitura restrita e continha somente a chamada ao backup StartingConnect/Evolution, sem referência ao Anderson. Não se encontrou outro agendamento concorrente para este banco. O histórico SQL Anderson mostrava somente o full antigo de julho antes da nova cópia.

O Anderson usa os mesmos componentes já instalados: systemd, Docker, SQL Server, Restic 0.16.4 e o provedor Gmail existente. Possui diretórios, chave de criptografia, repositório Restic, lock, status e timers próprios. Não usa o repositório de backup de outro projeto. Os scripts estão em [backup.py](../scripts/ops/backup.py) e [restore_check.py](../scripts/ops/restore_check.py).

Diretório privado: `/opt/startingweb/backups/anderson-multimarcas`, proprietário `pedro`, modo 0700. Configuração e chave Restic são 0600 e permanecem no servidor. A última cópia nativa está em `/opt/startingweb/backups/anderson-multimarcas/native/20261106T022225Z-6bb31c59/database.bak`; cópias nativas antigas são rotacionadas conforme retenção. O repositório criptografado está em `/opt/startingweb/backups/anderson-multimarcas/repository`; recibos em `state/` e relatórios por execução em `logs/`.

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

O executor instalado chama esse helper fixo antes de qualquer ativação. Rollback de aplicação dispensa esse preflight para permitir recuperação durante incidentes de backup. O backup operacional reutiliza o acesso administrativo já existente no container SQL; a aplicação usa a configuração runtime privada, cujos privilégios devem ser revistos antes da ativação. Não se criaram novas credenciais de banco.
