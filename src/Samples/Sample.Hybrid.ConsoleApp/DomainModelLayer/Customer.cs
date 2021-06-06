namespace Sample.Hybrid.ConsoleApp.DomainModel
{
    using SlimMessageBus;
    using System;

    public class Customer
    {
        public string Id { get; protected set; }
        public DateTime Created { get; protected set; }
        public DateTime Updated { get; protected set; }

        public string Firstname { get; protected set; }
        public string Lastname { get; protected set; }

        public string Email { get; protected set; }

        protected Customer()
        {
        }

        public Customer(string firstname, string lastname)
        {
            Id = Guid.NewGuid().ToString();
            Created = Updated = DateTime.UtcNow;
            Firstname = firstname;
            Lastname = lastname;
        }

        public void ChangeEmail(string email)
        {
            var oldEmail = Email;

            Email = email;
            Updated = DateTime.UtcNow;

            MessageBus.Current.Publish(new CustomerEmailChangedEvent(this, oldEmail));
        }
    }
}
