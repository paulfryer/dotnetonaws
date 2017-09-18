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
            await controller.SyncToDynamo(null, null);
            Console.WriteLine("DONE!");
        }

}


}
