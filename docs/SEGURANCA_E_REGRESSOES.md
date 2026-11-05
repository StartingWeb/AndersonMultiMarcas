# Segurança e regressões — 05/11/2026

Implementação na branch local `feat/unified-cicd-deployment`, preservando o trabalho de CI/CD existente. Os testes usam identidades sintéticas, armazenamento em memória e bancos SQL Server descartáveis. Nenhum usuário real teve senha, perfil ou dados alterados.

## Autorização e API

As quatro operações de escrita em `api/veiculos` exigem sessão autenticada, autorização por perfil/permissão e token antiforgery. Falta de autenticação retorna **401**, falta de permissão retorna **403**, e ausência de token antiforgery retorna **400**, incluindo upload multipart. A consulta pública da lista e dos detalhes continua anônima.

| Operação | Permissão exigida |
| --- | --- |
| Criar, atualizar ou enviar mídias | `AdminMenu:veiculos` |
| Marcar como vendido | `AdminMenu:vendas-veiculos` |
| Administração de usuários/perfis e importação/validação legada | Desenvolvedor |
| Sincronização R2 | Administrador, AdminConcessionaria ou Desenvolvedor; respeita permissões configuradas |

Desenvolvedor acessa os menus administrativos. Administrador/AdminConcessionaria mantêm acesso aos menus comuns quando o perfil ainda não possui permissões configuradas; quando existem claims `AdminMenu`, elas restringem o acesso. Perfis personalizados precisam de permissão explícita. Sessão sem perfil/permissão administrativa não acessa a administração. As mesmas regras controlam menus e acesso direto às rotas Razor; ocultar o menu deixa de ser a única proteção.

Para clientes da API que usam o cookie Identity, primeiro fazer GET autenticado em `/api/security/antiforgery`, preservar cookies e enviar o campo `token` no cabeçalho indicado por `headerName` (`X-CSRF-TOKEN`) em cada escrita. O endpoint de token não permite cache.

Implementação: [políticas](../Project/Infrastructure/Security/AdminAuthorization.cs), [controller](../Project/Features/Veiculos/Controllers/VeiculosController.cs), [configuração](../Project/Program.cs).

## Identidades, bootstrap e recuperação

Foram removidas as contas privilegiadas com senhas constantes do seed e o reset administrativo previsível. O bootstrap é desativado por padrão e só é chamado em Development. Para criar **uma conta local nova**, configurar externamente:

- `DevelopmentBootstrap__Enabled=true`;
- `DevelopmentBootstrap__Email`;
- `DevelopmentBootstrap__UserName`;
- `DevelopmentBootstrap__Password`;
- opcionalmente `DevelopmentBootstrap__NomeCompleto`.

Não colocar a senha em arquivos versionados. O bootstrap não altera senha, confirmação de e-mail ou papéis de identidades preexistentes, mesmo se o e-mail ou nome informado já existir.

A ação administrativa de recuperação gera um link individual com validade de uma hora; **não muda a senha no momento da geração**. O usuário escolhe a nova senha na página `/ResetPassword`, protegida contra CSRF. O token é ligado à identidade e não pode ser reutilizado após a troca. A página envia `Referrer-Policy: no-referrer`, `noindex,nofollow` e `no-store`; a tela de usuários também impede cache. Como não há entrega de e-mail configurada no projeto, o desenvolvedor entrega o link por um canal privado; nenhum e-mail é enviado automaticamente.

Novas senhas exigem pelo menos 12 caracteres, maiúscula, minúscula, número e símbolo. O login conta falhas e bloqueia por 15 minutos após cinco tentativas inválidas. Isso não redefine senhas antigas nem executa alterações em contas reais. Uma eventual rotação das credenciais antigas de ambientes existentes precisa de um procedimento próprio e autorizado.

Implementação: [bootstrap](../Data/IdentitySeed.cs), [gestão de usuários](../Project/Pages/Admin/Auth/Usuarios/Index.cshtml.cs), [nova senha](../Project/Pages/ResetPassword.cshtml.cs), [login](../Project/Pages/Login.cshtml.cs).

## Regressões corrigidas

