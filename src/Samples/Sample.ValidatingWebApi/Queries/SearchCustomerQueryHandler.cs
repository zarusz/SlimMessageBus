namespace Sample.ValidatingWebApi.Queries;

using Sample.ValidatingWebApi.Model;

public class SearchCustomerQueryHandler : IRequestHandler<SearchCustomerQuery, SearchCustomerQueryResult>
{
    public Task<SearchCustomerQueryResult> OnHandle(SearchCustomerQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new SearchCustomerQueryResult
        {
            Items =
            [
                new CustomerModel(Guid.NewGuid(), "John", "Wick", "john@whick.com", null)
            ]
        });
}

