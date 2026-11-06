# Banco: auditoria e validação isolada

05/11/2026. Nenhuma consulta de escrita ou migration foi executada no servidor real.

## Problemas e decisões

1. A migration histórica `20260528140115_AlignIdentityToGuid` alterava oito colunas ainda referenciadas por PK/FK/índices. O SQL Server recusava uma instalação nova. O identificador e schema de destino foram preservados; o bootstrap agora valida todas as chaves, remove/recria somente as dependências Identity conhecidas e converte dentro da transação EF. Bancos onde esse ID já está aplicado não reexecutam a alteração. Dependências personalizadas não previstas fazem a transação falhar, exigindo revisão.
2. `TRY_CONVERT(uniqueidentifier, ...)` aceita truncamento de sufixos. O preflight exige representação canônica com 36 caracteres, além da conversão, e rejeita duplicatas após normalização. Chaves inválidas permanecem intactas; não se inventa mapeamento para usuários reais.
3. O downgrade Identity era igualmente inválido e não é necessário ao rollback de aplicação. Agora falha explicitamente com 51002. Uma reversão de chaves requer plano separado, backup e autorização. Nenhum rollback automatizado chama migrations.
4. `RepairMissingAuditColumns` não tinha metadados de descoberta. Foram acrescentados `[Migration]`/`[DbContext]`; o SQL idempotente existente foi preservado. Um banco existente pode aplicar essa correção pendente sem duplicar colunas.
5. Foram recuperadas cinco migrations do `Data.dll` do Release, conservando seus IDs: índice único filtrado da capa, CNPJ nullable/normalização, lojas legadas, drift de auditoria/métricas e compatibilidade de upload. A implementação foi reconstruída e revisada; não representa fonte original comprovada.
6. Para bootstrap, duplicatas de capa são resolvidas por `Ordem, Id` antes do índice único. Nenhuma imagem é apagada. As demais imagens continuam permitidas com `Capa=0`.
7. O snapshot foi atualizado para .NET/EF10, CNPJ opcional e índice de capa. `AlignRecoveredReleaseModel` é uma migration de alinhamento de metadados sem DDL repetido: o Release já efetuou essas alterações. Foi retirada a supressão genérica de `PendingModelChangesWarning` do startup.

Não se alteraram os IDs das migrations históricas já aplicadas. As duas correções históricas foram avaliadas quanto ao efeito: permanecem ignoradas quando já registradas, conservam GUIDs e destino do schema; instalações novas passam a funcionar. Alterações em schema real continuam sujeitas à conferência do histórico, constraints customizadas, backup e revisão do SQL.

## Evidências

Foi criado o container exclusivo `anderson-recovery-sql-20261105`, SQL Server 2022 em `linux/amd64` emulado no Mac ARM, porta dinâmica publicada somente em `127.0.0.1`. Credencial aleatória reside apenas em `.cache/validation`, com permissão 600. Nenhum container SQL de outro projeto foi reutilizado.

A suíte SQL cobre instalação completa, reaplicação sem alterações, script idempotente gerado/reexecutado duas vezes, GUIDs/vínculos preservados, recusa de chaves inválidas e truncadas, downgrade recusado, CRUD com relacionamentos, CNPJ opcional/único, dados legados NULL, capa única, ordenação SQL asc/desc com desempate/paginação e campos comerciais preservados. Os resultados consolidados estão em [validação funcional](VALIDACAO_FUNCIONAL.md).

Os testes criam seus próprios bancos temporários e os removem ao final. Dados de navegador são sintéticos em banco separado. Isso demonstra instalação e contratos locais; **não demonstra compatibilidade com um backup exato da produção**. Esse ensaio depende de cópia sanitizada autorizada ou de preflight em janela controlada.

## SQL para revisão

```sh
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project Data/Data.csproj --startup-project Data/Data.csproj --configuration Release
dotnet ef migrations script --idempotent --project Data/Data.csproj --startup-project Data/Data.csproj --configuration Release --output artifacts/review-required.sql
```

A geração usa factory sem connection string e não abre banco. O CI preserva o SQL como evidência. Nunca adicione `database update` ao Dockerfile, entrypoint, deploy ou rollback.
