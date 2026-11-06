# Migrations produtivas preparadas — não executadas

Revisão de 06/11/2026. O usuário decidiu preservar as marcas 1/4 e diferenciar o nome da 4, mas **o rótulo ainda aguarda aprovação explícita**. Não executar SQL comercial enquanto as dependências não forem aprovadas. A autorização de instalar `anderson_runtime` é separada e pertence à PR #2.

## Estado lido em produção

| Informação | Resultado |
| --- | --- |
| ID 1 | Jaguar, ativa, cadastro 17/03/2026 21:25:53.2550543, 1 veículo |
| ID 4 | Jaguar, ativa, cadastro 24/03/2026 23:08:09.7600000, 0 veículos |
| Comparação | Nomes idênticos byte a byte, estado das referências igual à auditoria anterior |
| Relacionamento | Veiculo.MarcaId → Marca.Id, NO_ACTION |
| Nome | nvarchar(100), SQL_Latin1_General_CP1_CI_AS |
| Índices | PK_Marca único/clustered; IX_Marca_Nome não único/nonclustered, ativo e sem filtro |
| Semelhantes | Nenhum outro registro igual por CI_AI ou com prefixo Jaguar foi encontrado |
| Proposta | **Jaguar (cadastro adicional)** — reconhecível, sem colisão sob a collation real, dentro de 100 caracteres |
| Aprovação | Pendente; `approved_name` e referência de aprovação permanecem nulos |
| Impacto proposto | Somente Nome do ID 4; IDs, demais campos, veículos, relações e histórico preservados; 0 exclusões/0 relações alteradas |

Datas de cadastro são os valores SQL existentes, sem presumir conversão de fuso. Não foram modificadas. Os dados produtivos foram consultados somente por leitura nesta preparação de A/B/C.

## Versões produtivas revisadas

Os arquivos não são as propostas de ensaio com o guard removido. Têm contrato próprio de destino/escopo/autorização/checksum, transação, applock exclusivo, validação anterior/posterior e journal independente `AndersonProductionMigrationJournal`. Não escrevem no histórico EF existente e não são executados pelo CD ou pelo startup da aplicação.

- **A — [010_schema.sql](../deploy/production/migrations/010_schema.sql):** amplia as 12 colunas. Preserva a nulabilidade atual por padrão; verifica tipos, capacidades/nulabilidade esperadas e recusa estados inesperados. Confere fingerprints dos dados das quatro tabelas e das FKs antes/depois. Preserva IDs, dados e relacionamentos. Replay exige o resultado e a política registrados.
- **B — [020_marca.sql](../deploy/production/migrations/020_marca.sql):** requer A, nome comercial e referência de aprovação explícitos. Confere IDs, nomes antigos, estado ativo, 1/0 referências, FK esperada, limite em bytes Unicode, ausência de colisão e de triggers inesperados. Não aceita o sufixo técnico de ensaio. Altera somente Nome do ID 4 e confere hashes de todos os outros campos, das demais marcas e dos veículos. Registra antes/depois e aprovação em journal privado.
- **C — [030_index.sql](../deploy/production/migrations/030_index.sql):** requer A/B registradas e o nome aprovado ainda presente; recusa duplicados/índice inesperado antes de remover qualquer índice. Recria IX_Marca_Nome único em transação, preservando os dados. Replay confere checksum e definição registrada.

[Manifesto preparado](../deploy/production/target.prepared.json) fixa container, identidade SQL e GUID do banco auditados. **Não é uma autorização:** execução continua false; nome aprovado e referências de autorização continuam ausentes. Os SESSION_CONTEXT esperados não substituem a confirmação humana: são proteções contra erro de destino/escopo e devem ser preenchidos por operador autorizado somente na janela aprovada. Não há runner automático desses SQLs no pipeline.

## Questão de nulabilidade — ainda pendente

Produção tem Marca.LogoUrl NOT NULL; o modelo permite ausência de logo e o preflight conhecido exige NULL permitido. Como esta instrução pede preservar nulabilidade, A mantém NOT NULL por padrão e o bloqueio permanece. Foi solicitada confirmação específica para relaxar **somente LogoUrl**, mantendo as outras 11 colunas. A opção existe no script apenas com duas entradas explícitas (`AllowLogoNullability=approved` e uma referência de autorização própria); sem elas não relaxa a coluna. Não foi habilitada em produção nem no manifesto preparado.

## Testes e limites

Dois novos testes SQL executam essas versões em banco **sintético descartável**, com servidor/GUID próprios e referências de teste claramente identificadas. Cobrem falta de aprovação, dependência A/B/C, preservação da nulabilidade por padrão, autorização adicional de LogoUrl, dados/IDs/FKs, journal, replay e recusa de destino incorreto. Não são aprovações produtivas. Os 78 checkpoints anteriores de binários/rollback continuam válidos como evidência representativa; não foram repetidos por simples atualização documental.

## Execução futura e rollback

Antes de qualquer execução: decisão explícita do rótulo/nulabilidade, revisão dos scripts e checks finais, backup adicional concluído com SHA/VERIFYONLY/Restic, identidade do destino reconferida, plano de retorno e janela autorizada. A → B → C, sem migrations automáticas. Reconsultar veículos/nomes imediatamente antes de B; qualquer mudança exige nova avaliação, não remover o guard.

Rollback normal é do **binário**, mantendo colunas ampliadas, IDs e dados. Não há down automático com redução de colunas, deduplicação ou restauração de banco. O retorno do nome comercial seria outra mudança funcional explícita e poderia conflitar com C; não executar o undo de ensaio em produção. Preservar a identidade e o container do legado enquanto o rollback depender deles.

## Integração proposta

Primeiro revisar/integrar PR #3; depois atualizar PR #2 com a main resultante, resolver dependências e rodar CI do SHA combinado; então integrar PR #2 quando a política estiver legitimamente satisfeita. Não executar merge agora. Só StartingWeb está elegível: é necessário um mantenedor independente com acesso de revisão apropriado e inclusão correta em CODEOWNERS para as áreas protegidas. Não criar aprovação fictícia, não conceder acesso nem usar bypass.
