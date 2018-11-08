using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SlimMessageBus;
using static Sample.DomainEvents.WebApi.Startup;

namespace Sample.DomainEvents.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        // GET api/values
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            var bus1 = MessageBus.Current as DummyMessageBus;
            var bus2 = MessageBus.Current as DummyMessageBus;
            return new string[] { bus1.Id, bus2.Id };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
