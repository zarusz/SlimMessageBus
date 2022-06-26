namespace Sample.DomainEvents.WebApi.Controllers;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sample.DomainEvents.Domain;

[Route("api/[controller]")]
[ApiController]
public class OrdersController : Controller
{
    [HttpPost]
    public async Task<IActionResult> Post()
    {
        // Note for example simplicity the order data is hardcoded

        var john = new Customer("John", "Whick");

        var order = new Order(john);
        order.Add("id_machine_gun", 2);
        order.Add("id_grenade", 4);

        await order.Submit(); // this causes the domain event handlers to get triggered

        return Ok();
    }
}
