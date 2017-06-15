﻿using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.KinesisFirehose;
using Amazon.KinesisFirehose.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Functions
{

    public class SpotController
    {

        private IAmazonDynamoDB dynamo = new AmazonDynamoDBClient();
        private AmazonEC2Client ec2 = new AmazonEC2Client();
        private AmazonCloudWatchClient cloudWatch = new AmazonCloudWatchClient();
        private AmazonKinesisFirehoseClient firehose = new AmazonKinesisFirehoseClient();

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task SyncToDynamo(object @event, ILambdaContext context)
        {
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
            var respx = await ec2.DescribeSpotPriceHistoryAsync(new DescribeSpotPriceHistoryRequest());
            var groups = respx.SpotPriceHistory.GroupBy(s => string.Format("{0}|{1}|{2}", s.AvailabilityZone, s.InstanceType, s.ProductDescription));
            var latestPrices = groups.Select(g => new { g.Key, Value = g.OrderByDescending(v => v.Timestamp).First() }).ToList();


            //var facetsBatch = 

            DynamoDBContext context = new DynamoDBContext(dynamo);
            while (latestPrices.Count > 0)
            {
                Console.WriteLine("Items Left: " + latestPrices.Count);
                var batchSize = 25;
                if (batchSize > 25)
                    batchSize = 25;
                var maxRows = latestPrices.Count >= batchSize ? batchSize : latestPrices.Count;
                var rowsToSync = latestPrices.GetRange(0, maxRows);
                latestPrices.RemoveRange(0, maxRows);
                var observationBatch = context.CreateBatchWrite<FlatPriceObservation>();
                var writeRequests = new List<WriteRequest>();
                foreach (var r in rowsToSync)
                {
                    var instanceType = instanceTypes.Single(it => it.Code == r.Value.InstanceType);
                    var o = new FlatPriceObservation(new PriceObservation(r.Value, instanceType));
                    observationBatch.AddPutItem(o);
                }



                var observationWriteTask = observationBatch.ExecuteAsync();
                await Task.WhenAll(observationWriteTask, Task.Delay(1000));
            }
        }

        public async Task<string> QuerySpotHistory(RegionEndpoint region, List<string> csv, string nextToken)
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


}