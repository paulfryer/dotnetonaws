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
        [Route("{sortKey}")]
        public async Task<IActionResult> Index(string sortKey)
        {
            try
            {
                sortKey = WebUtility.UrlDecode(sortKey);

                DynamoDBContext context = new DynamoDBContext(dynamo);


				string tableName = "SpotPrice";
				Table table = Table.LoadTable(dynamo, tableName);

                //var filter = new RangeFilter(QueryOperator.BeginsWith, sortKey);
                var search = table.Query("PR|AR|RE|RI|FA|GE|SI|AZ", new QueryFilter("SK", QueryOperator.BeginsWith, sortKey));

                var resp2 = await search.GetNextSetAsync();

                // http://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DynamoDBContext.QueryScan.html
                //var resp1 = context.QueryAsync<FlatPriceObservation>("PK", QueryOperator.Equal, new List<string> { "PR|AR|RE|RI|FA|GE|SI|AZ" });

                /*
                var resp1 = context.FromQueryAsync<FlatPriceObservation>(new QueryOperationConfig {
                    
                    KeyExpression = new Expression {
                            ExpressionStatement = "PK = :pk AND begins_with(SK, :sk)",
                            ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>{
                                { ":pk",  }
                    },
                    

                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":pk", new AttributeValue("PR|AR|RE|RI|FA|GE|SI|AZ") },
                    { ":sk", new AttributeValue(sortKey) }
                });

                   context.FromQueryAsync<FlatPriceObservation>()
                         var items = new List<FlatPriceObservation>();

                
                foreach (var i in resp.Items) {
                    foreach (var k in i.Keys) {
                      
                    }        
                }
                */




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
