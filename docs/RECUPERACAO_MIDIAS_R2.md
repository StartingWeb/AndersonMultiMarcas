# Investigação das fotos de vendedores — 07/11/2026

## Diagnóstico

A amostra anterior corresponde aos vendedores **IDs 5, 6 e 7**. A conferência somente leitura foi ampliada às **oito referências FotoUrl existentes**, IDs **3–10**: todas retornam **404** público e no HEAD S3 autenticado. As URLs examinadas seguem a mesma base/key do código atual. Oito listas aos prefixos UUID exatos retornaram 200, vazias e sem truncamento.

**Originais recuperados: zero nas fontes inspecionadas.** Nenhuma foto foi inventada/substituída, key gravado/excluído ou URL/dado comercial alterado. Três veículos retornaram HEAD/GET 200 público/autenticado com SHA-256 igual por par; a falha não afeta todas as leituras do provedor.

[Evidência sanitizada](./evidence/R2_MEDIA_AUDIT_2026-11-07.json) e [auditoria/configuração R2](./VALIDACAO_R2.md). Não se publicam nomes/contatos dos vendedores.

## Registro por foto

Os keys omitem a barra inicial da FotoUrl. “Local” cobre container, volume Anderson, checkout, diretórios unificados e histórico Git examinados. “Backup” distingue a referência histórica de bytes recuperados da imagem. “Release” inclui camadas dos dois arquivos Docker próprios examinados.

| ID | Key esperado | Público / HEAD autenticado | Banco | R2 key/prefixo UUID | Local | Backup | Release | Hash original / recuperação |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 3 | `uploads/vendedores/8d0e178ef52c475d81e6e58374ecdfdb.jpg` | 404 / 404 | Referência existe | Ausente / vazio | Não localizado | UUID histórico; foto não recuperada | Não localizado | Indisponível; depende de fonte |
| 4 | `uploads/vendedores/1f7e2b52a5ce4c229764f0e2d8ebe0ae.jpg` | 404 / 404 | Referência existe | Ausente / vazio | Não localizado | UUID histórico; foto não recuperada | Não localizado | Indisponível; depende de fonte |
| **5** | `uploads/vendedores/5fd02187cf964b7299eda093b2da51ee.jpg` | 404 / 404 | Referência existe | Ausente / vazio | Não localizado | UUID histórico; foto não recuperada | Não localizado | Indisponível; depende de fonte |
| **6** | `uploads/vendedores/bc860fc2619c4c78be113a54d308e8ee.jpg` | 404 / 404 | Referência existe | Ausente / vazio | Não localizado | UUID histórico; foto não recuperada | Não localizado | Indisponível; depende de fonte |
| **7** | `uploads/vendedores/aaba7bd55cc34666a60f30457ceb69c3.jpg` | 404 / 404 | Referência existe | Ausente / vazio | Não localizado | UUID histórico; foto não recuperada | Não localizado | Indisponível; depende de fonte |
| 8 | `uploads/vendedores/607dcb7d28a74de981a261086da410bf.jpg` | 404 / 404 | Referência existe | Ausente / vazio | Não localizado | UUID histórico; foto não recuperada | Não localizado | Indisponível; depende de fonte |
| 9 | `uploads/vendedores/326db14b2469457b8e9a76d5aa41535a.jpg` | 404 / 404 | Referência existe | Ausente / vazio | Não localizado | UUID histórico; foto não recuperada | Não localizado | Indisponível; depende de fonte |
| 10 | `uploads/vendedores/21d66f23ea6544f59a30123ec092908c.jpg` | 404 / 404 | Referência existe | Ausente / vazio | Não localizado | UUID histórico; foto não recuperada | Não localizado | Indisponível; depende de fonte |

Para cada linha: manter a referência enquanto se obtém original ou decisão explícita do proprietário. Um 404 não autoriza apagar FotoUrl nem associar foto de outra pessoa.

## Fontes efetivamente pesquisadas

