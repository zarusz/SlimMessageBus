namespace Sample.ValidatingWebApi.QueryHandlers;

using Sample.ValidatingWebApi.Commands;
using Sample.ValidatingWebApi.Queries;

using SlimMessageBus;

public class SearchCustomerQueryHandler : IRequestHandler<SearchCustomerQuery, SearchCustomerResult>
{
    public Task<SearchCustomerResult> OnHandle(SearchCustomerQuery request, CancellationToken cancellationToken) => Task.FromResult(new SearchCustomerResult
    {
        Items =
            [
                new CustomerModel(Guid.NewGuid(), "John", "Whick", "john@whick.com", null)
            ]
    });
}

