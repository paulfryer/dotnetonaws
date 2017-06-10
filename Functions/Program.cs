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

            //await controller.QueryAthena();

            await controller.SyncToDynamo(null, null);
            
            Console.WriteLine("DONE!");

        }
    }


}
