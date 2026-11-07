# Validação do Cloudflare R2

Atualizado em **07/11/2026**. Auditoria somente leitura: nenhum bucket, credencial, política ou objeto foi criado/alterado; nenhum PUT/DELETE produtivo. Homologação de escrita e recuperação das fotos são pendências independentes.

## Resultado comprovado

- HEAD autenticado do bucket: **200**, em 07/11/2026 às 16:02 UTC, SigV4/TLS, recusando redirects.
- Três veículos: HEAD/GET **200** públicos e autenticados; SHA-256 dos bytes coincide em cada par.
- As três fotos ausentes anteriores são dos registros **5, 6 e 7**. A conferência ampliada às **oito FotoUrl existentes no banco**, IDs **3–10**, encontrou **404** público e no HEAD autenticado em todas.
- Oito ListObjectsV2 limitados aos prefixos exatos de UUID já referenciados pelo Anderson, MaxKeys=5: **200, vazios e sem truncamento**. Não se listou o bucket inteiro.
- Originais encontrados: **zero nas fontes inspecionadas**. Os oito UUIDs aparecem nos bytes do backup de 28/07/2026 e dos três backups nativos retidos; referências históricas não são arquivos de fotos recuperados.

[Evidência desta rodada](./evidence/R2_MEDIA_AUDIT_2026-11-07.json), [diagnóstico por foto e recuperação](./RECUPERACAO_MIDIAS_R2.md) e [evidência anterior preservada](./evidence/PRODUCTION_SQL_2026-11-07.json).

## Configuração e integração observadas

| Campo | Estado real, sem valores de acesso |
| --- | --- |
| Endpoint | HTTPS no formato `https://<AccountId>.r2.cloudflarestorage.com`; conta conferida em memória, identificador omitido |
| Bucket | `startingweb-storage`; o nome não comprova exclusividade ao Anderson |
| Região/assinatura | `auto`, SigV4, path-style |
| Base pública | `https://pub-2c5e28a366434870b48f2b07b425e788.r2.dev` — pública, sem credencial |
| Provider/dual read | `R2` / `true` |
| Legado | Variáveis efetivas de `andersonmultimarcas-web` |
| Futuro | Arquivo privado `shared/runtime.env` na raiz unificada; container futuro não iniciado |
| Comparação | AccountId, access/secret, bucket, base, endpoint e região idênticos entre legado e futuro, sem registrar valores de credenciais |
| Histórico versionado | Quatro versões de appsettings examinadas, sem R2 preenchido; defaults locais são sobrepostos pelo environment do runtime |
| Mounts legados | Somente volume Data Protection; dois XML, sem upload persistente |
| Teste | Nenhum bucket/prefixo/credencial de homologação autorizado identificado na configuração auditada |

Não se comprovou exclusividade do bucket ao projeto. As consultas usaram referências reais do próprio Anderson e seus prefixos UUID exatos; não se acessaram objetos/volumes de outros projetos.

### Chaves e URLs

[StoragePath](../Core/Storage/StoragePath.cs) reconhece `uploads/veiculos/`, `uploads/vendedores/` e `anderson-multimarcas/veiculos/`. Normaliza separadores/raiz web/query/fragment e rejeita travessia. [R2StorageService](../Core/Storage/R2StorageService.cs) forma `PublicBaseUrl + "/" + key` e usa esse mesmo key em S3; não há URL pré-assinada nesse serviço.

[VeiculoMediaService](../Project/Features/Veiculos/Services/VeiculoMediaService.cs) valida/encoda antes da gravação e gera UUID WebP. O storage sobrescreve quando recebe o mesmo key; o upload de veículo usa UUID para não depender do nome original. O [cadastro atual de vendedores](../Project/Pages/Admin/Cadastros/Vendedores/Upsert.cshtml.cs) persiste a FotoUrl fornecida, sem executar esse upload de veículo.

As oito FotoUrl têm formato `/uploads/vendedores/<UUID>.jpg`. O [resolver](../Project/Infrastructure/Storage/StorageImageResolver.cs) preserva key/extensão e forma exatamente as URLs examinadas. O formato coincide com o contrato; os objetos nesses keys estão ausentes. Não se demonstrou erro de normalização, renomeação `.jpg` para `.webp`, causa/momento da perda ou relação causal com A/B/C. Nenhuma URL foi alterada.

### Política e permissões efetivas

