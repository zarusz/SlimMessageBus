# AsyncAPI Specification Plugin for SlimMessageBus <!-- omit in toc -->

Please read the [Introduction](intro.md) before reading this provider documentation.

- [Introduction](#introduction)
- [Configuration](#configuration)
- [Sample AsyncAPI document](#sample-asyncapi-document)
- [Documentation](#documentation)
  
## Introduction

The [`SlimMessageBus.Host.AsyncApi`](https://www.nuget.org/packages/SlimMessageBus.Host.AsyncApi) introduces a document generator for [Saunter](https://github.com/tehmantra/saunter), which enables to generate an [AsyncAPI specification](https://www.asyncapi.com/) from SlimMessageBus.

## Configuration

On the SMB setup, use the `mbb.AddAsyncApiDocumentGenerator()` to add the `IDocumentGenerator` for Saunter library:

```cs
services.AddSlimMessageBus(mbb =>
{    
    // Register the IDocumentGenerator for Saunter library
    mbb.AddAsyncApiDocumentGenerator();
});
```

Then register the Saunter services (in that order):

```cs
services.AddAsyncApiSchemaGeneration(options =>
{
    options.AsyncApi = new AsyncApiDocument
    {
        Info = new Info("SlimMessageBus AsyncAPI Sample API", "1.0.0")
        {
            Description = "This is a sample of the SlimMessageBus AsyncAPI plugin",
            License = new License("Apache 2.0")
            {
                Url = "https://www.apache.org/licenses/LICENSE-2.0"
            }
        }
    };
});
```

Saunter also requires to add the following endpoints (consult the Saunter docs):

```cs
// Register AsyncAPI docs via Sauter 
app.MapAsyncApiDocuments();
app.MapAsyncApiUi();
```

See the [Sample.AsyncApi.Service](../src/Samples/Sample.AsyncApi.Service/) for a complete setup.

## Sample AsyncAPI document

When running the mentioned sample, the AsyncAPI document can be obtained via the following link:
https://localhost:7252/asyncapi/asyncapi.json

The generated document for the sample is available [here](../src/Samples/Sample.AsyncApi.Service/asyncapi.json) as well.

## Documentation

The comment and remarks are being taken from the code (for the consumer method and message type):

```cs
/// <summary>
/// Event when a customer is created within the domain.
/// </summary>
/// <param name="Id"></param>
/// <param name="Firstname"></param>
/// <param name="Lastname"></param>
public record CustomerCreatedEvent(Guid Id, string Firstname, string Lastname) : CustomerEvent(Id);

public class CustomerCreatedEventConsumer : IConsumer<CustomerCreatedEvent>
{
    /// <summary>
    /// Upon the <see cref="CustomerCreatedEvent"/> will store it with the database.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task OnHandle(CustomerCreatedEvent message) { }    
}
```

Ensure that your project has the `GenerateDocumentationFile` enabled (more [here](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle?view=aspnetcore-7.0&tabs=visual-studio#xml-comments)):

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```
