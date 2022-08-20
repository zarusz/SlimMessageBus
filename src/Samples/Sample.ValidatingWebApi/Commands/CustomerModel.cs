namespace Sample.ValidatingWebApi.Commands;

public record CustomerModel(Guid Id, string FirstName, string LastName, string? Email, string? Phone);