# Validação do Cloudflare R2

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## O que foi comprovado

A configuração real da aplicação foi inventariada sem exibir valores de acesso e preparada no runtime.env privado. Provider R2, dual read ativo, região auto, endpoint HTTPS da conta e base pública HTTPS foram confirmados. Campos de account/access/secret/bucket/service/public URL estavam preenchidos. O endpoint foi comparado ao formato de conta esperado; redirects no request autenticado são recusados.

[validate_r2.py](../scripts/ops/validate_r2.py) assinou exclusivamente **HEAD do bucket** com SigV4 e recebeu HTTP 200, com validação TLS. Não acessou objetos, não listou registros de clientes, não realizou uploads/exclusões nem modificou permissões. O recibo protegido está em `history/r2-validation.json` na raiz unificada.

Uma consulta agregada ao banco identificou referências no domínio público R2 e referências legadas `/uploads/`; nenhum URL, nome de arquivo ou objeto real de cliente foi usado como teste. O inventário de mounts confirmou somente Data Protection na aplicação viva. Os quatro arquivos de uploads do checkout antigo foram inventariados e não movidos; não se assumiu que representam todas as referências legadas.

O código [R2StorageService.cs](../Core/Storage/R2StorageService.cs), [StoragePath.cs](../Core/Storage/StoragePath.cs) e [StorageImageResolver.cs](../Project/Infrastructure/Storage/StorageImageResolver.cs) preserva comportamento S3/dual read e paths legados recuperados. Os testes do CI de referência passaram, incluindo tratamento de falhas e compatibilidade de URLs com dados sintéticos/dublês. Isso não comprova disponibilidade de cada objeto legado nem permissões de PUT/DELETE reais.

## Impedimento do ciclo real

Não foi encontrada configuração de bucket exclusivo de testes Anderson. O bucket produtivo não foi usado para gravação, nem mesmo sob prefixo temporário. Não se reutilizou bucket de outro projeto.

O contrato sem credenciais está em [r2-test.env.example](../deploy/r2-test.env.example). Faltam bucket exclusivo, endpoint, URL e credenciais de teste autorizadas, com permissões mínimas e separadas da produção. Manter esse arquivo real fora do Git e do GitHub de código-fonte; configuração de produção permanece somente no servidor.

## Teste a executar quando houver bucket autorizado

1. Confirmar que bucket/credenciais são exclusivamente de teste e diferentes da produção.
2. Gerar imagem sintética sem dados de clientes, com chave aleatória exclusiva da execução.
3. Calcular SHA-256 local e executar upload por meio do mesmo código da aplicação.
4. Executar HEAD, conferir tamanho/content type e ler o objeto pelo caminho previsto.
5. Comparar bytes/SHA-256 e a URL pública de teste quando aplicável.
6. Excluir somente o objeto sintético criado pela execução e confirmar ausência.
7. Simular credencial/objeto ausente e recusa de HEAD, verificando erros/retry e ausência de segredos nos logs.
8. Registrar aceite, limpeza e limitações. Não promover o resultado de dublês para prova de permissões no serviço real.

## Aceitação antes do corte

O HEAD 200 comprova acesso de metadados ao bucket configurado; não comprova upload, exclusão, leitura pública de objetos ou compatibilidade integral das imagens antigas. A aprovação R2 e o ciclo sintético continuam pendentes. O teste HTTP da imagem padrão no container é de um estático publicado, não de um objeto R2 de cliente.

Não alterar objetos, prefixos, bucket produtivo, NPM ou URLs de clientes para fazer o ensaio passar. Qualquer migração de mídia precisa de inventário e escopo próprios. Ver [ATIVACAO_PRODUCAO.md](./ATIVACAO_PRODUCAO.md).

## Revisão técnica adicional — resolvido localmente

A única Environment GitHub encontrada continua sendo `production`; não há configuração de homologação autorizada no mecanismo auditado. A fonte privada do endpoint é `Storage__R2__ServiceUrl` (`https://<AccountId>.r2.cloudflarestorage.com`), com região `auto`, path-style e bucket indicado pelo runtime privado. Não publicar identificadores/chaves desnecessários no Git. Nenhuma operação de objeto produtivo foi feita nesta revisão.

