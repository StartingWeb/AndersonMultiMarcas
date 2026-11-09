# Segunda cópia fora da VPS — preparação

## Resolvido

Backup Anderson local íntegro, Restic criptografado, SHA-256, recuperação autenticada, retenção 7/4/3 e SMTP próprios continuam operacionais. A configuração compartilhada TS Compro Ouro foi novamente inspecionada em memória, sem imprimir/copiar credenciais. Os campos de repositório apontam para armazenamento local; os timers de backup/monitor existentes foram inventariados. Nenhum destino externo autorizado foi identificado no mecanismo auditado. Isso não prova inexistência de contas/serviços fora dessa configuração.

## Preparado, aguardando autorização

[offsite-backup.example.json](../deploy/offsite-backup.example.json) é um contrato desabilitado, sem endpoint ou segredo e não está instalado no servidor. Para integrar futuramente são necessários: destino já aprovado, referência da autorização, namespace/repositório exclusivos do Anderson, identidade esperada do repositório, credencial de acesso limitada, chave de criptografia independente guardada fora da VPS e responsável pela recuperação.

Plano: `restic copy` do snapshot local já verificado para repositório externo exclusivo; autenticar a identidade do destino; restaurar em staging privado; comparar SHA-256 e manifesto; executar check/read-data; somente então registrar réplica válida e aplicar retenção 7/4/3 no destino. A falha remota nunca remove a última cópia local, não marca cópia como válida e gera incidente deduplicado pelo mecanismo SMTP já existente. Acrescentar monitor de idade da réplica e alarme externo à VPS; um monitor no mesmo host não detecta sua perda total.

Antes de autorizar uso: ensaio com dados sintéticos; depois cópia real expressamente autorizada, restauração e DBCC isolados; registrar tempos/RPO/RTO e testar recuperação sem depender da VPS. Não compartilhar snapshots/chaves de outros projetos, não usar o bucket produtivo de mídia e não contratar/criar cobrança nesta tarefa.

## Bloqueante

Faltam destino e gestão independente de credenciais/chave autorizados. Nenhuma cópia real saiu da VPS, nenhum serviço foi contratado e nenhum backup existente foi excluído por essa preparação. O backup local mantém seu agendamento; a resiliência à perda total da VPS continua pendente.
