namespace Sample.ValidatingWebApi.Queries;

using SlimMessageBus;

public record SearchCustomerQuery : IRequestMessage<SearchCustomerResult>
{
    public Guid? CustomerId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
