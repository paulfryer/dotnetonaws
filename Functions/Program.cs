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
            var description = (new CFProxyStateMachine()).Describe("us-west-2", "072676109536");
            Console.Write(description);

            var context = new CFProxyState
            {
                Services = "iot",
                Regions = "us-west-2",
                DomainName = "api.octank.biz"
            };
            var engine = new StateMachineEngine<CFProxyStateMachine, CFProxyState>(context);

            engine.Start();

            //TestFlckr().Wait();

            //TestSyncPricesAsync().Wait();

            Console.ReadKey();
        }

        public static async Task TestFlckr(){
            var f = new FlickrController();

    
            var r = await f.SearchImages(new FlickrController.ImageProcessingState{
                APIKey = "enterapikeyhere",
                Page = 0,
                PageSize = 100,
                Tags = "ddd,ddd"
            });

        }

        private static async Task TestSyncPricesAsync()
        {
            var controller = new SpotController();
            await controller.SyncToDynamo(null, null);            
            Console.WriteLine("DONE!");

        }
    }


}
