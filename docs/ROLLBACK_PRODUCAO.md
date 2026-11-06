# Rollback da aplicação

Atualizado em 05/11/2026, horário de Brasília. Preparação autorizada; **corte de produção não autorizado nem executado**.

## Baseline preservado

| Item | Valor |
| --- | --- |
| Container vivo | `andersonmultimarcas-web` |
| ID | `9842fa7e0f745dc589ddc9ee230813840524ff65c8aecb3d54cf6eaf7f0c2a78` |
| Revisão Release | `fb8a205768dce239c960ffb1cfb043b2680de7b3` |
| Imagem/digest | `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5` |
| Rede/upstream | `startingweb_network`, `andersonmultimarcas-web:8080` |
| DP | `andersonmultimarcas-dataprotection-keys`, app `AndersonMultiMarcas` |
| Nome reservado depois do corte | `andersonmultimarcas-legacy-fb8a205768dc` — ainda não renomeado |
| Snapshot privado | `/opt/startingweb/apps/anderson-multimarcas-unified/snapshots/preparation-20261106T003900Z` |
| Arquivo da imagem | `legacy-image.tar.gz`, 151,708,109 bytes |
| SHA-256 do arquivo | `230ddb5a956a4fbefe065670376e6502cbc60f5c73baf8a5a60cf4673414350a` |
| Validação | Gzip/tar, hashes dos blobs OCI e docker load aprovados |

Metadados completos do container e configuração real foram guardados em arquivo 0600 sob diretório 0700, fora do Git. Não imprimi-los em Actions ou incidentes. Os proxy hosts 4 e 5, seus certificados, aliases e mounts foram inventariados e não modificados. O arquivo de imagem não depende da tag latest nem do prune diário.

## O que foi testado

Nove cenários com Docker real e recursos descartáveis no Mac passaram: deploy válido; falha HTTP503; falha de executável; imagem inválida; checksum adulterado; configuração insegura; transferência interrompida; rollback manual pelo arquivo preservado; retorno ao mesmo container legado simulado. Conferiu-se ausência final de containers, volumes, redes e tags de teste.

O executor foi ampliado para a identidade OCI do servidor e retestado; a imagem .NET real foi carregada e ligada a SQL restaurado isolado. Houve retorno de gates locais pela chave SSH real e recusa de shell/SFTP/leitura. As chaves DP persistiram entre dois containers de aplicação de teste. O teste não parou/reiniciou o container legado real e não desviou tráfego pelo NPM.

## Limites

Rollback de aplicação volta imagem e snapshot de configuração. Não desfaz dados, SQL, migrations, importações ou objetos R2. Um restore produtivo pode perder gravações posteriores e exige autorização separada. A proposta SQL de ampliação não reduz colunas nem altera registros, mas sua adoção e a política de duplicidade de marcas seguem em revisão; compatibilidade de escrita/rollback deve ser aceita antes do corte.

`/health` comprova liveness. Para release unificada, exigir também o commit em `/health/release`, HTTPS público e fluxos relevantes. O legado não tem esse endpoint: hoje retorna 404; usar seu ID/digest registrado e health, sem inventar um SHA de fonte para o binário.

## Primeira publicação: retorno ao legado

Somente após autorização explícita do corte:

1. Suspender gatilhos do Release e verificar que não existe outra publicação em andamento.
2. Confirmar imagem offline, snapshot privado, volume DP, backup recente e compatibilidade SQL.
3. Parar e renomear o legado para o nome reservado, preservando o mesmo container e seus mounts/aliases. Não removê-lo.
4. Instalar o pacote aprovado pelo executor. Se o health interno falhar, o journal permite voltar ao mesmo ID legado.
5. Se o health interno passar e a verificação HTTPS/commit falhar, rollback sem alvo volta ao legado retido, mesmo sem release gerida anterior.
6. Conferir o legado por ID/imagem, health interno, catálogo, HTTPS e imagens. Manter novas tentativas suspensas até diagnóstico.

