namespace SlimMessageBus.Host.Outbox.DbContext.Test;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

public static class DatabaseFacadeExtenstions
{
    public static Task EraseTableIfExists(this DatabaseFacade db, string tableName)
    {
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
        return db.ExecuteSqlRawAsync($"""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = '{tableName}')
            BEGIN
                DELETE FROM dbo.{tableName};
            END
            """);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
    }
}
