# Proposta de identidade SQL de runtime

Preparação apenas. Nenhum login, usuário ou grant foi criado/alterado na instância produtiva. O runtime preparado ainda herda `sa` e continua recusado pelo executor.

## Resolvido em ambiente isolado

A aplicação não precisa de `sysadmin`, `db_owner`, criação de banco ou DDL no startup. O código usa EF/SqlClient; não foram identificadas procedures de negócio que exijam EXECUTE. As fixtures de migrations da suíte criam/removem bancos e são executadas com administrador **somente no SQL descartável**. Não se concede CREATE DATABASE ao runtime para acomodar uma fixture.

A proposta [runtime_permissions_trial.sql](../deploy/sql/runtime_permissions_trial.sql) é restrita às bases de ensaio. Login `anderson_runtime`, role própria `anderson_application` pertencente a dbo:

- CONNECT e SELECT/INSERT/UPDATE/DELETE em 14 tabelas explícitas: Loja, Marca, Veiculo, VeiculoCaracteristica, VeiculoMidia, Vendedor; ImportJob/Item/Log/History; AspNetUsers/Roles/UserRoles/RoleClaims.
- Somente SELECT nas tabelas Identity UserClaims/UserLogins/UserTokens (nenhum endpoint atual as modifica), em `__EFMigrationsHistory` e nas tabelas legadas AspNetMenus/AspNetMenuRoles, quando presentes. Escrita na configuração de menus legada não faz parte da aplicação nova nem dos fluxos aceitos neste ensaio.
- Nenhum grant para schema inteiro, outros bancos ou outras tabelas; nenhum EXECUTE geral. Sem grant option. Recusa roles/grants inesperados e identidade administrativa.
- DENY de criação de tabelas/views/procedures/funções/schemas, alterações de usuários/roles/schemas, backup e ALTER no schema dbo. Nenhuma associação a roles administrativas de servidor; a instância de ensaio também nega CREATE ANY DATABASE/ALTER ANY LOGIN/ALTER SERVER STATE.

Os testes verificam CRUD real via EF e HTTP, grants por objeto e recusa de CREATE/ALTER/DROP TABLE, CREATE USER/LOGIN/DATABASE, GRANT, alteração de role de servidor e DBCC administrativo. Operações de runtime são feitas com a identidade restrita, sem usar credenciais produtivas.

## Preparado, aguardando autorização

[render_runtime_connection.py](../scripts/ops/render_runtime_connection.py) recebe senha em arquivo privado 0600 pertencente ao operador e cria uma proposta de conexão 0600 **fora do checkout**, com O_EXCL, sem imprimir a senha, conectar a SQL ou instalar runtime. Escapa a senha como valor ADO.NET literal, não como código shell. Os testes cobrem delimitadores, newline/injeção, permissões e recusa de sobrescrita. TLS é solicitado; `TrustServerCertificate=True` permite o certificado autoassinado atual e não comprova identidade TLS do servidor.

Para futura instalação: gerar segredo exclusivo fora do Git; revisar identidade da instância e os grants; autorizar separadamente CREATE LOGIN/USER na produção; aplicar por canal administrativo sem senha em argumentos/logs; validar permissões efetivas e fluxos; atualizar apenas o arquivo privado de runtime com escrita atômica e modo 0600. A versão produtiva dos scripts precisa de revisão própria: não remover o guard das bases de ensaio por conta própria. Preservar o usuário antigo do legado para o rollback, sem revogação implícita.

## Bloqueante

O login produtivo dedicado e a instalação da conexão **não foram autorizados nem executados**. A validação de nome não prova privilégios de outros logins: a instalação futura exige conferir roles, grants diretos/efetivos e propriedade do banco. `sa` e identidade ausente continuam sendo recusados pelo executor. Os três gates de ativação continuam false.