| Fonte Anderson | Método e resultado |
| --- | --- |
| Container `andersonmultimarcas-web` | Inventário de `/app/wwwroot` e teste dos oito caminhos exatos; nenhum original |
| Volume Data Protection | `andersonmultimarcas-dataprotection-keys`, mount/labels do projeto; dois XML, sem imagem/upload. Nenhum volume de outro projeto acessado |
| Checkout legado | `/opt/startingweb/apps/anderson-multimarcas`: 278 arquivos fora de `.git`; quatro uploads de veículos/marca, nenhum dos UUIDs |
| Git legado | 1.432 objetos alcançáveis/todos os refs; busca dos oito UUIDs em caminhos históricos sem correspondência; quatro versões de appsettings sem R2 preenchido |
| Git local | Todos os refs/caminhos históricos, sem correspondência com os oito UUIDs |
| Raiz unificada | `/opt/startingweb/apps/anderson-multimarcas-unified`: 130 arquivos inventariados em shared/history/snapshots/incoming/bin; releases vazio, nenhum original/caminho de recuperação identificado |
| Imagem legada arquivada | `snapshots/preparation-20261106T003900Z/legacy-image.tar.gz`: seis camadas/5.617 membros; busca dos UUIDs por streaming, sem extração/restore, sem resultado |
| Artefato futuro | `incoming/rehearsal-artifact/image.tar.gz`: sete camadas/6.098 membros; mesmo método, sem resultado |
| Cinco snapshots Restic | Inventário individual: somente `database.bak` e `manifest.json`; nenhuma foto separada. Nenhum snapshot excluído/restaurado |
| Quatro backups nativos | Busca exata UTF-8/UTF-16LE dos oito UUIDs: encontrados no `.bak` de 28/07/2026 e nos três `.bak` retidos de 06–07/11. Nomes/hashes na evidência; nenhum restore |
| R2 | HEAD dos oito keys e oito listas limitadas ao prefixo UUID exato, MaxKeys=5; ausência nesse escopo. Nenhuma listagem global |

Inventários rasos de nomes em apps/backups e volumes serviram somente para localizar o projeto. Nenhum diretório Anderson adicional foi encontrado nos níveis raiz de `/opt/startingweb` e `/home/pedro` examinados. Não houve busca global nem acesso a outros projetos. Nenhuma instância isolada Anderson de restore/compat estava em execução; não foi criada restauração para essa busca.

`Vendedores1.JPG`, `Vendedores2.JPG` e `Vendedores3.JPG` versionados são usados como fotos institucionais/equipe na home, hero e CSS. Não há correspondência comprovada com cada FotoUrl individual; não foram considerados originais/substitutos.

## Limites e histórico

Os oito UUIDs já aparecem em bytes do backup de 28/07/2026, anterior a A/B/C. Isso confirma ocorrência histórica das strings, sem decodificar linhas antigas, associar IDs históricos ou provar bytes/existência/integridade das imagens naquela data. Não se atribuiu a falha às migrations.

O código atual conserva `.jpg`; UUID WebP pertence ao upload de veículos. Não se identificou mapa de migração/renomeação recuperável. Não se demonstrou quando/por que as fotos desapareceram, se chegaram ao R2 ou permaneceram em uploads locais de uma implantação anterior.

A conclusão é **mídia não recuperada nas fontes examinadas**. Não é prova de perda irreversível em todo acervo, computador do operador, conta antiga ou key renomeado desconhecido. Não foi auditada política de gestão Cloudflare; sucesso de leitura/lista restrita não demonstra isolamento nem permissão de escrita.

## Plano condicionado à fonte e autorização

1. Obter do proprietário ou de acervo Anderson identificado os originais dos IDs/keys acima. Registrar origem/escopo de eventual outra conta/backup próprio antes de inspecionar. Nenhuma fonte adicional recuperável foi confirmada nesta rodada.
2. Inspecionar cópia em diretório isolado: decodificação, formato real, dimensões, tamanho e SHA-256; confirmar correspondência individual com o proprietário e key esperado.
3. Repetir HEAD público/autenticado antes da reparação. Se o key passar a existir, preservar/reconciliar; não sobrescrever automaticamente.
4. Preparar manifesto por registro com origem, hash, dimensões/formato, key e operação exata. Pedir **autorização específica para restaurar os objetos produtivos enumerados**. Preservar FotoUrl quando o original permitir restaurar o mesmo key.
5. Somente após autorização, executar operações aprovadas; conferir HEAD/GET público/autenticado, bytes/hash e renderização. Registrar resultado e reversão sem exclusão ampla.

Sem os originais, o proprietário poderá fornecer novas fotos identificadas ou aprovar apresentação sem foto. Cada alternativa exige mudança explicitamente definida; nenhuma foi aplicada.

A [homologação sintética em destino isolado](./VALIDACAO_R2.md) é aprovação separada da restauração produtiva. Banco, URLs, container e volumes foram preservados; nenhum merge/deploy ou restore produtivo foi realizado.
