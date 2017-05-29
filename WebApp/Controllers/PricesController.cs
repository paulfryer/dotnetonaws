using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebApp.Controllers
{ 
    [Route("[controller]")]
    public class PricesController : Controller {

        public PricesController(  )
        {           
        }

       [HttpGet]
       [Route("")]
        public async Task<IActionResult> Index()
        {

            try
            {
                var model = new
                {
                    Prices = new List<dynamic> {
                        new {
                                Price = 0.332m
                        }
                    }

                };

                return View(model);

            }catch(Exception ex)
            {
                var result = new BadRequestResult();
                return result;
            }            
        }
        

    }
}
