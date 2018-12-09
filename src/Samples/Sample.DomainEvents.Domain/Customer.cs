using System;

namespace Sample.DomainEvents.Domain
{
    /// <summary>
    /// aggregate root 
    /// </summary>
    public class Customer
    {
        public string CustomerId { get; private set; }
        public string Firstname { get; private set; }
        public string Lastname { get; private set; }

        public Customer(string firstname, string lastname)
        {
            CustomerId = Guid.NewGuid().ToString();
            Firstname = firstname;
            Lastname = lastname;
        }
    }
}