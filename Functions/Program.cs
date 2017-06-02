using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.EC2;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.Serialization.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Functions
{
    class Program
    {
        static void Main(string[] args)
        {
            TestSyncPricesAsync().Wait();

            Console.ReadKey();
        }

        private static async Task TestSyncPricesAsync()
        {
            var controller = new SpotController();


            var dynamoEventJson = File.OpenText("DynamoEvent.json").ReadToEnd();
            var @event = JsonConvert.DeserializeObject<DynamoDBEvent>(dynamoEventJson);

            // await controller.SyncToCloudWatch(@event, null);

            while (true) {
                await controller.SyncToDynamo(Amazon.RegionEndpoint.APNortheast1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.APNortheast2, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.APSouth1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.APSoutheast1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.APSoutheast2, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.CACentral1, null);
                //await controller.SyncToDynamo(Amazon.RegionEndpoint.CNNorth1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.EUCentral1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.EUWest1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.EUWest2, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.SAEast1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.USEast1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.USEast2, null);
                //await controller.SyncToDynamo(Amazon.RegionEndpoint.USGovCloudWest1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.USWest1, null);
                await controller.SyncToDynamo(Amazon.RegionEndpoint.USWest2, null);
            }




            Console.WriteLine("DONE!");

        }
    }

   

    public class SpotController {

        private IAmazonDynamoDB dynamo = new AmazonDynamoDBClient();
        private AmazonEC2Client ec2 = new AmazonEC2Client();
        private AmazonCloudWatchClient cloudWatch = new AmazonCloudWatchClient();


        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task SyncToCloudWatch(DynamoDBEvent @event, ILambdaContext context) {

           

           // Console.Write(JsonConvert.SerializeObject(@event));
           
            var records = @event.Records.ToList();
            var tasks = new List<Task>();

            while (records.Count > 0) {

                
                var maxRows = records.Count >= 20 ? 20 : records.Count;
                var rowsToSync = records.GetRange(0, maxRows);
                records.RemoveRange(0, maxRows);

                Console.WriteLine("About to write records to cloudwatch: " + rowsToSync);
                var metricData = new List<MetricDatum>();

                foreach (var record in rowsToSync)
                {
                    //Console.WriteLine(record.EventName);
                    metricData.Add(new MetricDatum
                    {
                        MetricName = "PricePerECU",
                        Dimensions = new List<Dimension>{
                                 new Dimension{
                                     Name = "Area",
                                     Value = record.Dynamodb.NewImage["AvailabilityZone"].S.Split('-')[0]
                                 },
                                 new Dimension{
                                     Name = "Region",
                                     Value = record.Dynamodb.NewImage["AvailabilityZone"].S.Split('-')[1]
                                 },
                                 new Dimension{
                                     Name = "AZ",
                                     Value = record.Dynamodb.NewImage["AvailabilityZone"].S.Split('-')[2]
                                 },
                                 new Dimension{
                                     Name = "InstanceFamily",
                                     Value = record.Dynamodb.NewImage["InstanceType"].S.Split('.')[0]
                                 },
                                 new Dimension{
                                     Name = "InstanceType",
                                     Value = record.Dynamodb.NewImage["InstanceType"].S.Split('.')[1]
                                 },
                                 new Dimension{
                                     Name = "OS",
                                     Value = record.Dynamodb.NewImage["ProductDescription"].S
                                 }
                            },
                        Timestamp = DateTime.Parse(record.Dynamodb.NewImage["Timestamp"].S),
                        Value = Convert.ToDouble(record.Dynamodb.NewImage["PricePerECU"].N)
                    });
                }

                tasks.Add(cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
                {
                    Namespace = "SpotPriceData2",
                    MetricData = metricData
                }));
            }

            await Task.WhenAll(tasks);
        }


        public async Task SyncToDynamo(Amazon.RegionEndpoint region, ILambdaContext context) {

            Console.WriteLine("Starting to get prices.");
            Console.WriteLine("PROCESSING REGION: " + region);

            var instanceTypes = GetInstanceTypeDescriptions();

            ec2 = new AmazonEC2Client(region);

            var resp = await ec2.DescribeSpotPriceHistoryAsync(new Amazon.EC2.Model.DescribeSpotPriceHistoryRequest {
                   
            });
            
            Console.Write(resp.NextToken);
                   
            while (resp.SpotPriceHistory.Count > 0) {
        
                Console.WriteLine("Rows Left: " + resp.SpotPriceHistory.Count);
                var maxRows = resp.SpotPriceHistory.Count >= 25 ? 25 : resp.SpotPriceHistory.Count; 
                var rowsToSync = resp.SpotPriceHistory.GetRange(0, maxRows);
                resp.SpotPriceHistory.RemoveRange(0, maxRows);

                var writeRequests = rowsToSync.Select(r => new WriteRequest {
                   PutRequest = new PutRequest {
                       Item = new Dictionary<string, AttributeValue> {
                           { "PartitionKey", new AttributeValue(string.Format("{0}|{1}|{2}", r.AvailabilityZone, r.InstanceType, r.ProductDescription)) },
                           {"SortKey", new AttributeValue(Convert.ToString(r.Timestamp.ToUniversalTime())) },
                           {"AvailabilityZone", new AttributeValue(r.AvailabilityZone) },
                           {"Price", new AttributeValue(Convert.ToString(r.Price)) },
                           {"ProductDescription", new AttributeValue(r.ProductDescription) },
                           {"InstanceType", new AttributeValue(r.InstanceType) },
                           {"Timestamp", new AttributeValue(Convert.ToString(r.Timestamp.ToUniversalTime())) },
                           { "PricePerCPU", new AttributeValue{
                            N = Convert.ToString(Convert.ToDecimal(r.Price) / instanceTypes.Single(it=>it.Code==r.InstanceType).CPU)
                           }
                           },
                           {
                               "PricePerECU", new AttributeValue{
                                   N = Convert.ToString(Convert.ToDecimal(r.Price) / instanceTypes.Single(it=>it.Code==r.InstanceType).ECU)
                               }
                           }

                       }
                   }
                }).ToList();

                var writeTask = dynamo.BatchWriteItemAsync(new BatchWriteItemRequest
                {
                    RequestItems = new Dictionary<string, List<WriteRequest>>
                    {
                        { "SpotPriceHistory", writeRequests}
                    }
                });

                // throttle the write rate
                await Task.WhenAll(writeTask, Task.Delay(100));
                
            }
        }


        private List<InstanceTypeDescription> GetInstanceTypeDescriptions()
        {
            var instanceTypesFile = System.IO.File.OpenText("InstanceTypes.csv");

            var csv = instanceTypesFile.ReadToEnd();
            csv = csv.Replace("\"", "");
            var rows = csv.Split('\n').ToList();
            // remove header
            rows.RemoveRange(0, 1);

            var instanceTypes = new List<InstanceTypeDescription>();

            foreach (var row in rows)
            {
                Console.WriteLine(row);
                var cols = row.Split(',');
                try
                {
                    instanceTypes.Add(new InstanceTypeDescription
                    {
                        Code = cols[1],
                        Memory = Convert.ToDecimal(cols[2].Replace(" GB", "")),
                        CPU = Convert.ToInt16(cols[4].Replace(" vCPUs", "")),
                        ECU = Convert.ToDecimal(cols[3].Replace(" units", "")),
                        MaxIPs = Convert.ToInt16(cols[16])
                    });
                }
                catch (Exception e)
                {
                    Console.Write(e);
                }

            }

            return instanceTypes;
        }

    }

    public class InstanceTypeDescription {
        public string Code { get; set; }
        public decimal Memory { get; set; }
        public decimal ECU { get; set; }
        public int CPU { get; set; }
        public int MaxIPs { get; set; }
    }


}
