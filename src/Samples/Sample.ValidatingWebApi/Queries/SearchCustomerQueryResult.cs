namespace Sample.ValidatingWebApi.Queries;

using Sample.ValidatingWebApi.Model;

public record SearchCustomerQueryResult
{
    public IEnumerable<CustomerModel> Items { get; set; } = [];
}