Nenhum passo de troca acima foi executado na preparação.

## Executor e transações

[release.py](../scripts/deploy/release.py) exige lock, habilitação local, aprovação de paridade e pacote íntegro. Recusa container vivo não gerido. Cada release guarda imagem/manifesto/configuração imutáveis. `current.json` referencia o ID imutável local; `release.json` preserva a identidade produzida no CI. O ID de configuração clássico pode diferir do ID OCI após load; ambos são vinculados pela configuração/rootfs autenticados.

O journal `transaction.json` registra o baseline antes da troca. Em interrupção, rollback sem alvo recupera a transação antes de permitir novo deploy. Falha de recuperação é reportada como falha e mantém evidências; não executar migrations para tentar corrigi-la. Uma falha de escrita no histórico não deve impedir a tentativa de voltar à aplicação.

Comandos futuros, apenas sob autorização operacional:

```sh
python3 -I -B /opt/startingweb/apps/anderson-multimarcas-unified/bin/release.py rollback \
  --root /opt/startingweb/apps/anderson-multimarcas-unified

# A partir da segunda release gerida, é possível escolher um pacote local retido.
python3 -I -B /opt/startingweb/apps/anderson-multimarcas-unified/bin/release.py rollback \
  --root /opt/startingweb/apps/anderson-multimarcas-unified --release SHA_COMPLETO_REVISADO
```

Não escolher um commit apenas porque ele existe no Git: pacote e configuração precisam estar preservados e compatíveis com o banco atual. Não usar fetch/reset/build do deploy antigo como rollback. Não executar prune, compose down -v ou limpeza de volumes/release durante recuperação.

## Emergência e retenção

Se o executor não conseguir recuperar, preservar journal e snapshots, identificar o baseline e planejar manualmente a liberação do nome do container novo e retorno do antigo. Manter NPM, rede e volume DP. Se faltar imagem, conferir SHA e carregar o arquivo offline; não reconstruir a partir da main corrente. Se houver incompatibilidade de banco, interromper e adotar o plano de banco aprovado, sem restore automático.

Manter aplicação ativa, pelo menos um baseline anterior e legado inicial durante o período de observação a ser definido na autorização. Arquivos de configuração nos snapshots contêm segredos e exigem 0600/0700. Revalidar os gates, Environment automático e proteção/revisão da main e manter publicações suspensas durante incidentes. Ver [ATIVACAO_PRODUCAO.md](./ATIVACAO_PRODUCAO.md).

## Preflight e recuperação

O deploy novo exige backup íntegro/recente e schema conhecido compatível pelo checker fixo. Rollback não exige esse checker: uma falha de backup não deve impedir restaurar a aplicação anterior. Lock, gates locais, integridade do pacote, configurações e health continuam obrigatórios. Em falha de rollback o journal permanece; bloquear novas publicações e acionar recuperação manual. Não religar gates nem remover journal para forçar sucesso.

A ampliação proposta na PR #3 preserva dados/larguras, mas o novo ensaio da PR #3 demonstrou operações representativas de escrita do binário antigo depois dessas alterações, inclusive editando os máximos gravados pela aplicação nova. O índice único continua bloqueado por dados duplicados. Assim, rollback binário não é autorização para alterar/reverter schema e o primeiro corte segue bloqueado.

## Ensaio cruzado adicional

Quatro fases em cópia restaurada passaram com 78 checkpoints: legado no original, legado após propostas, novo após propostas e retorno ao legado mantendo ampliação. Os máximos novos não foram truncados na edição pelo legado; upload, bytes, referência, remoção física de mídia, exclusão lógica e arquivos-sentinela passaram com SQL restrito. Esse resultado usa Local storage isolado; o rollback de arquivos no R2 real continua bloqueante.

O undo de nomes/índice fornecido na PR #3 é uma ferramenta separada e restrita ao ensaio. Não faz parte do rollback binário e não pode ser usado para reduzir colunas ou restaurar banco automaticamente. Não revogar o login antigo nem remover o legado antes de autorização própria.
