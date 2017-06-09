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
using Amazon.Athena;
using Amazon.Athena.Model;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;

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
		[Route("athena")]
		public async Task<IActionResult> ExecutueAthenaQueryAsync()
		{

			try
			{
                var athena = new AmazonAthenaClient(RegionEndpoint.USWest2);
                var sqs = new AmazonSQSClient(RegionEndpoint.USWest2);
                /*
                var r1 = await athena.CreateNamedQueryAsync(new Amazon.Athena.Model.CreateNamedQueryRequest{
                    Database = "spotanalytics",
                    Name = "some name here.",
                    QueryString = "SELECT * FROM price limit 1000;"
                });
                */

                var resp = await athena.StartQueryExecutionAsync(new StartQueryExecutionRequest
                {
                    QueryExecutionContext = new QueryExecutionContext{
                        Database = "sampledb"  
                    },
                    ResultConfiguration = new ResultConfiguration{
                        OutputLocation = "s3://spot-price-data/"
                    },
                    QueryString = "SELECT * FROM elb_logs limit 10000;"
                });

                await Task.Delay(4000);

                var results = await athena.GetQueryResultsAsync(new GetQueryResultsRequest { 
                    QueryExecutionId = resp.QueryExecutionId,
                    MaxResults = 1000
                });


                var sqsTasks = new List<Task>();
                // it looks like the first row is always the headers, so we'lls skip this.
                // however a better method would be to NOT skip this and check if the row looks/feels like a 
                // header and skip it that way. This way we don't throw away a row that might actually be a 
                // non header.
                foreach (var r in results.ResultSet.Rows.Skip(1))
                {

                    var record = new List<string>();

                    foreach (var d in r.Data){
                        record.Add(d.VarCharValue);
                    }





                    var t = sqs.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = "https://sqs.us-west-2.amazonaws.com/989469592528/poc-LargeFile-Queue",
                        MessageBody = JsonConvert.SerializeObject(record.ToArray())
                    });
                    sqsTasks.Add(t);
                }

                await Task.WhenAll(sqsTasks);

				return new ContentResult
				{
					ContentType = "application/json",
                    Content = JsonConvert.SerializeObject(results),
					StatusCode = 200
				};

			}
			catch (Exception ex)
			{

				return new ContentResult
				{
					ContentType = "text/html",
					Content = String.Format("<html><body><h2>{0}<h2></body></html>", ex.Message),
					StatusCode = 400
				};
			}

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


                var observations = context.FromDocuments<FlatPriceObservation>(resp2).OrderBy(o => o.PE).Take(100);

                /*
                var resp = await dynamo.QueryAsync(new QueryRequest
                {
                    TableName = "SpotPrice",
                    KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",

                    ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    { ":pk", new AttributeValue("PR|AR|RE|RI|FA|GE|SI|AZ") },
                    { ":sk", new AttributeValue(sortKey) }
                }
                });             
         */
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
