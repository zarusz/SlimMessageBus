namespace Sample.ValidatingWebApi.QueryHandlers;

using Sample.ValidatingWebApi.Commands;
using Sample.ValidatingWebApi.Queries;
using SlimMessageBus;
using System.Threading.Tasks;

public class SearchCustomerQueryHandler : IRequestHandler<SearchCustomerQuery, SearchCustomerResult>
{
    public async Task<SearchCustomerResult> OnHandle(SearchCustomerQuery request, string path)
    {
        return new SearchCustomerResult
        {
            Items = new[]
            {
                new CustomerModel(Guid.NewGuid(), "John", "Whick", "john@whick.com", null)
            }
        };
    }
}

