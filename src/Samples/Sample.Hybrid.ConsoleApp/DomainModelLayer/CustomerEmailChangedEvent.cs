namespace Sample.Hybrid.ConsoleApp.DomainModel
{
    using System;

    /// <summary>
    /// Some domain event
    /// </summary>
    public class CustomerEmailChangedEvent
    {
        public DateTime Timestamp { get; }
        public Customer Customer { get; }
        public string OldEmail { get; }

        public CustomerEmailChangedEvent(Customer customer, string oldEmail)
        {
            Timestamp = DateTime.UtcNow;
            Customer = customer;
            OldEmail = oldEmail;
        }
    }
}
