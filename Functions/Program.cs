using Amazon.CloudWatch;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.EC2;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
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

            await controller.SyncToDynamo("", null);

            Console.WriteLine("DONE!");

        }
    }

   

    public class SpotController {

        private IAmazonDynamoDB dynamo = new AmazonDynamoDBClient();
        private AmazonEC2Client ec2 = new AmazonEC2Client();
        private AmazonCloudWatchClient cloudWatch = new AmazonCloudWatchClient();

        public async Task SyncToCloudWatch(dynamic input, ILambdaContext context) {

            // TODO: figure out what the sync object looks like.


            throw new NotImplementedException();
            /*
            cloudWatch.PutMetricDataAsync(new Amazon.CloudWatch.Model.PutMetricDataRequest
            {
                Namespace = "SpotPriceHistory"
                //MetricData = new }
            );*/
        }

        public async Task SyncToDynamo(dynamic input, ILambdaContext context) {

            Console.WriteLine("Starting to get prices.");

            var resp = await ec2.DescribeSpotPriceHistoryAsync();


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
                           {"SortKey", new AttributeValue(Convert.ToString(r.Timestamp)) },
                           {"Price", new AttributeValue(Convert.ToString(r.Price)) },
                           {"ProductDescription", new AttributeValue(r.ProductDescription) },
                           {"InstanceType", new AttributeValue(r.InstanceType) },
                                 {"Timestamp", new AttributeValue(Convert.ToString(r.Timestamp)) },

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

                // throttle the write rate to a minimum of 250ms per request.
                await Task.WhenAll(writeTask, Task.Delay(250));
                
            }
        }

    }
}
