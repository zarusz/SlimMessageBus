using Sample.ValidatingWebApi.Commands;

namespace Sample.ValidatingWebApi.Queries;

public record SearchCustomerResult
{
    public IEnumerable<CustomerModel> Items { get; set; } = Enumerable.Empty<CustomerModel>();
}