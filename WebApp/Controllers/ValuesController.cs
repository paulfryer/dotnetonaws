using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


namespace WebApp.Controllers
{

    [Route("proxy")]
    public class ValuesController : Controller
    {
        [HttpGet("{*path}")]
        [HttpPost("{*path}")]
        [HttpPut("{*path}")]
        [HttpOptions("{*path}")]
        [HttpHead("{*path}")]
        [HttpPatch("{*path}")]
        [HttpDelete("{*path}")]
        public string Execute(string path)
        {

            return path;
        }
    }
}
