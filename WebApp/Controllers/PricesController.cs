using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.EC2;
using Amazon.DynamoDBv2;
using System.Linq;
using System.Net;
using Functions;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using WebApp.Models;

namespace WebApp.Controllers
{
    /// <summary>
    /// Adding a comment.
    /// </summary>
    [Route("api/[controller]")]
    public partial class PricesController : Controller
    {
        IAmazonEC2 ec2;
        IAmazonDynamoDB dynamo;
        IAmazonCloudWatch cloudWatch;

        public PricesController(IAmazonEC2 ec2, IAmazonDynamoDB dynamo, IAmazonCloudWatch cloudWatch)
        {
            this.ec2 = ec2;
            this.dynamo = dynamo;
            this.cloudWatch = cloudWatch;
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
        public async Task<IActionResult> Get(string PR, string AR)
        {
            return await GetAny(PR, AR);
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
            // Set CORS response headers.

            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            Response.Headers.Add("Cache-Control", "60");
            if (Request.Method == "OPTIONS")
            {
                return new ContentResult { StatusCode = 200 };
            }

            var respObj = new RespObj
            {
                PA = "",
                IT = new List<dynamic>(),
                NM = new Dictionary<string, string>()
            };


            string metricName = "PR";

            // Build CloudWatch metric query.

            var listMetricsReq = new ListMetricsRequest
            {
                Namespace = "SpotAnalytics",
                Dimensions = new List<DimensionFilter>()
            };

            if (!string.IsNullOrEmpty(PR))
            {
                metricName += "|AR";
                listMetricsReq.Dimensions.Add(new DimensionFilter { Name = "PR", Value = PR });
                respObj.NM.Add("PR", Names[PR]);
                respObj.PA += PR;
            }
            if (!string.IsNullOrEmpty(AR))
            {
                metricName += "|RE";
                listMetricsReq.Dimensions.Add(new DimensionFilter { Name = "AR", Value = AR });
                respObj.NM.Add("AR", Names[AR]);
                respObj.PA += "|" + AR;
            }
            if (!string.IsNullOrEmpty(RE))
            {
                metricName += "|RI";
                listMetricsReq.Dimensions.Add(new DimensionFilter { Name = "RE", Value = RE });
                respObj.NM.Add("RE", Names[RE]);
                respObj.PA += "|" + RE;
            }
            if (!string.IsNullOrEmpty(RI))
            {
                metricName += "|FA";
                listMetricsReq.Dimensions.Add(new DimensionFilter { Name = "RI", Value = RI });
                respObj.NM.Add("RI", Names[$"{AR}-{RE}-{RI}"]);
                respObj.PA += "|" + RI;
            }

            // If we get down to the family level (FA) then query dynamo for current prices.
            if (!string.IsNullOrEmpty(FA))
            {

                respObj.NM.Add("FA", Names[FA]);
                respObj.PA += "|" + FA;

                var sortKey = WebUtility.UrlDecode($"{PR}|{AR}|{RE}|{RI}|{FA}");
                DynamoDBContext context = new DynamoDBContext(dynamo);

                string tableName = "SpotPrice";
                Table table = Table.LoadTable(dynamo, tableName);

                var search = table.Query("PR|AR|RE|RI|FA|GE|SI|AZ", new QueryFilter("SK", QueryOperator.BeginsWith, sortKey));

                var resp2 = await search.GetNextSetAsync();

                var observations = context.FromDocuments<FlatPriceObservation>(resp2).OrderBy(o => o.PE).Take(100);

                foreach (var ob in observations)
                    respObj.IT.Add(ob);

                // return JSON
                return new ContentResult
                {
                    Content = JsonConvert.SerializeObject(respObj),
                    ContentType = "application/json",
                    StatusCode = 200
                };
            }

            listMetricsReq.MetricName = metricName;
            // query cloudwatch for metrics
            var resp = await cloudWatch.ListMetricsAsync(listMetricsReq);

            var statsTasks = new List<Task<GetMetricStatisticsResponse>>();

            var dimensionValueMap = new Dictionary<int, string>();

            // foreach metric found get the statistics, and do this all in parallel using Tasks.
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

            // when all the statistics have been collected.
            await Task.WhenAll(statsTasks);

            // Convert to application response model
            foreach (var task in statsTasks)
            {
                var k = dimensionValueMap[task.Id];

                var stat = new StatObj
                {
                    CO = k,
                    NA = Names.SingleOrDefault(d => d.Key == k).Value,
                    ST = task.Result.Datapoints.OrderBy(d => d.Timestamp)
                    .Select(d => new Stat { AV = Convert.ToDecimal(String.Format("{0:0.00000}", d.Average)), TM = d.Timestamp })
                    .ToList()
                };

                if (metricName.EndsWith("RI"))
                {
                    var search = $"{AR}-{RE}-{k}";
                    stat.NA = Names.SingleOrDefault(d => d.Key == search).Value;
                }

                if (string.IsNullOrEmpty(stat.NA))
                    stat.NA = stat.CO;


                respObj.IT.Add(stat);

            }

            // return JSON
            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(respObj),
                ContentType = "application/json",
                StatusCode = 200
            };
        }

        public Dictionary<string, string> Names
        {
            get
            {
                return new Dictionary<string, string> {
                    {"Linux-UNIX", "Linux Unix" },
                    {"SUSE-Linux", "SUSE Linux" },
                    {"Windows", "Windows" },
                    {"ca", "Canada" },
                    {"eu", "Europe" },
                    {"ap", "Asia Pacific" },
                    {"sa", "South America" },
                    {"us", "United States" },
                    {"east", "East" },
                    {"west", "West" },
                    {"southeast", "South East" },
                    {"northeast", "North East" },
                    {"northwest", "North West" },
                    {"southwest", "South West" },
                    {"north", "North" },
                    {"south", "South" },
                    {"central", "Central" },

                    {"us-west-1", "N. California" },
                    {"us-west-2", "Oregon" },
                    {"us-east-1", "N. Virginia" },
                    {"us-east-2", "Ohio" },
                    {"ap-south-1", "Mumbai" },
                    {"ap-northeast-2", "Seoul" },
                    {"ap-southeast-1", "Singapore" },
                    {"ap-southeast-2", "Syndey" },
                    {"ap-northeast-1", "Tokyo" },
                    {"eu-central-1", "Frankfurt" },
                    {"eu-west-1", "Ireland" },
                    {"eu-west-2", "London" },
                    {"sa-east-1", "São Paulo" },
                    {"ca-central-1", "Canada"},

                    {"t", "General Purpose Burstable"},
                    {"m", "General Purpose" },
                    {"c", "Compute Optimized"},
                    {"cc", "Compute Optimized" },
                    {"cg", "Accelerated Computing" },
                    {"hi", "Storage Optimized" },
                    {"hs", "Storage Optimized" },
                    {"x", "Extreme Memory Optimized"},
                    {"r", "Memory Optimized" },
                    {"p", "General Purpose GPU" },
                    {"g", "Graphics Optimized" },
                    {"f", "FPGA Optimized" },
                    {"i", "Storage Optimized" },
                    {"d", "Dense Storage" },
                    {"cr", "Cluster Networking" }
                };
            }
        }
    }
}