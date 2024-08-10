namespace Sample.DomainEvents.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ValuesController : Controller
{
    // GET api/values
    [HttpGet]
    public IActionResult Get()
    {
        // Note: The bus will look will get a new MessageBusProxy instance but its dependencies will be tied to he current HTTP request scope
        var bus1 = MessageBus.Current;
        var bus2 = MessageBus.Current;
        return Json(ReferenceEquals(bus1, bus2));
    }
}
