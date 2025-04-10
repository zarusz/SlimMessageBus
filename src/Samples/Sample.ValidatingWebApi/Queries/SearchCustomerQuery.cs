namespace Sample.ValidatingWebApi.Queries;

public record SearchCustomerQuery : IRequest<SearchCustomerQueryResult>
{
    public Guid? CustomerId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
