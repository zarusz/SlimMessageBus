using System;

namespace Sample.Hybrid.ConsoleApp.DomainModel
{
    /// <summary>
    /// Some domain event
    /// </summary>
    public class CustomerEmailChangedEvent
    {
        public DateTime Timestamp { get; set; }
        public Customer Customer { get; set; }
        public string OldEmail { get; set; }

        public CustomerEmailChangedEvent()
        {
        }

        public CustomerEmailChangedEvent(Customer customer, string oldEmail)
        {
            Timestamp = DateTime.UtcNow;
            Customer = customer;
            OldEmail = oldEmail;
        }
    }
}
