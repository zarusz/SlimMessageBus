namespace SlimMessageBus.Host.Outbox.DbContext.Test.DataAccess;

public class Customer
{
    public Guid Id { get; protected set; }
    public string Firstname { get; protected set; }
    public string Lastname { get; protected set; }
    public string UniqueId { get; protected set; }

    public Customer(string firstname, string lastname, string uniqueId)
    {
        Id = Guid.NewGuid();
        Firstname = firstname;
        Lastname = lastname;
        UniqueId = uniqueId;
    }

    protected Customer()
    {
        Firstname = string.Empty;
        Lastname = string.Empty;
    }
}
