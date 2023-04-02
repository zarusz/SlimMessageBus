namespace Sample.AsyncApi.Service.Messages;

public class CustomerEventConsumer : IConsumer<CustomerEvent>
{
    /// <summary>
    /// Process the <see cref="CustomerEvent"/> and acts accordingly.
    /// </summary>
    /// <remarks>
    /// This will create an customer entry in the local databse for the created customer.
    /// </remarks>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task OnHandle(CustomerEvent message)
    {
        throw new NotImplementedException();
    }
}
