namespace SlimMessageBus.Host.Outbox.Sql.DbContext.Test.DataAccess;

using Microsoft.EntityFrameworkCore;

public class CustomerContext : DbContext
{
    public const string Schema = "Outbox";

    public DbSet<Customer> Customers { get; set; }

    #region EF migrations

    /// <summary>
    /// Used by EF Migrations
    /// </summary>
    public CustomerContext()
    {
    }

    /// <summary>
    /// Used by EF Migrations
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Used for schema migration tooling
            optionsBuilder.UseSqlServer("-");
        }
    }

    #endregion

    public CustomerContext(DbContextOptions<CustomerContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Customer>();
    }
}
