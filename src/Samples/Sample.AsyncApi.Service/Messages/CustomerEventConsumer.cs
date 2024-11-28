namespace Sample.AsyncApi.Service.Messages;

public class CustomerEventConsumer : IConsumer<CustomerEvent>
{
    /// <summary>
    /// Process the <see cref="CustomerEvent"/> and acts accordingly.
    /// </summary>
    /// <remarks>
    /// This will create an customer entry in the local database for the created customer.
    /// </remarks>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task OnHandle(CustomerEvent message, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
