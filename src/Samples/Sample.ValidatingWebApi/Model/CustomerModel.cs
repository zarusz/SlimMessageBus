namespace Sample.ValidatingWebApi.Model;

public record CustomerModel(Guid Id, string FirstName, string LastName, string? Email, string? Phone);