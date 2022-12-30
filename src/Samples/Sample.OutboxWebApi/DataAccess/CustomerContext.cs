namespace Sample.OutboxWebApi.DataAccess;

using Microsoft.EntityFrameworkCore;

public class CustomerContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }

    public CustomerContext(DbContextOptions<CustomerContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>();
    }
}
