namespace SlimMessageBus.Host.Sql.Common;

public interface ISqlConnectionProvider
{
    SqlConnection Connection { get; }
}
