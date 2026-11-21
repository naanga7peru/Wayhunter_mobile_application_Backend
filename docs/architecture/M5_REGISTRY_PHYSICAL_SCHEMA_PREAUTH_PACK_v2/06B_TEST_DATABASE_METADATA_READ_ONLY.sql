-- READ ONLY: FUTURE use only on explicitly inspected isolated test database.
-- This database DOES NOT currently exist. Do not execute now.
SET NOCOUNT ON;
IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest'
    THROW 52201, 'Wrong database', 1;
SELECT DB_NAME() DatabaseName, ORIGINAL_LOGIN() OriginalLogin,
 CAST(SERVERPROPERTY('InstanceName') AS nvarchar(128)) InstanceName;
SELECT t.name TableName, c.column_id, c.name ColumnName, typ.name SqlType,
 c.max_length, c.is_nullable, c.collation_name
FROM sys.tables t JOIN sys.columns c ON t.object_id=c.object_id
JOIN sys.types typ ON c.user_type_id=typ.user_type_id
WHERE t.name IN (N'GameAttempts',N'GameProofEvidence',N'GameProofSubmissionOperations',N'GameProofMediaClaims')
ORDER BY t.name,c.column_id;
SELECT t.name TableName, i.name IndexName, i.is_unique, i.type_desc,
 i.has_filter, i.filter_definition, ic.key_ordinal, c.name KeyColumn
FROM sys.tables t JOIN sys.indexes i ON t.object_id=i.object_id
LEFT JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
LEFT JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE t.name IN (N'GameAttempts',N'GameProofEvidence',N'GameProofSubmissionOperations',N'GameProofMediaClaims')
ORDER BY t.name,i.name,ic.key_ordinal;
SELECT OBJECT_NAME(parent_object_id) TableName, name FKName,
 delete_referential_action_desc, update_referential_action_desc,
 is_disabled, is_not_trusted FROM sys.foreign_keys
WHERE parent_object_id IN (OBJECT_ID(N'dbo.GameProofMediaClaims'),OBJECT_ID(N'dbo.GameProofSubmissionOperations'));
SELECT OBJECT_NAME(parent_object_id) TableName, name CheckName, definition,
 is_disabled, is_not_trusted FROM sys.check_constraints
WHERE parent_object_id=OBJECT_ID(N'dbo.GameProofMediaClaims');
SELECT OBJECT_NAME(parent_id) TableName,name TriggerName,is_disabled FROM sys.triggers
WHERE parent_id IN (OBJECT_ID(N'dbo.GameProofMediaClaims'),OBJECT_ID(N'dbo.GameProofSubmissionOperations'),OBJECT_ID(N'dbo.GameProofEvidence'));
SELECT name RoleName FROM sys.database_principals WHERE name=N'm5_registry_test_executor';