| Área | Contrato observado |
| --- | --- |
| Escrita | PutObject sem chunked encoding; assinatura SigV4/região auto; rewind de stream; PUT confirmado não vira falha se faltar HEAD |
| Leitura | HEAD/GetObject; 404/NoSuchKey viram ausência; a camada da aplicação tenta fallback local em erro remoto |
| Listagem | ListObjectsV2 com prefixo/paginação para ferramentas de sincronização; permissão restrita aos prefixos autorizados quando habilitada |
| Chaves | uploads/veiculos, uploads/vendedores e legado anderson-multimarcas/veiculos; uploads normais usam UUID WebP, sem depender do nome original |
| Duplicidade/overwrite | A API de storage sobrescreve se o mesmo key for enviado. O fluxo de upload gera chaves diferentes; não faz overwrite de arquivo preexistente por nome original |
| URLs | URL pública formada pela base configurada; não foi encontrado uso de URL pré-assinada nesse serviço. Espaços/acentos são normalizados como chaves; `?`/`#` seguem a regra de interpretação de URLs do StoragePath e não devem ser enviados como nomes arbitrários de objetos |
| Exclusão | DeleteObject; ausente é tolerado. A camada ApplicationStorageService registra falha remota e ainda remove a cópia local, portanto requer monitoramento/reconciliação de órfãos; sucesso de DB não prova deleção remota |
| Transação de mídia | Validação/encode antes do primeiro write; chaves novas são compensadas em falha; referências antigas só são excluídas após commit SQL e conferência de uso |
| Credenciais | Exclusivas do bucket/prefixos necessários, sem administração da conta; Get/Head/Put/Delete e List apenas quando a ferramenta realmente exigir; fora do Git/log |

Oito novos testes .NET usam um S3 em memória, sem rede: upload/HEAD/read/hash/delete, ausente, overwrite, chaves geradas distintas para nomes repetidos, acentos/espaços, roundtrip de 10 MiB, credencial/503, timeout/cancelamento e falha de exclusão preservando objeto. Somam-se aos testes de compatibilidade R2 já existentes. Não são prova de permissão, retry ou timeout no provedor real. O retry de transporte depende do SDK; o serviço não implementa um retry próprio por operação.

## Preparado, aguardando autorização

O arquivo de configuração de teste já está em [r2-test.env.example](../deploy/r2-test.env.example). Fornecer bucket/endpoint/base pública exclusivos e credenciais autorizadas. Executar o ciclo real somente após comparar esse destino com o produtivo; usar chaves UUID de execução, limite de 10 MiB, SHA-256 e limpeza exclusiva dos objetos sintéticos. Cobrir 403, 503 e timeout via proxy controlado no ambiente de homologação, nunca por injeção de falhas no bucket produtivo.

## Bloqueante

O ciclo real R2 e a compatibilidade de mídia antiga/nova no provedor continuam bloqueantes da ativação. O ensaio de rollback validou upload e referências com storage **local isolado** e volume descartável compartilhado entre binários. Não transforma esse resultado em prova sobre objetos/R2 produtivos. Manter o aceite R2 pendente na revisão formal de paridade e os gates false.

## Dados externos necessários nesta rodada

Não foi encontrado bucket **ou prefixo de homologação previamente autorizado**; o GitHub só tem Environment production, e a configuração privada auditada não declara destino de teste. Não usar um prefixo arbitrário no bucket produtivo. Faltam: (1) referência explícita da autorização; (2) endpoint/região e bucket; (3) prefixo de homologação, se compartilhado, com política que impeça acesso/modificação fora dele; (4) credencial de teste em arquivo privado no servidor, nunca colada em chat/Git; (5) base pública ou rota de download de teste; (6) permissões Put/Get/Head/Delete e List apenas quando necessário; (7) prova do isolamento e responsável pela limpeza.

Com esses dados, executar arquivos sintéticos com chave UUID, upload/download/hash/ausente/overwrite/delete e falhas controladas exclusivamente no destino autorizado. Os oito testes locais já aprovados não foram repetidos sem necessidade e não substituem esse ciclo real. Nenhum objeto produtivo foi modificado/excluído. A atualização da conexão SQL futura não alterou campos R2.

## Conferência de 07/11/2026

Endpoint R2 da conta via HTTPS, região auto, campos necessários presentes, base pública HTTPS e HEAD autenticado do bucket 200. A auditoria inicial fez apenas HEAD do bucket; a leitura adicional de objetos está descrita abaixo, sem uploads/exclusões produtivos. Configuração futura preservada após A/B/C; nenhum destino de teste autorizado foi identificado. O gate continua pendente pelos dados listados acima. [Recibo sanitizado](./evidence/PRODUCTION_SQL_2026-11-07.json).

## Leitura real de mídia existente — 07/11/2026

Amostra da página pública, sem PUT/DELETE: três imagens de veículos retornaram **200/image-webp** na URL pública e no HEAD S3 autenticado; três fotos de vendedores retornaram **404** em ambos. URLs não foram alteradas por A/B/C. Não se encontraram os arquivos correspondentes no container; a busca no checkout legado (820 entradas) não encontrou originais dos oito nomes de fotos de vendedores referenciados pela página. Isso não prova ausência em todos os acervos nem autoriza recriar imagens ou apontar para outros arquivos.

**Falha técnica aberta:** localizar originais das três fotos ausentes ou uma fonte de recuperação autorizada, conferir correspondência/integridade e definir reparação com o proprietário. Nenhum objeto foi regravado ou excluído, nenhuma URL/dado comercial mudou. A homologação sintética de escrita permanece uma dependência separada, com destino isolado autorizado. A amostra de leitura confirma acesso às imagens de veículos testadas, não integridade de todo o bucket. [Evidência sanitizada](./evidence/PRODUCTION_SQL_2026-11-07.json).
