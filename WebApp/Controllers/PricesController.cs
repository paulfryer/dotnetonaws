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
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace WebApp.Controllers
{
    [Route("api/[controller]")]
    public class PricesController : Controller
    {
        IAmazonEC2 ec2;
        IAmazonDynamoDB dynamo;

        AmazonCloudWatchClient cloudWatch = new AmazonCloudWatchClient();

        public PricesController(IAmazonEC2 ec2, IAmazonDynamoDB dynamo)
        {
            this.ec2 = ec2;
            this.dynamo = dynamo;
        }

        [HttpOptions]
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> Get()
        {
            return await GetAny();
        }

        [HttpOptions]
        [HttpGet]
        [Route("{PR}")]
        public async Task<IActionResult> Get(string PR)
        {
            return await GetAny(PR);
        }

        [HttpOptions]
        [HttpGet]
        [Route("{PR}/{AR}")]
        public async Task<IActionResult> Get(string PR,string AR)
        {
            return await GetAny(PR,AR);
        }

        [HttpOptions]
        [HttpGet]
        [Route("{PR}/{AR}/{RE}")]
        public async Task<IActionResult> Get(string PR, string AR, string RE)
        {
            return await GetAny(PR, AR, RE);
        }

        [HttpOptions]
        [HttpGet]
        [Route("{PR}/{AR}/{RE}/{RI}")]
        public async Task<IActionResult> Get(string PR, string AR, string RE, string RI)
        {
            return await GetAny(PR, AR, RE, RI);
        }

        [HttpOptions]
        [HttpGet]
        [Route("{PR}/{AR}/{RE}/{RI}/{FA}")]
        public async Task<IActionResult> GetAny(string PR = null, string AR = null, string RE = null, string RI = null, string FA = null)
        {
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            if (Request.Method == "OPTIONS")
            {                
                return new ContentResult { StatusCode = 200 };
            }

            string metricName = "PR";
            
            var listMetricsReq = new ListMetricsRequest
            {
                Namespace = "SpotAnalytics",
                Dimensions = new List<DimensionFilter>()
            };

            if (!string.IsNullOrEmpty(PR)) {
                metricName += "|AR";
                listMetricsReq.Dimensions.Add(new DimensionFilter { Name = "PR", Value = PR });              
            }
            if (!string.IsNullOrEmpty(AR)) {
                metricName += "|RE";
                listMetricsReq.Dimensions.Add(new DimensionFilter { Name = "AR", Value = AR });
            }
            if (!string.IsNullOrEmpty(RE)) {
                metricName += "|RI";
                listMetricsReq.Dimensions.Add(new DimensionFilter { Name = "RE", Value = RE });
            }
            if (!string.IsNullOrEmpty(RI))
            {
                metricName += "|FA";
                listMetricsReq.Dimensions.Add(new DimensionFilter { Name = "RI", Value = RI });
            }

            listMetricsReq.MetricName = metricName;

            var resp = await cloudWatch.ListMetricsAsync(listMetricsReq);

            var statsTasks = new List<Task<GetMetricStatisticsResponse>>();

            var dimensionValueMap = new Dictionary<int, string>();

            foreach (var metric in resp.Metrics)
            {
                var getStatsReq = new GetMetricStatisticsRequest
                {
                    Dimensions = new List<Dimension>(),
                    EndTime = DateTime.UtcNow,
                    StartTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)),
                    MetricName = metricName,
                    Namespace = "SpotAnalytics",
                    Period = Convert.ToInt32(TimeSpan.FromMinutes(5).TotalSeconds),
                    Statistics = new List<string> { "Average" }
                };

                foreach (var dimension in metric.Dimensions)
                    getStatsReq.Dimensions.Add(dimension);
                
                var statsTask = cloudWatch.GetMetricStatisticsAsync(getStatsReq);
                var mapValue = metric.Dimensions.Single(d => d.Name == metricName.Substring(metricName.Length - 2, 2)).Value;
                dimensionValueMap.Add(statsTask.Id, mapValue);
                statsTasks.Add(statsTask);
            }


            await Task.WhenAll(statsTasks);

            var respObj = new Dictionary<string, List<Stat>>();

            foreach (var task in statsTasks) {
                var k = dimensionValueMap[task.Id];
                var v = task.Result.Datapoints.OrderBy(d => d.Timestamp)
                    .Select(d => new Stat{ AV = Convert.ToDecimal(d.Average), TM = d.Timestamp })
                    .ToList();
                respObj.Add(k, v);
       
            }

            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(respObj),
                ContentType = "application/json",
                StatusCode = 200
            };
        }

        public class Stat {
            public decimal AV { get; set;}
            public DateTime TM { get; set; }
        }

        /*
        [HttpOptions]
        [HttpGet]
        [Route("{sortKey}")]
        public async Task<IActionResult> Index(string sortKey)
        {
            if (Request.Method == "OPTIONS") {
                Response.Headers.Add("Access-Control-Allow-Origin", "*");
                return new ContentResult { StatusCode = 200 }; 
            }

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
        [Route("TODOFILLTHISOUThistory")]
        public async Task<IActionResult> Index()
        {
            var resp = await ec2.DescribeSpotPriceHistoryAsync();
            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(resp.SpotPriceHistory),
                ContentType = "application/json",
                StatusCode = 200
            };

        }*/
    }
}
