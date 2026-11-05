# Pendências para futura ativação

Atualizado em 05/11/2026. O trabalho é local, na branch `feat/unified-cicd-deployment`. Não houve push, PR, merge, deploy, reinício, alteração de banco, R2 ou Nginx Proxy Manager. O mecanismo legado continua ativo.

## Estado observado sem escrita remota

Servidor `startingweb-server`: Ubuntu 24.04.4. Container `andersonmultimarcas-web`, imagem `startingweb/andersonmultimarcas-release:latest`, ID `sha256:605b1b3cd5f1485442fa2ddfb8bd6e7ac68edcffe63f95c1fa39f8ef98e66df5`, iniciado em `2026-10-23T20:43:11.986710244Z`, saudável no início e nas conferências posteriores da atividade, com o mesmo horário de início, image ID e RestartCount=1. As consultas utilizaram somente `docker ps`, `docker inspect`, `uname` e identificação do sistema; nenhum segredo de ambiente foi impresso.

A referência Git do Release é `fb8a205768dce239c960ffb1cfb043b2680de7b3`. A comparação somente leitura por SHA-256 confirmou que **Project.dll, Core.dll, Data.dll e Domain.dll (4/4) e os 188 arquivos estáticos sem uploads (188/188)** no container produtivo são idênticos aos do Release Git usado na recuperação. Não se coletaram configurações, credenciais ou uploads reais. Isso identifica a versão do código publicada; equivalência de comportamento depende também do schema e da configuração, ainda não reproduzidos a partir de produção.

## Ações externas necessárias

| Pendência | Evidência/impacto | Ação para desbloquear |
|---|---|---|
| Revisão da branch e autorização de envio | Tudo permanece local; CI/CD não foi disparado no GitHub. | Autorizar push/PR em etapa própria; revisar e executar CI remoto antes de aprovar integração. |
| Aprovação formal de paridade | DLLs 4/4 e estáticos 188/188 coincidem com o Release investigado; matriz local documentada. | Revisar os testes e a matriz, aprovar o manifest de paridade para a futura ativação. |
| Schema/dados exatos de produção | Migrations e SQL validados em SQL isolado/sintético; banco produtivo não foi alterado. | Fornecer backup sanitizado autorizado, validar restauração/constraints/histórico; revisar SQL e planejar backup/janela antes de qualquer aplicação. |
| R2 de teste | Compatibilidade S3 e falhas/retry validadas com dublês; nenhuma escrita em bucket real. | Disponibilizar bucket exclusivo e credenciais de teste por segredo local; executar upload/HEAD/read/delete apenas nele. |
| Environment GitHub | Workflow exige reviewers/main-only; configuração remota não foi alterada. | Criar/proteger `production`, revisar bypass administrativo, permissões e branch rules. |
| SSH e servidor | Transporte, host key e gates estão implementados, sem provisionar destino real. | Configurar secrets SSH/known_hosts e diretório unificado em operação autorizada; revisar usuário e permissões. |
| Configuração runtime | Exige SQL, R2 se utilizado, chaves persistentes, portas/volumes e health público. | Provisionar arquivos de ambiente e diretórios fora do Git; revisar redigido antes de ativar. |
| Habilitação de publicação | Variáveis de habilitação e paridade continuam ausentes/falsas; segurança/integridade continuam obrigatórias. | Só após validações e aprovação específica, definir gates e realizar primeira publicação controlada com rollback preparado. |
| Migração entre repositórios | Não se desativou legado nem arquivou repositório. | Fazer corte coordenado e observar nova publicação; arquivamento/desativação exigem autorização separada. |

Nenhuma dessas pendências foi contornada com acesso ao iPhone, execução cloud substituta ou escrita remota. O código está preparado para revisão e ensaio controlado; a ativação produtiva depende das evidências e autorizações acima. Não afirmar migração de produção concluída.