| Operação | Prova | Limite |
| --- | --- | --- |
| HEAD bucket | 200 | Não comprova escrita/política completa |
| HEAD/GetObject | Três veículos 200; hashes público/autenticado iguais | Somente esses objetos; 404 dos vendedores não é teste negativo de autorização |
| ListObjectsV2 | Oito consultas 200 nos prefixos UUID exatos | Não comprova restrição da credencial a esses prefixos |
| PUT/overwrite/DeleteObject | Não executados | Permissões/transporte/limpeza reais não homologados |
| Gestão Cloudflare/token | Sem política de gestão auditável nesta sessão | Escopo e negações fora dele não comprovados |

Sucesso de leitura não demonstra isolamento. Retry de transporte depende do SDK; não há retry próprio por operação. HEAD pós-PUT é diagnóstico: PUT aceito não vira falha porque HEAD foi recusado/ausente. [ApplicationStorageService](../Project/Infrastructure/Storage/ApplicationStorageService.cs) pode registrar falha de exclusão remota e ainda remover a cópia local; uma reparação futura exige conferência remota explícita.

## Destino isolado proposto — requer autorização

A proposta concreta é criar **dois buckets exclusivamente sintéticos do Anderson**:

1. `anderson-multimarcas-homologacao`: destino do ciclo; credencial **nova Object Read & Write limitada somente a este bucket**, sem administração da conta/buckets e sem reutilizar token produtivo.
2. `anderson-multimarcas-homologacao-negativa`: controle vazio, privado, sem mídias comerciais; o token do ciclo não recebe acesso a ele. Permite testar negação fora do escopo sem tocar o bucket compartilhado.

Nomes sujeitos à disponibilidade. A autorização necessária cobre criação dos dois buckets, criação da credencial limitada, configuração de base pública exclusiva de teste no primeiro quando aplicável e entrega do arquivo privado 0600 fora do Git/GitHub. Registrar referência da autorização, política efetiva e responsável pela limpeza. **Nenhum recurso foi criado nesta rodada.**

Um prefixo no bucket compartilhado só seria alternativa com política suportada e credencial própria impedindo acesso fora dele. Prefixo com token capaz de modificar o bucket inteiro não é isolamento. Não foi encontrada prova dessa alternativa no runtime auditado.

[r2-test.env.example](../deploy/r2-test.env.example) prepara o contrato local com destino/credenciais vazios. Não executa requisições, não concede autorização e não implementa sozinho uma barreira de isolamento. Não incluir configuração SQL produtiva.

## Ciclo preparado, ainda não executado

1. Conferir autorização/política; comparar em memória bucket, token e base com produção, recusando coincidência. Rodar somente serviço R2 com configuração isolada, sem banco produtivo.
2. Gerar imagem sintética válida até 10 MiB, SHA-256 e UUID da execução. Reservar keys `uploads/veiculos/r2-acceptance/<UUID>/`; manter manifesto apenas dos objetos criados.
3. Fazer upload pelo mesmo `R2StorageService` da aplicação.
4. HEAD autenticado: tamanho/content type. GET autenticado: comparar bytes/SHA-256.
5. GET público pela base de teste, se aprovado, comparando SHA-256.
6. Consultar key sintético inexistente da execução: distinguir ausência de 403/falha de transporte.
7. Sobrescrever somente o key criado no passo 3 com segunda imagem; conferir novo hash/tamanho.
8. Excluir somente keys do manifesto e confirmar ausência autenticada/pública, considerando cache. Nunca limpar bucket/prefixo inteiro.
9. Com o token do passo 1, consultar HEAD/List do **bucket de controle sintético** e exigir negação. Conferir também a política: HEAD negado sozinho não prova proibição de escrita. Se a autorização abranger negativa real de PUT, usar apenas key UUID sintético no controle vazio; eventual sucesso indevido é falha de isolamento e exige limpeza pelo responsável com credencial própria. Não fazer qualquer teste negativo no bucket produtivo/compartilhado.
10. Registrar recibos sanitizados, hashes, ausência, overwrite e limpeza. Simular 403/503/timeout/cancelamento em dublês/proxy isolado, sem manipular política/credencial produtiva.

[Testes de contrato em memória](../Project.Tests/Features/Storage/R2IsolationContractTests.cs), [compatibilidade de release](../Project.Tests/Recovery/R2ReleaseRecoveryTests.cs) e [auditoria HEAD sem escrita](../scripts/ops/tests/test_r2_audit.py) não substituem esse ciclo real nem comprovam a política Cloudflare.

## Gates e dependências

A escrita real aguarda autorização/destino isolado. As oito fotos aguardam fonte original verificada e autorização específica de reparação. Não atestar paridade R2 nem ativar gates antes dos aceites aplicáveis; ver [ATIVACAO_PRODUCAO.md](./ATIVACAO_PRODUCAO.md). Nenhum merge, deploy, mudança de política Cloudflare, URL ou objeto produtivo foi feito nesta rodada.
