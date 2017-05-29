using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.KeyManagementService;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebApplication1.Controllers
{ 
    [Route("api/[controller]")]
    public class ExchangesController : Controller {

        private IAmazonKeyManagementService kmsClient;
        private IAmazonDynamoDB dynamoClient;
        private IAmazonS3 s3Client;
        private IAmazonRekognition rekognitionClient;

        public ExchangesController(IAmazonKeyManagementService kmsClient, 
            IAmazonDynamoDB dynamoClient, IAmazonS3 s3Client, IAmazonRekognition rekognitionClient)
        {
            this.rekognitionClient = rekognitionClient;
            this.s3Client = s3Client;
            this.kmsClient = kmsClient;
            this.dynamoClient = dynamoClient;

            this.dynamoClient = new AmazonDynamoDBClient();
           
        }

        [HttpPost]
        [Route("image")]
        public async Task<IActionResult> PostAsync() {

            //var iot = new Amazon.IotData.AmazonIotDataClient("somurl");

           

            var detectFacesRequest = new DetectFacesRequest
            {
                Image = new Image
                {
                    S3Object = new S3Object
                    {
                        Bucket = "padnug",
                        Name = "IMG_1938.JPG"
                    }
                }
            };
            var result = await rekognitionClient.DetectFacesAsync(detectFacesRequest);

            return new ContentResult
            {
                ContentType = "application/json",
                Content = JsonConvert.SerializeObject(result.FaceDetails),
                StatusCode = 200
            };
        }

       [HttpGet]
       [Route("")]
        public async Task<IActionResult> GetAsync()
        {

            try
            {
                var queryRequest = new QueryRequest("bitdango")
                {
                    KeyConditions = new Dictionary<string, Condition>
                    {
                        { "PartitionKey", new Condition
                        {
                            ComparisonOperator = ComparisonOperator.EQ,
                            AttributeValueList = new List<AttributeValue>
                            {
                                new AttributeValue("exchange|coinbase")
                            }
                        } }
                    }
                };
                

                var queryResponse = await dynamoClient.QueryAsync(queryRequest);

                var result = new ContentResult
                {
                    ContentType = "application/json",
                    Content = JsonConvert.SerializeObject(queryResponse.Items)
                };
                return result;

            }catch(Exception ex)
            {
                return new BadRequestResult();
            }            
        }

        [HttpGet]
        [Route("{exchangeCode}/accounts")]
        public string GetAccounts(string exchangeCode)
        {
            return "GETTING: " + exchangeCode;
        }

        [HttpPost]
        [Route("{exchangeCode}/payment")]
        public string CreatePayment(string exchangeCode)
        {
            return "MAking a payment: " + exchangeCode;
        }

    }
}
