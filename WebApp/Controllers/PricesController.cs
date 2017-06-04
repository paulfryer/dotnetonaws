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
        [Route("{sortKey}")]
        public async Task<IActionResult> Index(string sortKey)
        {
            try
            {
                var resp = await dynamo.QueryAsync(new QueryRequest
                {
                    TableName = "SpotPrice",
                    KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",

                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":pk", new AttributeValue("PR|AR|RE|RI|FA|GE|SI|AZ") },
                    { ":sk", new AttributeValue(sortKey) }
                }
                });

                return new ContentResult
                {
                    Content = JsonConvert.SerializeObject(resp.Items),
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
