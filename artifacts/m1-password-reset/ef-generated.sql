BEGIN TRANSACTION;
CREATE TABLE [IdentityPasswordResetCredentials] (
    [CredentialId] uniqueidentifier NOT NULL,
    [ConsumedAtUtc] datetimeoffset NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [ExpiresAtUtc] datetimeoffset NOT NULL,
    [RevokedAtUtc] datetimeoffset NULL,
    [TokenHash] char(64) NOT NULL,
    [UserId] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_IdentityPasswordResetCredentials] PRIMARY KEY ([CredentialId]),
    CONSTRAINT [CK_IdentityPasswordResetCredentials_Expiry] CHECK ([ExpiresAtUtc] > [CreatedAtUtc]),
    CONSTRAINT [FK_IdentityPasswordResetCredentials_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
);

CREATE INDEX [IX_IdentityPasswordResetCredentials_User_Expiry] ON [IdentityPasswordResetCredentials] ([UserId], [ExpiresAtUtc]);

CREATE UNIQUE INDEX [UX_IdentityPasswordResetCredentials_TokenHash] ON [IdentityPasswordResetCredentials] ([TokenHash]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261121100000_AddIdentityPasswordResetCredentials', N'10.0.15');

COMMIT;
GO
