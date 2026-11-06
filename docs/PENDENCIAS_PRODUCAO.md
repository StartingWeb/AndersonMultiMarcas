# Pendências antes da ativação

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Concluído e verificado

- Backup nativo inicial com checksum, SHA-256, VERIFYONLY e cópia criptografada Restic restaurada/autenticada.
- Restauração SQL isolada, DBCC CHECKDB, constraints, schema e contagens iguais à origem; aplicação .NET 10 sem migrations na cópia original.
- Full diário às 03:00 America/Sao_Paulo, systemd habilitado/persistente e usuário com linger; monitor horário; retenção 7/4/3.
- Destino unificado privado e configuração real fora do Git; legado vivo preservado, imagem exportada e testada com docker load.
- Environment production main-only, bypass administrativo false, cinco secrets SSH, chave exclusiva restrita e host key conferida.
- Ensaio real dos nove cenários de deploy/rollback e validação de configuração/rootfs OCI.
- R2 configurado, endpoint/região/base pública avaliados e HEAD do bucket produtivo 200, sem requests a objetos.
- Três gates GitHub false; gates locais ausentes; containers/SQL/NPM originais preservados.

## Bloqueios e decisões externos

| Pendência | Evidência e ação necessária |
| --- | --- |
| Destinatário dos relatórios | Não havia endereço Anderson definido. Informar o endereço autorizado; configurar no arquivo privado e executar novo backup manual com notificação real. Não houve envio nem confirmação de entrega. |
| Revisor independente | Só StartingWeb apareceu como colaborador. Informar usuário/equipe apropriado, conceder o acesso necessário por processo autorizado e configurar required_reviewers com prevent_self_review. Não há aprovação humana completa enquanto faltar isso. |
| Branch main | Não havia proteção global. Decidir a política de revisão/checks para main. A regra main-only do Environment já está aplicada. |
| Drift SQL para gravações | Produção tem colunas menores que o modelo e índice de marcas não único com duplicidade existente. Rever a branch separada infra/schema-compatibility-review e seu ensaio. Não se autoriza reduzir strings, eliminar/mesclar marcas ou mudar produção implicitamente. |
| Aceitação do schema | Ler VALIDACAO_SCHEMA_PRODUCAO.md; aprovar diferenças retidas, comportamento de escrita e rollback. Duas migrations de histórico estão ausentes, mas a aplicação de leitura funcionou sem executá-las. |
| R2 exclusivo de testes | Falta bucket/credenciais exclusivos. Preparar teste sintético upload/HEAD/read/hash/delete/limpeza. Não usar prefixo dentro do bucket produtivo. Permissões de escrita/exclusão e imagens reais não foram declaradas comprovadas. |
| Cópia fora da VPS | Backup atual está no mesmo disco. Indicar destino protegido aprovado e gestão independente da chave; armazenamento local não atende perda total da VPS. |
| Revisão das mudanças de infraestrutura | Rever e integrar manualmente a branch/PR. O artefato atual contém scripts anteriores ao ajuste SCP/OCI; o primeiro deploy via Actions precisa do CI e pacote do commit final. |
| Paridade e gates | Revisão formal da matriz, SHA final e arquivo de aprovação. Não criar source-parity.approved.json nem deploy.enabled ou habilitar variáveis sem autorização explícita. |
| Janela e corte | Definir operador, janela, observação e critérios; depois solicitar autorização do corte com evidências atualizadas. Workflow Release permanece ativo até essa janela. |
| Login administrativo produtivo | Validar com operador autorizado no aceite; nenhum login/senha real foi solicitado, exibido ou usado no ensaio. |

## Fora do escopo de mudança atual

O SQL compartilhado continua com exposição de porta 1433 e configuração de conexão observada sem criptografia obrigatória; firewall e confiança TLS precisam de revisão própria, sem reiniciar o serviço ou afetar outros bancos. Configurações antigas de backup de outros projetos não foram alteradas. Não se revogaram credenciais legadas nem se arquivou o Release.

O estado do monitor Anderson é alerta enquanto faltar notificação, embora a cópia válida exista. Nenhuma pendência foi substituída por gate fictício, endereço inventado ou uso de objetos de clientes em testes.

Documentos operacionais: [backup](./BACKUP_ANDERSONMULTIMARCAS.md), [schema](./VALIDACAO_SCHEMA_PRODUCAO.md), [R2](./VALIDACAO_R2.md), [ativação](./ATIVACAO_PRODUCAO.md) e [rollback](./ROLLBACK_PRODUCAO.md).
