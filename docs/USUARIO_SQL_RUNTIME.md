# Identidade SQL de runtime — instalada no destino futuro

Atualização de 06/11/2026: criação do login/usuário e instalação da conexão do ambiente futuro foram expressamente autorizadas nesta rodada. `anderson_runtime` foi instalado às 17:26:45 Brasília, com grants mínimos verificados. O arquivo privado futuro deixou de usar `sa`. A identidade/conexão do legado continua preservada. Em 07/11, A/B/C foram aplicadas sob autorização separada; o runtime não foi recriado nem rotacionado.

## Resolvido em ambiente isolado

A aplicação não precisa de `sysadmin`, `db_owner`, criação de banco ou DDL no startup. O código usa EF/SqlClient; não foram identificadas procedures de negócio que exijam EXECUTE. As fixtures de migrations da suíte criam/removem bancos e são executadas com administrador **somente no SQL descartável**. Não se concede CREATE DATABASE ao runtime para acomodar uma fixture.

A proposta [runtime_permissions_trial.sql](../deploy/sql/runtime_permissions_trial.sql) é restrita às bases de ensaio. Login `anderson_runtime`, role própria `anderson_application` pertencente a dbo:

- CONNECT e SELECT/INSERT/UPDATE/DELETE em 14 tabelas explícitas: Loja, Marca, Veiculo, VeiculoCaracteristica, VeiculoMidia, Vendedor; ImportJob/Item/Log/History; AspNetUsers/Roles/UserRoles/RoleClaims.
- Somente SELECT nas tabelas Identity UserClaims/UserLogins/UserTokens (nenhum endpoint atual as modifica), em `__EFMigrationsHistory` e nas tabelas legadas AspNetMenus/AspNetMenuRoles, quando presentes. Escrita na configuração de menus legada não faz parte da aplicação nova nem dos fluxos aceitos neste ensaio.
- Nenhum grant para schema inteiro, outros bancos ou outras tabelas; nenhum EXECUTE geral. Sem grant option. Recusa roles/grants inesperados e identidade administrativa.
- DENY de criação de tabelas/views/procedures/funções/schemas, alterações de usuários/roles/schemas, backup e ALTER no schema dbo. Nenhuma associação a roles administrativas de servidor; a instância de ensaio também nega CREATE ANY DATABASE/ALTER ANY LOGIN/ALTER SERVER STATE.

Os testes verificam CRUD real via EF e HTTP, grants por objeto e recusa de CREATE/ALTER/DROP TABLE, CREATE USER/LOGIN/DATABASE, GRANT, alteração de role de servidor e DBCC administrativo. Operações de runtime são feitas com a identidade restrita, sem usar credenciais produtivas.

## Mecanismos seguros de credencial

[render_runtime_connection.py](../scripts/ops/render_runtime_connection.py) recebe senha em arquivo privado 0600 pertencente ao operador e cria uma proposta de conexão 0600 **fora do checkout**, com O_EXCL, sem imprimir a senha, conectar a SQL ou instalar runtime. Escapa a senha como valor ADO.NET literal, não como código shell. Os testes cobrem delimitadores, newline/injeção, permissões e recusa de sobrescrita. TLS é solicitado; `TrustServerCertificate=True` permite o certificado autoassinado atual e não comprova identidade TLS do servidor.

O procedimento de instalação abaixo já foi executado sob a autorização desta rodada. Qualquer rotação, revogação ou ampliação futura exige escopo próprio. Preservar a identidade do legado para o rollback; não remover guards de ensaio nem substituir política produtiva por SQL sem revisão.

## Executado e verificado

[provision_runtime.py](../scripts/ops/provision_runtime.py) foi testado em SQL descartável e depois executado no destino autorizado, com ID imutável do container, identidade da instância e GUID do banco conferidos. [runtime_permissions.production.sql](../deploy/sql/runtime_permissions.production.sql) possui guards próprios de destino/escopo/autorização; o perfil de permissões continua idêntico ao ensaiado. Há lock, recusa de adoção de login preexistente sem recibo próprio, replay sem rotação, senha gerada no servidor por CSPRNG, arquivos 0600 em diretório 0700 e passagem por stdin, sem senha em argumentos/logs/Git.

Recibo privado: `shared/runtime-provisioned.json`. O teste efetivo autenticado como runtime confirmou sysadmin=0, db_owner=0, CREATE TABLE/ALTER ANY USER/CREATE ANY DATABASE/ALTER ANY LOGIN=0 e quatro permissões CRUD em Marca. Os demais grants são os 14 objetos revisados; não há grant global/schema-wide. O arquivo anterior foi preservado protegido para recuperação. Somente a conexão DefaultConnectionProd do destino futuro foi substituída; os demais campos e o legado não foram alterados.

O instalador não aplica as migrations comerciais, não inicia app e não reinicia SQL. A prova de CRUD/negativas foi executada apenas no SQL descartável; em produção a verificação do acesso foi somente leitura. O CI agora também ensaia o instalador em SQL descartável, após gerar o schema revisável.

## Ainda bloqueante

A/B/C e LogoUrl foram resolvidos em 07/11. Acesso real somente leitura confirmou novamente login, 49 marcas/401 veículos, CRUD e ausência de privilégios administrativos; escrita/negativas e login administrativo sintético passaram apenas na cópia. [Recibo](./evidence/PRODUCTION_SQL_2026-11-07.json). R2 real, revisão independente, backup externo/aceite de risco e paridade continuam pendentes. A validação do executor continua recusando `sa`/identidade ausente, embora a conexão preparada já use a identidade dedicada. Os três gates continuam false.
