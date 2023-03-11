using System.Net.Mime;
using System.Reflection;

using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;

using Sample.ValidatingWebApi.Commands;
using Sample.ValidatingWebApi.Queries;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.FluentValidation;
using SlimMessageBus.Host.Memory;

var builder = WebApplication.CreateBuilder(args);

// Configure SMB
builder.Services.AddSlimMessageBus(mbb => mbb
    .WithProviderMemory()
    .AutoDeclareFrom(Assembly.GetExecutingAssembly())
    .AddServicesFromAssembly(Assembly.GetExecutingAssembly())
    .AddAspNet()
    .AddFluentValidation(cfg =>
    {
        // Configure SlimMessageBus.Host.FluentValidation plugin
        cfg.AddProducerValidatorsFromAssemblyContaining<CreateCustomerCommandValidator>();
        // You can map the validation errors into a custom exception
        //cfg.AddValidationErrorsHandler(errors => new ApplicationException("Custom Validation Exception"));
    }));

// FluentValidation library - find and register IValidator<T> implementations:
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerCommandValidator>();

// Required for SlimMessageBus.Host.AspNetCore package
builder.Services.AddHttpContextAccessor();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Translates the ValidationException into a 400 bad request
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        if (exceptionHandlerPathFeature?.Error is ValidationException e)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = MediaTypeNames.Application.Json;
            await context.Response.WriteAsJsonAsync(new { e.Errors });
        }
    });
});

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
