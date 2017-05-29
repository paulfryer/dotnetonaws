using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebApp.Controllers
{ 
    [Route("api/[controller]")]
    public class PricesController : Controller {

        public PricesController(  )
        {           
        }

       [HttpGet]
       [Route("")]
        public async Task<IActionResult> GetAsync()
        {

            try
            {
                throw new NotImplementedException();

            }catch(Exception ex)
            {
                var result = new BadRequestResult();
                return result;
            }            
        }
        

    }
}
