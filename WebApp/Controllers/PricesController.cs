using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.EC2;

namespace WebApp.Controllers
{ 
    [Route("api/[controller]")]
    public class PricesController : Controller {


        IAmazonEC2 ec2Client;

        public PricesController(IAmazonEC2 ec2Client )
        {
            this.ec2Client = ec2Client;
        }

       [HttpGet]
       [Route("")]
        public async Task<IActionResult> Index()
        {
            var resp = await ec2Client.DescribeSpotPriceHistoryAsync();
            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(resp.SpotPriceHistory),
                ContentType = "application/json",
                StatusCode = 200
            };
           
        }
        

    }
}
