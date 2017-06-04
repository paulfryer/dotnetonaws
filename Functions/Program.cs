using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.KinesisFirehose;
using Amazon.KinesisFirehose.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.Serialization.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

            /*
            var csv = new List<string>();
            string nextToken = "";
            while (nextToken != null) {
                nextToken = await controller.QuerySpotHistory(Amazon.RegionEndpoint.USGovCloudWest1, csv, nextToken);
            }
            */


            // var dynamoEventJson = File.OpenText("DynamoEvent.json").ReadToEnd();
            // var @event = JsonConvert.DeserializeObject<DynamoDBEvent>(dynamoEventJson);
            // await controller.SyncToCloudWatch(@event, null);


            await controller.SyncToDynamo(null, null);
            
            Console.WriteLine("DONE!");

        }
    }



    public class SpotController
    {

        private IAmazonDynamoDB dynamo = new AmazonDynamoDBClient();
        private AmazonEC2Client ec2 = new AmazonEC2Client();
        private AmazonCloudWatchClient cloudWatch = new AmazonCloudWatchClient();
        private AmazonKinesisFirehoseClient firehose = new AmazonKinesisFirehoseClient();


        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task SyncToDynamo(object @event, ILambdaContext context) {
            var restrictedRegions = new List<RegionEndpoint> { RegionEndpoint.CNNorth1, RegionEndpoint.USGovCloudWest1 };

            foreach (var region in RegionEndpoint.EnumerableAllRegions)
                if (!restrictedRegions.Contains(region))
                    await SyncToDynamoByRegion(region);
        }

        public async Task SyncToDynamoByRegion(RegionEndpoint region)
        {

            Console.WriteLine("Starting to get prices.");
            Console.WriteLine("PROCESSING REGION: " + region);

            var instanceTypes = GetInstanceTypeDescriptions();

            ec2 = new AmazonEC2Client(region);

            var respx = await ec2.DescribeSpotPriceHistoryAsync(new DescribeSpotPriceHistoryRequest
            {

            });


            var groups = respx.SpotPriceHistory.GroupBy(s => string.Format("{0}|{1}|{2}", s.AvailabilityZone, s.InstanceType, s.ProductDescription));


            var latestPrices = groups.Select(g => new { g.Key, Value = g.OrderByDescending(v => v.Timestamp).First() }).ToList();

            //resp.NextToken


            while (latestPrices.Count > 0)
            {
                Console.WriteLine("Items Left: " + latestPrices.Count);

                var batchSize = 25;

                if (batchSize > 25)
                    batchSize = 25;

                var maxRows = latestPrices.Count >= batchSize ? batchSize : latestPrices.Count;
                var rowsToSync = latestPrices.GetRange(0, maxRows);
                latestPrices.RemoveRange(0, maxRows);


                var writeRequests = new List<WriteRequest>();

                foreach (var r in rowsToSync)
                {
                    var instanceType = instanceTypes.Single(it => it.Code == r.Value.InstanceType);
                    var o = new FlatPriceObservation(new PriceObservation(r.Value, instanceType));


                    var partitionKey = "PR|AR|RE|RI|FA|GE|SI|AZ";
                    var sortKey = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}", o.PR, o.AR, o.RE, o.RI, o.FA, o.GE, o.SI, o.AZ );

                    var item = new Dictionary<string, AttributeValue> {
                        { "PK", new AttributeValue(partitionKey) },
                        { "SK", new AttributeValue(sortKey) } };
                    foreach (var property in o.GetType().GetProperties())
                    {
                        var attributeValue = new AttributeValue();
                        var value = Convert.ToString(property.GetValue(o));
                        if (property.PropertyType == typeof(Decimal) || property.PropertyType == typeof(int))
                            attributeValue.N = value;
                        else
                            attributeValue.S = value;
                        item.Add(property.Name, attributeValue);
                    }
                    writeRequests.Add(new WriteRequest
                    {
                        PutRequest = new PutRequest
                        {
                            Item = item
                        }
                    });
                }
                
                var writeTask = dynamo.BatchWriteItemAsync(new BatchWriteItemRequest
                {
                    RequestItems = new Dictionary<string, List<WriteRequest>>
                    {
                        { "SpotPrice", writeRequests}
                    }
                });

                await Task.WhenAll(writeTask, Task.Delay(1000));
            }
        }

        public async Task<string> QuerySpotHistory(Amazon.RegionEndpoint region, List<string> csv, string nextToken)
        {
            Console.WriteLine(string.Format("Processing Region: {0}, NextToken: {1}", region, nextToken ?? nextToken.Substring(0, 10)));
            var instanceTypes = GetInstanceTypeDescriptions();
            using (var ec2Client = new AmazonEC2Client(region))
            {
                var req = new DescribeSpotPriceHistoryRequest();
                if (nextToken != "")
                    req.NextToken = nextToken;
                var resp = await ec2.DescribeSpotPriceHistoryAsync(req);
                nextToken = resp.NextToken;
                foreach (var spotPrice in resp.SpotPriceHistory)
                {
                    var instanceType = instanceTypes.Single(it => it.Code == spotPrice.InstanceType);
                    var observation = new PriceObservation(spotPrice, instanceType);
                    csv.Add(observation.ToCSV());
                }
            }
            return nextToken;
        }

        public async Task SyncToFirehose(Amazon.RegionEndpoint region, ILambdaContext context)
        {

            Console.WriteLine("Starting to get prices.");
            Console.WriteLine("PROCESSING REGION: " + region);

            var instanceTypes = GetInstanceTypeDescriptions();

            ec2 = new AmazonEC2Client(region);

            var resp = await ec2.DescribeSpotPriceHistoryAsync(new Amazon.EC2.Model.DescribeSpotPriceHistoryRequest
            {

            });

            //resp.NextToken

            Console.Write(resp.NextToken);


            var tasks = new List<Task>();

            while (resp.SpotPriceHistory.Count > 0)
            {

                Console.WriteLine("Rows Left: " + resp.SpotPriceHistory.Count);
                var maxRows = resp.SpotPriceHistory.Count >= 250 ? 250 : resp.SpotPriceHistory.Count;
                var rowsToSync = resp.SpotPriceHistory.GetRange(0, maxRows);
                resp.SpotPriceHistory.RemoveRange(0, maxRows);


                var records = new List<Amazon.KinesisFirehose.Model.Record>();

                foreach (var r in rowsToSync)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        var streamWriter = new StreamWriter(memoryStream);

                        var parts = new List<string>
                        {
                            r.AvailabilityZone.Split('-')[0],
                            r.AvailabilityZone.Split('-')[1],
                            r.AvailabilityZone.Split('-')[2],
                            r.InstanceType.Value.Substring(0, r.InstanceType.Value.IndexOf('.') - 1),
                            r.InstanceType.Value.Substring(r.InstanceType.Value.IndexOf('.') - 1, 1),
                            r.InstanceType.Value.Substring(r.InstanceType.Value.IndexOf('.') + 1, r.InstanceType.Value.Length - r.InstanceType.Value.IndexOf('.') - 1),
                            r.Price,
                            r.ProductDescription,
                            Convert.ToString(r.Timestamp.ToUniversalTime()),
                            String.Format("{0:0.000000}", Convert.ToDecimal(r.Price) / instanceTypes.Single(it=>it.Code==r.InstanceType).CPU),
                            String.Format("{0:0.000000}", Convert.ToDecimal(r.Price) / instanceTypes.Single(it=>it.Code==r.InstanceType).ECU),
                            String.Format("{0:0.000000}", Convert.ToDecimal(r.Price) / instanceTypes.Single(it=>it.Code==r.InstanceType).Memory)
                        };
                        streamWriter.WriteLine(string.Join(",", parts));

                        records.Add(new Amazon.KinesisFirehose.Model.Record
                        {
                            Data = memoryStream
                        });

                        streamWriter.Dispose();
                    }
                }



                var t = firehose.PutRecordBatchAsync(new PutRecordBatchRequest
                {
                    DeliveryStreamName = "SpotPrice",
                    Records = records
                });

                tasks.Add(t);
            }
            await Task.WhenAll(tasks);
        }

        private static List<InstanceTypeDescription> instanceTypeDescriptions;

        private List<InstanceTypeDescription> GetInstanceTypeDescriptions()
        {
            if (instanceTypeDescriptions != null)
                return instanceTypeDescriptions;

            var instanceTypesFile = System.IO.File.OpenText("InstanceTypes.csv");

            var csv = instanceTypesFile.ReadToEnd();
            csv = csv.Replace("\"", "");
            var rows = csv.Split('\n').ToList();
            // remove header
            rows.RemoveRange(0, 1);

            var instanceTypes = new List<InstanceTypeDescription>();

            foreach (var row in rows)
            {
                //Console.WriteLine(row);
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

            instanceTypeDescriptions = instanceTypes;

            return instanceTypes;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task SyncToCloudWatch(DynamoDBEvent @event, ILambdaContext context)
        {



            // Console.Write(JsonConvert.SerializeObject(@event));

            var records = @event.Records.ToList();
            var tasks = new List<Task>();

            while (records.Count > 0)
            {


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

    }


    public class PriceObservation
    {

        public InstanceTypeDescription InstanceType { get; private set; }
        public AvailabilityZone AvailabilityZone { get; private set; }

        public PriceObservation(SpotPrice spotPrice, InstanceTypeDescription instanceType)
        {
            InstanceType = instanceType;
            AvailabilityZone = new AvailabilityZone(spotPrice.AvailabilityZone);
            Price = Convert.ToDecimal(String.Format("{0:0.0000}", Convert.ToDecimal(spotPrice.Price)));
            Timestamp = spotPrice.Timestamp.ToUniversalTime();
            Product = spotPrice.ProductDescription;

            PricePerCPU = Decimal.Parse(String.Format("{0:0.00000}", Price / InstanceType.CPU));
            PricePerECU = Decimal.Parse(String.Format("{0:0.00000}", Price / InstanceType.ECU));
            PricePerGB = Decimal.Parse(String.Format("{0:0.00000}", Price / InstanceType.Memory));

        }

        public DateTime Timestamp { get; private set; }
        public decimal Price { get; private set; }
        public string Product { get; private set; }

        public decimal PricePerCPU { get; private set; }
        public decimal PricePerECU { get; private set; }
        public decimal PricePerGB { get; private set; }

        public string ToJSON() {
            var miniObservation = new FlatPriceObservation(this);
            return JsonConvert.SerializeObject(miniObservation);
        }

        public string ToCSV()
        {
            var parts = new List<string>
            {
                AvailabilityZone.Area,
                AvailabilityZone.Region,
                AvailabilityZone.RegionInstance,
                AvailabilityZone.AZ,
                InstanceType.Family,
                InstanceType.Generation,
                InstanceType.Size,
                Product,
                Convert.ToString(Timestamp),
                Convert.ToString(Price),
                Convert.ToString(PricePerCPU),
                Convert.ToString(PricePerECU),
                Convert.ToString(PricePerGB)
            };
            return string.Join(",", parts);
        }
    }

    public class FlatPriceObservation {

        public FlatPriceObservation()
        {

        }

        public FlatPriceObservation(PriceObservation o)
        {
            AR = o.AvailabilityZone.Area;
            RE = o.AvailabilityZone.Region;
            RI = o.AvailabilityZone.RegionInstance;
            AZ = o.AvailabilityZone.AZ;
            FA = o.InstanceType.Family;
            GE = o.InstanceType.Generation;
            SI = o.InstanceType.Size;
            PR = o.Product;            
            PU = o.Price;
            PC = o.PricePerCPU;
            PE = o.PricePerECU;
            PM = o.PricePerGB;
            LT = o.Timestamp;
            IT = DateTime.UtcNow;
        }

        public string AR { get; set; }
        public string RE { get; set; }
        public string RI { get; set; }
        public string AZ { get; set; }
        public string FA { get; set; }
        public string GE { get; set; }
        public string SI { get; set; }
        public string PR { get; set; }
        public DateTime LT { get; set; }
        public decimal PU { get; set; }
        public decimal PC { get; set; }
        public decimal PE { get; set; }
        public decimal PM { get; set; }
        public DateTime IT { get; set; }
    };

    public class AvailabilityZone
    {

        public AvailabilityZone(string availabilityZone)
        {
            var azParts = availabilityZone.Split('-');
            Area = azParts[0];
            Region = azParts[1];
            RegionInstance = azParts[2].Substring(0, 1);
            AZ = azParts[2].Substring(1, 1);
        }

        public string Area { get; private set; }
        public string Region { get; private set; }
        public string AZ { get; private set; }
        public string RegionInstance { get; private set; }
    }

   

    public class InstanceTypeDescription
    {
        public string Code { get; set; }
        public decimal Memory { get; set; }
        public decimal ECU { get; set; }
        public int CPU { get; set; }
        public int MaxIPs { get; set; }

        public string Family
        {
            get
            {
                return Code.Substring(0, Code.IndexOf('.') - 1);
            }
        }

        public string Generation
        {
            get { return Code.Substring(Code.IndexOf('.') - 1, 1); }
        }

        public string Size
        {
            get
            {
                return Code.Substring(Code.IndexOf('.') + 1, Code.Length - Code.IndexOf('.') - 1);
            }
        }

    }


}