| Problema | Correção e evidência |
| --- | --- |
| Ordenação por `PrecoVenda.Valor` não traduzida | Ordena a propriedade convertida `PrecoVenda`, com desempate por Id. Teste executa handler no SQL Server real, asc/desc, empate, páginas e filtros min/max/faixa. |
| Edição apaga vídeo, vendedor e notas omitidos | DTO distingue propriedade omitida de `null` explícito. Handler preserva omissões e mantém DataVenda/Vendido; `null` explícito permite limpar. Coberto em InMemory e SQL Server. |
| Retry ignora item com erro abaixo do limite | Erro só é terminal quando esgota tentativas. Falha transitória avança; falha permanente para no limite. Retry manual adiciona novo orçamento limitado e conserva contagem anterior. |
| JSON-LD perde `@context` e `@type` | Vehicle, Brand, Offer, ItemList e ListItem usam dicionários com chaves corretas; serializador escapa conteúdo, inclusive `</script>`. Detalhe da API usa canonical da página pública. |
| Paginação aceita tamanho ilimitado/offset inválido | Página fica entre 1 e 1.000.000 e tamanho entre 1 e 100, sem overflow no offset. |
| Imagem inválida gera HTTP 500 na API | Falhas de decodificação e limites retornam erro de entrada 400. Upload válido com sessão e CSRF é exercitado por HTTP, usando armazenamento em memória e decodificação/encoding reais. |

Testes: [autorização HTTP](../Project.Tests/Security/VehicleApiAuthorizationTests.cs), [tokens e bootstrap](../Project.Tests/Security/IdentitySecurityTests.cs), [perfis e permissões](../Project.Tests/Security/AdminAuthorizationTests.cs), [regressões SQL](../Project.Tests/Database/VehicleSqlRegressionTests.cs), [retry](../Project.Tests/Features/Storage/Legacy/LegacyVehicleImageImportRetryTests.cs), [JSON-LD](../Project.Tests/Shared/SeoJsonLdTests.cs), [edição](../Project.Tests/Features/Veiculos/VeiculoCommandHandlersTests.cs).

## Execução verificada

Em 05/11/2026 foi executada a suíte completa .NET com `ANDERSON_TEST_SQL` apontando para o SQL Server local descartável, carregada de configuração local ignorada pelo Git sem imprimir o segredo:

```text
Passed! - Failed: 0, Passed: 114, Skipped: 0, Total: 114
.NET 10; duração dos testes: 9 segundos
```

Esse registro é a fotografia da execução desta frente, não substitui a validação consolidada final após alterações de outras frentes. Nessa execução os testes SQL aplicaram migrations em bases com nome único, realizaram consultas/escritas sintéticas e removeram as bases em `finally`. Também houve passagem anterior com 112 testes. A execução final desta frente compilou sem warnings; `git diff --check` passou. A busca direcionada confirmou a remoção dos literais privilegiados antigos no código atual.

Os testes HTTP usam a aplicação real via WebApplicationFactory/TestServer, cookie Identity obtido pelo formulário de login e tokens antiforgery reais. Não substituem autenticação por cabeçalhos de teste. InMemory não prova integridade relacional; por isso preço e edição têm testes separados em SQL Server. O retry é validado sem acesso à origem legada/R2 real: o caso permanente usa uma URL proibida pelo guard, e o caso de progresso usa dry-run. Esses ensaios não provam importação ponta a ponta em um bucket externo.

## Revisão adicional de uploads e consistência

Um probe em SQL Server descartável reproduziu falha ao enviar imagens quando a capa existente era a segunda mídia: o handler marcava a primeira como capa sem limpar a anterior, violando o índice único recuperado. O handler agora remove a marcação antiga, persiste e marca a primeira imagem ativa ordenada, dentro da mesma transação. Em falha de persistência, reverte a transação e só tenta remover arquivos novos quando uma consulta ao banco confirma que não há referência ao BlobName ou URL. Uma falha de consulta impede a remoção e registra necessidade de revisão. Isso também protege o resultado incerto de um commit e chaves reutilizadas.

Os dois testes SQL adicionais reproduzem o cenário da capa e uma falha de inserção por campo maior que o schema. Confirmam capa única, rollback, limpeza do arquivo novo e preservação de um arquivo já referenciado. A execução direcionada com quatro testes SQL de veículo e 18 testes HTTP passou: **22 aprovados, zero falhas/ignorados**.

O serviço de upload agora valida e normaliza **todo o lote antes de gravar o primeiro arquivo**. Uma segunda imagem inválida não deixa a primeira órfã. Se o storage falhar, tenta compensar somente as chaves GUID geradas nessa operação, incluindo a tentativa que pode ter sido aceita antes de um timeout. Falhas de limpeza são registradas, sem informar que uma exclusão remota foi confirmada. [Testes do lote](../Project.Tests/Images/VehicleMediaBatchTests.cs).

A revisão também reproduziu truncamento de chaves pelo `TRY_CONVERT(uniqueidentifier)` e a falha do downgrade histórico de Identity por dependências de PK/FK. Esses achados foram repassados à frente de migrations, responsável por rejeitar chaves não canônicas e explicitar o bloqueio de downgrade de Identity. O rollback operacional do deploy não deve executar downgrade de schema.
