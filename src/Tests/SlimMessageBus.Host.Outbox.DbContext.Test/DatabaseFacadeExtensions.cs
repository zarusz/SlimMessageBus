namespace SlimMessageBus.Host.Outbox.DbContext.Test;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

public static class DatabaseFacadeExtensions
{
    public static async Task DropSchemaIfExistsAsync(this DatabaseFacade database, string schema, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            IF EXISTS(
                SELECT schema_name
                FROM information_schema.schemata
                WHERE schema_name = @p0
            )
            BEGIN
                DECLARE @sql NVARCHAR(MAX);
                SELECT @sql = CONCAT('DROP SCHEMA ', @p0);
                EXEC(@sql)
                PRINT 'Schema dropped.'
            END
            """;

        try
        {
            await database.EnsureSchemaIsEmptyAsync(schema, cancellationToken);
            await database.ExecuteSqlRawAsync(sql, [schema], cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 4060)
        {
            // db does not exist
        }
    }

    public static async Task EnsureSchemaIsEmptyAsync(this DatabaseFacade database, string schema, CancellationToken cancellationToken = default)
    {
        Debug.Assert(!schema.Equals("dbo", StringComparison.OrdinalIgnoreCase));

        FormattableString sql = $"""
            IF EXISTS (
                SELECT schema_name 
                FROM information_schema.schemata 
                WHERE schema_name = {schema}
            )
            BEGIN
                -- Disable foreign key constraints
                PRINT 'Disabling foreign key constraints...';

                DECLARE @sql NVARCHAR(MAX);
                DECLARE @constraint NVARCHAR(MAX);

                DECLARE constraint_cursor CURSOR FOR
                SELECT 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' NOCHECK CONSTRAINT ' + QUOTENAME(name)
                FROM sys.foreign_keys
                WHERE OBJECT_SCHEMA_NAME(parent_object_id) = {schema};

                OPEN constraint_cursor;
                FETCH NEXT FROM constraint_cursor INTO @constraint;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    EXEC sp_executesql @constraint;
                    FETCH NEXT FROM constraint_cursor INTO @constraint;
                END;

                CLOSE constraint_cursor;
                DEALLOCATE constraint_cursor;

                PRINT 'Foreign key constraints disabled.';

                -- Drop all foreign key constraints
                PRINT 'Dropping foreign key constraints...';

                SET @sql = N'';
                SELECT @sql += 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';'
                FROM sys.foreign_keys
                WHERE OBJECT_SCHEMA_NAME(parent_object_id) = {schema};

                EXEC sp_executesql @sql;

                PRINT 'Foreign key constraints dropped.';

                -- Drop all tables
                PRINT 'Dropping tables...';

                DECLARE @table NVARCHAR(MAX);

                DECLARE table_cursor CURSOR FOR
                SELECT QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = {schema};

                OPEN table_cursor;
                FETCH NEXT FROM table_cursor INTO @table;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = 'DROP TABLE ' + @table;
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM table_cursor INTO @table;
                END;

                CLOSE table_cursor;
                DEALLOCATE table_cursor;

                PRINT 'Tables dropped.';

                -- Drop all views
                PRINT 'Dropping views...';

                DECLARE @view NVARCHAR(MAX);

                DECLARE view_cursor CURSOR FOR
                SELECT QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)
                FROM INFORMATION_SCHEMA.VIEWS
                WHERE TABLE_SCHEMA = {schema};

                OPEN view_cursor;
                FETCH NEXT FROM view_cursor INTO @view;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = 'DROP VIEW ' + @view;
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM view_cursor INTO @view;
                END;

                CLOSE view_cursor;
                DEALLOCATE view_cursor;

                PRINT 'Views dropped.';

                -- Drop all stored procedures
                PRINT 'Dropping stored procedures...';

                DECLARE @procedure NVARCHAR(MAX);

                DECLARE procedure_cursor CURSOR FOR
                SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + '.' + QUOTENAME(name)
                FROM sys.procedures
                WHERE SCHEMA_NAME(schema_id) = {schema};

                OPEN procedure_cursor;
                FETCH NEXT FROM procedure_cursor INTO @procedure;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = 'DROP PROCEDURE ' + @procedure;
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM procedure_cursor INTO @procedure;
                END;

                CLOSE procedure_cursor;
                DEALLOCATE procedure_cursor;

                PRINT 'Stored procedures dropped.';

                -- Drop all functions
                PRINT 'Dropping functions...';

                DECLARE @function NVARCHAR(MAX);

                DECLARE function_cursor CURSOR FOR
                SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + '.' + QUOTENAME(name)
                FROM sys.objects
                WHERE type IN ('FN', 'IF', 'TF') -- Scalar, inline table-valued, and table-valued functions
                    AND SCHEMA_NAME(schema_id) = {schema};

                OPEN function_cursor;
                FETCH NEXT FROM function_cursor INTO @function;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = 'DROP FUNCTION ' + @function;
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM function_cursor INTO @function;
                END;

                CLOSE function_cursor;
                DEALLOCATE function_cursor;

                PRINT 'Functions dropped.';

                -- Drop all user-defined types (UDTs)
                PRINT 'Dropping user-defined types...';

                DECLARE @udt NVARCHAR(MAX);

                DECLARE udt_cursor CURSOR FOR
                SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + '.' + QUOTENAME(name)
                FROM sys.types
                WHERE is_user_defined = 1
                    AND SCHEMA_NAME(schema_id) = {schema};

                OPEN udt_cursor;
                FETCH NEXT FROM udt_cursor INTO @udt;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = 'DROP TYPE ' + @udt;
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM udt_cursor INTO @udt;
                END;

                CLOSE udt_cursor;
                DEALLOCATE udt_cursor;

                PRINT 'User-defined types dropped.';

                -- Drop all sequences
                PRINT 'Dropping sequences...';

                DECLARE @sequence NVARCHAR(MAX);

                DECLARE sequence_cursor CURSOR FOR
                SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + '.' + QUOTENAME(name)
                FROM sys.sequences
                WHERE SCHEMA_NAME(schema_id) = {schema};

                OPEN sequence_cursor;
                FETCH NEXT FROM sequence_cursor INTO @sequence;

                WHILE @@FETCH_STATUS = 0
                BEGIN
                    SET @sql = 'DROP SEQUENCE ' + @sequence;
                    EXEC sp_executesql @sql;
                    FETCH NEXT FROM sequence_cursor INTO @sequence;
                END;

                CLOSE sequence_cursor;
                DEALLOCATE sequence_cursor;

                PRINT 'Sequences dropped.';
            END
            """;

        await database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);
    }
}