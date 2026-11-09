# Validação do Cloudflare R2

**Homologação isolada concluída em 07/11/2026, às 16:36 UTC (13:36 de Brasília)**, com autorização expressa para dois buckets, nova credencial restrita e dados sintéticos. [Recibo sanitizado](./evidence/R2_HOMOLOGATION_2026-11-07.json). Objetos produtivos alterados: **zero**. Nenhuma mudança de aplicação, banco, DNS produtivo ou credenciais produtivas.

## Conta, buckets e credencial

A conta autenticada no Safari deste Mac foi comparada em memória com AccountId/endpoint do runtime existente. O recibo publica apenas fingerprint SHA-256 da identidade. A lista inicial continha somente startingweb-storage; os dois nomes autorizados estavam ausentes.

| Recurso | Estado final |
| --- | --- |
| anderson-multimarcas-homologacao | Criado, ENAM, Standard; vazio após limpeza; r2.dev habilitado |
| anderson-multimarcas-homologacao-negativa | Criado, ENAM, Standard; privado e vazio, sem domínio customizado ou URL pública |
| startingweb-storage | Preservado; operações negativas usam somente token novo e chave sintética inexistente |
| Credencial nova | Anderson-Homologacao-20261107; account R2 token, Object Read & Write somente no primeiro bucket |
| Administração / todos os buckets | Não concedidos |
| Armazenamento do segredo | Somente no Mac, JSON privado fora do Git; arquivo 0600 em diretório 0700 |
| Validade / filtro IP | Forever; filtro IP padrão sem restrição adicional |
| Credenciais existentes | Duas ativas, vinculadas ao bucket produtivo; preservadas; nenhum valor registrado |
| Lifecycle dos dois novos buckets | Apenas aborto padrão de multipart incompleto após sete dias; sem expiração de objetos completos |
| CORS / bucket locks / domínios customizados | Ausentes nos dois novos buckets |
| Funcionalidades pagas adicionais | Nenhuma habilitada ou contratada |

Arquivo privado local não versionado: `.cache/production-prep/policy-r2-20261107/private/r2-homologacao.json`. Nenhum valor de acesso consta deste documento.

A credencial não foi instalada no runtime, em secrets GitHub ou no CI. As permissões foram conferidas no resumo de criação e na listagem persistida, depois comprovadas pelos testes abaixo. Não foi reutilizada a credencial de produção.

## Ciclo autenticado real

Run: `20261107T163623Z-56f7ba2ecd541ae7c1c4cf0c`. Prefixo exclusivo: `codex-homologacao/20261107T163623Z-56f7ba2ecd541ae7c1c4cf0c/`. Arquivo SVG de 178 bytes, gerado em memória para esta execução, Content-Type image/svg+xml. SigV4, região auto, endpoint da mesma conta, TLS validado e sem redirects.

| Teste | Resultado |
| --- | --- |
| Upload sintético | 200 |
| HEAD autenticado / Content-Type | 200 / image/svg+xml |
| GET autenticado / SHA-256 | 200 / coincidente |
| Objeto inexistente distinto | 404 NoSuchKey |
| Sobrescrita do objeto próprio | 200 |
| Novo GET / hash novo | 200 / coincidente e diferente do original |
| Exclusão do objeto próprio | 204 |
| Ausência após exclusão | 404 NoSuchKey |
| LIST do prefixo após limpeza | 200, zero chaves |

SHA-256 original: `49ad18fb22931675e6b3e0df4a3c3d99183f73e435d7e6cebba39558f32fcb06`.

SHA-256 após sobrescrita: `6a233e712eb8bb12420322c5ed840153bba96d84094bdc345acdb7b110119c3d`.

A primeira tentativa completou upload/leitura/hash, mas recebeu 403 na entrega pública logo após habilitar a URL. Seu finally excluiu o único objeto próprio e confirmou prefixo vazio. O ciclo completo acima repetiu com outro prefixo e User-Agent de navegador e passou. A causa do primeiro 403 não foi estabelecida; o recibo preserva o resultado sem atribuí-lo conclusivamente à propagação ou ao cliente.

## Testes negativos

A mesma credencial foi usada nos dois buckets fora de escopo. LIST limitado ao prefixo aleatório/max-keys=1; GET/PUT/DELETE somente na chave sintética never-created.svg do run. Não houve DELETE de objeto real nem limpeza produtiva.

| Operação | Controle negativo | startingweb-storage |
| --- | --- | --- |
| Listar | 403 AccessDenied | 403 AccessDenied |
| Ler chave inexistente | 403 AccessDenied | 403 AccessDenied |
| Criar chave inexistente | 403 AccessDenied | 403 AccessDenied |
| Excluir chave inexistente | 403 AccessDenied | 403 AccessDenied |

As oito negações comprovam acesso recusado para essas operações, além da política restrita conferida no painel. São distintas de NoSuchKey: o 404 foi obtido apenas em chave inexistente do bucket autorizado. O controle negativo foi novamente inspecionado no painel, privado e vazio. Nenhum objeto desconhecido foi apagado.

## Entrega pública isolada

URL: [https://pub-46c1a84ed617400fa2e2fdc830248159.r2.dev](https://pub-46c1a84ed617400fa2e2fdc830248159.r2.dev). Endpoint gratuito de desenvolvimento r2.dev, sujeito a rate limit e sem garantia de uso produtivo. Não foi criado domínio customizado, DNS, Worker ou plano adicional.

GET da mídia sintética retornou 200, image/svg+xml e SHA-256 idêntico ao autenticado. Certificado/hostname/cadeia HTTPS validados; sessão TLSv1.2, certificado com validade até Feb  3 05:25:19 2027 GMT. O objeto foi removido ao final, portanto a URL não preserva uma mídia de demonstração.

O bucket negativo permaneceu com Public Development URL desativada e sem domínio customizado, conferido novamente após os testes. A URL de homologação não oferece acesso autenticado ao controle negativo nem à produção.

## Fotos ausentes: pendência independente

A [auditoria anterior somente leitura](./evidence/R2_MEDIA_AUDIT_2026-11-07.json) encontrou oito referências de fotos de vendedores sem original recuperado nas fontes examinadas. Três veículos tiveram leitura pública/autenticada e hashes coincidentes. Essas evidências históricas não foram substituídas por arquivos fictícios.

Nesta rodada não se alterou FotoUrl, registros de vendedores/veículos, imagens, credenciais produtivas ou objetos comerciais. A [recuperação das fotos](./RECUPERACAO_MIDIAS_R2.md) depende dos originais e de autorização específica por objeto. O sucesso sintético não aprova paridade da aplicação nem resolve as oito fotos.

## Limpeza e próximos usos

Os dois prefixos da execução foram confirmados vazios; os buckets foram preservados. Novos ensaios devem usar somente a credencial de homologação, outro prefixo aleatório e dados sintéticos, sem banco produtivo. Nunca executar limpeza global ou ampliar o token. [Pendências](./PENDENCIAS_PRODUCAO.md), [ativação](./ATIVACAO_PRODUCAO.md).
