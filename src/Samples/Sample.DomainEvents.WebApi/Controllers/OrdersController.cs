namespace Sample.DomainEvents.WebApi.Controllers;

using Microsoft.AspNetCore.Mvc;

using Sample.DomainEvents.Domain;

[Route("api/[controller]")]
[ApiController]
public class OrdersController : Controller
{
    /// <summary>
    /// Create sample order
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    public async Task<IActionResult> Post()
    {
        // Note for example simplicity the order data is hardcoded and we do not follow any CQRS patterns
        // For more sophisticated scenarios you would use a command pattern and a command handler (see CQRS sample)

        var john = new Customer("John", "Whick");

        var order = new Order(john);
        order.Add("id_machine_gun", 2);
        order.Add("id_grenade", 4);

        // this causes the domain event handlers to get triggered
        await order.Submit();

        return Ok();
    }
}
