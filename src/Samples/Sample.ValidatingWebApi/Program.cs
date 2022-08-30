using Sample.ValidatingWebApi.Commands;
using SlimMessageBus;
using SlimMessageBus.Host.AspNetCore;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.FluentValidation;
using System.Reflection;
using FluentValidation;
using Sample.ValidatingWebApi.Queries;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure SMB
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb
        .WithProviderMemory()
        .AutoDeclareFrom(Assembly.GetExecutingAssembly());
}, addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() });

// register validators
//builder.Services.AddValidationErrorsHandler(errors => new ApplicationException("Custom Validation Exception"));
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerCommandValidator>();
builder.Services.AddProducerValidatorsFromAssemblyContaining<CreateCustomerCommandValidator>();
//builder.Services.AddTransient<IProducerInterceptor<CreateCustomerCommand>, ProducerValidationInterceptor<CreateCustomerCommand>>();
//builder.Services.AddTransient(typeof(IProducerInterceptor<>), typeof(ProducerValidationInterceptor<>));

builder.Services.AddHttpContextAccessor();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app
    .MapPost("/customer", (CreateCustomerCommand command, IMessageBus bus) => bus.Send(command))
    .WithName("CreateCustomer");

app
    .MapPost("/customer/search", (SearchCustomerQuery query, IMessageBus bus) => bus.Send(query))
    .WithName("SearchCustomer");

await app.RunAsync();
