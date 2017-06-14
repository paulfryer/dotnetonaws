using Amazon;
using Amazon.Lambda.Serialization.Json;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Functions
{
    class Program
    {
        static void Main(string[] args)
        {
            TestExporter();

            //TestSyncPricesAsync().Wait();

            Console.ReadKey();
        }
        private static async Task TestExporter()
        {
            var c = new ExportController();

            var @event = new StepFunctionExportState{
                StateMachineArn = "arn:aws:states:us-west-2:989469592528:stateMachine:PollForTweets-2",
                ExportBucketName = "code-build-output"
            };

            @event = await c.GetStepFunctionDefinition(@event);

            while (@event.LambdaFunctionsToExport > 0)
                await c.ExportLambdaFunction(@event);

            Console.ReadKey();
        }

        private static async Task TestSyncPricesAsync()
        {

            var c = new ImageNetController();
            var r = await c.TagImage(null, null);


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

            //await controller.QueryAthena();

            await controller.SyncToDynamo(null, null);
            
            Console.WriteLine("DONE!");

        }
    }


}
