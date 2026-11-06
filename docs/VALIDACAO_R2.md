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
