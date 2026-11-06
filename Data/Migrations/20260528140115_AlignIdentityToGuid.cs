using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignIdentityToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same migration ID and target schema: already-applied databases are unchanged.
            // Bootstrap repair: validate every existing key before touching constraints.
            // EF runs this operation in a transaction; unexpected dependencies fail closed.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [AspNetRoles] WHERE TRY_CONVERT(uniqueidentifier, [Id]) IS NULL OR DATALENGTH(CONVERT(nvarchar(max), [Id])) <> 72 OR LOWER(CONVERT(nvarchar(450), [Id])) <> LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, [Id]))))
                    THROW 51000, 'Identity migration blocked: AspNetRoles.Id contains a non-GUID key; review mapping before migration.', 1;
                IF EXISTS (SELECT 1 FROM [AspNetUsers] WHERE TRY_CONVERT(uniqueidentifier, [Id]) IS NULL OR DATALENGTH(CONVERT(nvarchar(max), [Id])) <> 72 OR LOWER(CONVERT(nvarchar(450), [Id])) <> LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, [Id]))))
                    THROW 51000, 'Identity migration blocked: AspNetUsers.Id contains a non-GUID key; review mapping before migration.', 1;
                IF EXISTS (SELECT 1 FROM [AspNetRoleClaims] WHERE TRY_CONVERT(uniqueidentifier, [RoleId]) IS NULL OR DATALENGTH(CONVERT(nvarchar(max), [RoleId])) <> 72 OR LOWER(CONVERT(nvarchar(450), [RoleId])) <> LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, [RoleId]))))
                    THROW 51000, 'Identity migration blocked: AspNetRoleClaims.RoleId contains a non-GUID key; review mapping before migration.', 1;
                IF EXISTS (SELECT 1 FROM [AspNetUserClaims] WHERE TRY_CONVERT(uniqueidentifier, [UserId]) IS NULL OR DATALENGTH(CONVERT(nvarchar(max), [UserId])) <> 72 OR LOWER(CONVERT(nvarchar(450), [UserId])) <> LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, [UserId]))))
                    THROW 51000, 'Identity migration blocked: AspNetUserClaims.UserId contains a non-GUID key; review mapping before migration.', 1;
                IF EXISTS (SELECT 1 FROM [AspNetUserLogins] WHERE TRY_CONVERT(uniqueidentifier, [UserId]) IS NULL OR DATALENGTH(CONVERT(nvarchar(max), [UserId])) <> 72 OR LOWER(CONVERT(nvarchar(450), [UserId])) <> LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, [UserId]))))
                    THROW 51000, 'Identity migration blocked: AspNetUserLogins.UserId contains a non-GUID key; review mapping before migration.', 1;
                IF EXISTS (SELECT 1 FROM [AspNetUserRoles] WHERE TRY_CONVERT(uniqueidentifier, [UserId]) IS NULL OR DATALENGTH(CONVERT(nvarchar(max), [UserId])) <> 72 OR LOWER(CONVERT(nvarchar(450), [UserId])) <> LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, [UserId]))))
                    THROW 51000, 'Identity migration blocked: AspNetUserRoles.UserId contains a non-GUID key; review mapping before migration.', 1;
                IF EXISTS (SELECT 1 FROM [AspNetUserRoles] WHERE TRY_CONVERT(uniqueidentifier, [RoleId]) IS NULL OR DATALENGTH(CONVERT(nvarchar(max), [RoleId])) <> 72 OR LOWER(CONVERT(nvarchar(450), [RoleId])) <> LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, [RoleId]))))
                    THROW 51000, 'Identity migration blocked: AspNetUserRoles.RoleId contains a non-GUID key; review mapping before migration.', 1;
                IF EXISTS (SELECT 1 FROM [AspNetUserTokens] WHERE TRY_CONVERT(uniqueidentifier, [UserId]) IS NULL OR DATALENGTH(CONVERT(nvarchar(max), [UserId])) <> 72 OR LOWER(CONVERT(nvarchar(450), [UserId])) <> LOWER(CONVERT(nvarchar(36), TRY_CONVERT(uniqueidentifier, [UserId]))))
                    THROW 51000, 'Identity migration blocked: AspNetUserTokens.UserId contains a non-GUID key; review mapping before migration.', 1;
                IF EXISTS (SELECT TRY_CONVERT(uniqueidentifier, [Id]) FROM [AspNetRoles] GROUP BY TRY_CONVERT(uniqueidentifier, [Id]) HAVING COUNT(*) > 1)
                    THROW 51001, 'Identity migration blocked: duplicate GUID representations in AspNetRoles.', 1;
                IF EXISTS (SELECT TRY_CONVERT(uniqueidentifier, [Id]) FROM [AspNetUsers] GROUP BY TRY_CONVERT(uniqueidentifier, [Id]) HAVING COUNT(*) > 1)
                    THROW 51001, 'Identity migration blocked: duplicate GUID representations in AspNetUsers.', 1;
                ALTER TABLE [AspNetRoleClaims] DROP CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId];
                ALTER TABLE [AspNetUserClaims] DROP CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId];
                ALTER TABLE [AspNetUserLogins] DROP CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId];
                ALTER TABLE [AspNetUserRoles] DROP CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId];
                ALTER TABLE [AspNetUserRoles] DROP CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId];
                ALTER TABLE [AspNetUserTokens] DROP CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId];
                DROP INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims];
                DROP INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims];
                DROP INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins];
                DROP INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles];
                ALTER TABLE [AspNetRoles] DROP CONSTRAINT [PK_AspNetRoles];
                ALTER TABLE [AspNetUsers] DROP CONSTRAINT [PK_AspNetUsers];
                ALTER TABLE [AspNetUserRoles] DROP CONSTRAINT [PK_AspNetUserRoles];
                ALTER TABLE [AspNetUserTokens] DROP CONSTRAINT [PK_AspNetUserTokens];
                ALTER TABLE [AspNetRoles] ALTER COLUMN [Id] uniqueidentifier NOT NULL;
                ALTER TABLE [AspNetUsers] ALTER COLUMN [Id] uniqueidentifier NOT NULL;
                ALTER TABLE [AspNetRoleClaims] ALTER COLUMN [RoleId] uniqueidentifier NOT NULL;
                ALTER TABLE [AspNetUserClaims] ALTER COLUMN [UserId] uniqueidentifier NOT NULL;
                ALTER TABLE [AspNetUserLogins] ALTER COLUMN [UserId] uniqueidentifier NOT NULL;
                ALTER TABLE [AspNetUserRoles] ALTER COLUMN [UserId] uniqueidentifier NOT NULL;
                ALTER TABLE [AspNetUserRoles] ALTER COLUMN [RoleId] uniqueidentifier NOT NULL;
                ALTER TABLE [AspNetUserTokens] ALTER COLUMN [UserId] uniqueidentifier NOT NULL;
                ALTER TABLE [AspNetRoles] ADD CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id]);
                ALTER TABLE [AspNetUsers] ADD CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]);
                ALTER TABLE [AspNetUserRoles] ADD CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]);
                ALTER TABLE [AspNetUserTokens] ADD CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]);
                CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
                CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
                CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
                CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
                ALTER TABLE [AspNetRoleClaims] ADD CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE;
                ALTER TABLE [AspNetUserClaims] ADD CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
                ALTER TABLE [AspNetUserLogins] ADD CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
                ALTER TABLE [AspNetUserRoles] ADD CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE;
                ALTER TABLE [AspNetUserRoles] ADD CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
                ALTER TABLE [AspNetUserTokens] ADD CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Identity keys are a data contract. Application rollback never downgrades schema.
            // Reverting GUID keys needs a separately reviewed mapping and constraint plan.
            migrationBuilder.Sql("THROW 51002, 'Identity downgrade blocked: retain GUID keys and restore the application artifact; schema reversal requires a reviewed plan.', 1;");
        }
    }
}
