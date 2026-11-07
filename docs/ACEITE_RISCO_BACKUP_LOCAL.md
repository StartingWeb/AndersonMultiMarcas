# Aceite temporário de backup exclusivamente local

Decisão confirmada pelo proprietário em **07/11/2026**, durante a finalização da preparação do AndersonMultiMarcas para CI/CD. Este registro documenta uma decisão já autorizada; não pede nova aprovação.

## Decisão e risco aceito

O AndersonMultiMarcas pode operar temporariamente com backups armazenados exclusivamente na VPS `startingweb-server`. A perda total, indisponibilidade irrecuperável ou comprometimento destrutivo da VPS pode atingir o banco e todas as cópias locais, inclusive o repositório Restic. Backup local íntegro não equivale a recuperação de desastre fora do servidor.

A ausência de segunda cópia externa, isoladamente, não bloqueia a preparação técnica desta rodada. O aceite não habilita publicação nem substitui os demais critérios de primeiro deploy. Nenhum armazenamento externo, segunda VPS, contratação, cobrança ou transferência de dados a outro provedor foi autorizado ou executado nesta rodada.

## Controles preservados

- Backup diário às **03:00, America/Sao_Paulo**, com timer persistente e execução independente da sessão SSH.
- SQL CHECKSUM, RESTORE VERIFYONLY WITH CHECKSUM, SHA-256 e recuperação autenticada pelo Restic criptografado com chave exclusiva.
- Restauração SQL isolada e periódica, com DBCC CHECKDB, integridade referencial e comparação estrutural/contagens. Manter a rotina mensal e antes de publicação com alteração de schema.
- Retenção **7 diários, 4 semanais e 3 mensais**, monitor de atraso, alertas e relatórios ao e-mail operacional `startingwebltda@gmail.com`.
- Preservação das cópias existentes. Nenhum `forget`, `prune`, rotação manual, backup duplicado ou novo teste SMTP foi executado nesta auditoria. A política do diário continua vigente.
- Arquivos privados e chave permanecem protegidos no servidor; nenhum segredo foi registrado na documentação.

Antes do futuro primeiro deploy, exigir **novo backup restaurável**, mesmo que o backup pré-migration de 07/11 permaneça íntegro. Este aceite não autoriza reduzir frequência, criptografia, verificações, restauração, retenção ou monitoramento.

## Evidência operacional em 07/11/2026

Auditoria por SSH, somente leitura operacional, às 12:57–12:59 de Brasília:

| Controle | Resultado |
| --- | --- |
| Último diário | `20261107T060000Z-ab4e6a7b`, 03:00:00–03:00:38, sucesso; unidade terminou 03:00:45, código 0 |
| Próximo diário | 08/11/2026 03:00 America/Sao_Paulo, correspondente a 06:00 UTC |
| Timer/monitor | Ambos enabled/active/persistentes; linger=yes; monitor saudável às 12:35:36, sem incidente pendente |
| Último backup válido | Extra pré-migration `20261107T150452Z-c36eef7e`, 12:04:52–12:05:00; COPY_ONLY e preservação explícita de cópias anteriores |
| Integridade atual | Identidade Restic autenticada, tamanho/SHA do nativo e `check --read-data` aprovados novamente; helper de leitura retornou `ready` |
| Último restore real | `46fe30bdb760`, 07/11/2026 12:07:28, restauração nativa/DBCC/integridade referencial aprovados e zero violações |
| Retenção/preservação | Configuração 7/4/3; cinco snapshots verificados e três cópias nativas, com inventário idêntico antes/depois da auditoria |
| Espaço disponível | 65.336.471.552 bytes livres às 12:57; reserva mínima 2 GiB e limite do repositório 8 GiB mantidos |
| SMTP | Diário e extra com aceite do provedor na primeira tentativa, DATA 250, TLS 1.3 e certificado validado; entrega na caixa não comprovada |

O recibo de restauração descreve a cópia anterior às migrations A/B/C; seu campo histórico `schema_compatibility_status=review_required` não é uma nova falha de restauração. O contrato de escrita conhecido do banco atual foi conferido pelo helper de leitura e retornou compatível. Não se repetiu restore, migrations, provisionamento SQL ou mudança de dados comerciais.

[Recibo sanitizado da auditoria](./evidence/BACKUP_AUDIT_2026-11-07.json) e [operação completa de backup](./BACKUP_ANDERSONMULTIMARCAS.md). O monitor local foi verificado por seu último recibo e estado do timer, sem injetar falhas ou disparar alerta. Não se acessaram dados/volumes de outros projetos ou configuração SMTP; não se coletaram credenciais.

## Melhoria recomendada e revisão futura

A próxima melhoria é uma **cópia externa criptografada**, com chave e credenciais recuperáveis independentemente da VPS, seguida de restauração periódica e ensaio de recuperação sem depender desse servidor. A [proposta técnica existente](./BACKUP_EXTERNO_PROPOSTA.md) continua apenas preparada, sem instalação ou destino contratado.

A decisão pode ser revista pelo proprietário quando houver destino e gestão de credenciais/chave aprovados, ou quando o risco operacional justificar a mudança. **Nenhuma data de revisão ou prazo de validade foi definido pelo proprietário.** A recomendação não altera o aceite temporário atual nem autoriza ações na conta de um provedor.
