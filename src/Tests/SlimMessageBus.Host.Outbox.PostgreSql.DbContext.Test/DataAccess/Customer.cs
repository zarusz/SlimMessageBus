namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext.Test.DataAccess;

public class Customer
{
    public Guid Id { get; protected set; }
    public string FirstName { get; protected set; }
    public string LastName { get; protected set; }
    public string UniqueId { get; protected set; }

    public Customer(string firstName, string lastName, string uniqueId)
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        LastName = lastName;
        UniqueId = uniqueId;
    }

    protected Customer()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
    }
}
