using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.EC2;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Linq;
using System.Net;
using Functions;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace WebApp.Controllers
{
    [Route("api/[controller]")]
    public class PricesController : Controller
    {
        IAmazonEC2 ec2;
        IAmazonDynamoDB dynamo;

        public PricesController(IAmazonEC2 ec2, IAmazonDynamoDB dynamo)
        {
            this.ec2 = ec2;
            this.dynamo = dynamo;
        }
        
        [HttpGet]
        [HttpOptions]
        [Route("{sortKey}")]
        public async Task<IActionResult> Index(string sortKey)
        {
            try
            {
                sortKey = WebUtility.UrlDecode(sortKey);
                DynamoDBContext context = new DynamoDBContext(dynamo);
                
				string tableName = "SpotPrice";
				Table table = Table.LoadTable(dynamo, tableName);
                
                var search = table.Query("PR|AR|RE|RI|FA|GE|SI|AZ", new QueryFilter("SK", QueryOperator.BeginsWith, sortKey));
                               
                var resp2 = await search.GetNextSetAsync();

                var observations = context.FromDocuments<FlatPriceObservation>(resp2).OrderBy(o => o.PE).Take(100);
                return new ContentResult
                {
                    Content = JsonConvert.SerializeObject(observations),
                    ContentType = "application/json",
                    StatusCode = 200
                };
            }
            catch (Exception e)
            {
                return new ContentResult
                {
                    Content = "<html><body><h3>" + e.Message + "</h3></body></html>",
                    ContentType = "text/html",
                    StatusCode = 400
                };
            }
        }


        [HttpGet]
        [Route("")]
        public async Task<IActionResult> Index()
        {
            var resp = await ec2.DescribeSpotPriceHistoryAsync();
            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(resp.SpotPriceHistory),
                ContentType = "application/json",
                StatusCode = 200
            };

        }


    }
}
