using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Functions
{
    public class ExportController
    {
        IAmazonStepFunctions stepfunctions = new AmazonStepFunctionsClient();
        IAmazonLambda lambda = new AmazonLambdaClient();
        IAmazonS3 s3 = new AmazonS3Client();

        public const int DefaultMinutesBeforeExpire = 10;

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> GetStepFunctionDefinition(StepFunctionExportState @event)
        {
            if (string.IsNullOrEmpty(@event.ExportBucketName))
                throw new ArgumentException("Missing property: ExportBucketName");
            if (@event.MinutesBeforeExpire <= 0)
                @event.MinutesBeforeExpire = DefaultMinutesBeforeExpire;
            @event.ExportId = ("export-" + Guid.NewGuid()).ToLower();

            Console.WriteLine($"Getting definition for Step Function: {@event.StateMachineArn}");

            var resp = await stepfunctions.DescribeStateMachineAsync(new DescribeStateMachineRequest
            {
                StateMachineArn = @event.StateMachineArn
            });
            var stateMachine = JsonConvert.DeserializeObject<dynamic>(resp.Definition);
            @event.LambdaFunctionArns = (stateMachine.States as IEnumerable<dynamic>)
                .Where(s => s.Value.Type == "Task")
                .Select(s => (string)s.Value.Resource)
                .ToList();
            @event.StepFunctionDefinition = resp.Definition;

            Console.WriteLine($"Found {@event.LambdaFunctionsToExport} lambda functions to export.");

            @event.LambdaFunctionConfigs = new List<FunctionConfiguration>();
            @event.LambdaFunctionCodeLocations = new List<FunctionCodeLocation>();

            return @event;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> ExportLambdaFunction(StepFunctionExportState @event)
        {
            var lambdaArn = @event.LambdaFunctionArns.First();
            @event.LambdaFunctionArns.RemoveAt(0);
            
            Console.WriteLine($"Exporting lambda function: {lambdaArn}");

            var resp = await lambda.GetFunctionAsync(new GetFunctionRequest
            {
                FunctionName = lambdaArn
            });


            switch (resp.Code.RepositoryType)
            {
                case "S3":

                    var codeUrl = new Uri(resp.Code.Location);

                    using (var http = new HttpClient())
                    {
                        var codeResp = await http.GetAsync(resp.Code.Location);

                        Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                        var cleanedFunctionName = rgx.Replace(resp.Configuration.FunctionName, "");

                        var putReq = new PutObjectRequest
                        {
                            BucketName = @event.ExportBucketName,
                            Key = $"exports/{@event.ExportId}/{cleanedFunctionName}.zip",
                            InputStream = await codeResp.Content.ReadAsStreamAsync()
                        };

                        var uploadResp = await s3.PutObjectAsync(putReq);

                        var signedUrl = s3.GetPreSignedURL(new GetPreSignedUrlRequest
                        {
                            BucketName = putReq.BucketName,
                            Key = putReq.Key,
                            Expires = DateTime.UtcNow.AddMinutes(@event.MinutesBeforeExpire)
                        });

                        resp.Code.Location = signedUrl;
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unsupported respository type: {resp.Code.RepositoryType}");
            }

            @event.LambdaFunctionConfigs.Add(resp.Configuration);
            @event.LambdaFunctionCodeLocations.Add(resp.Code);

            return @event;
        }
    }

    public class StepFunctionExportState {
        public string ExportId { get; set; }
        public string ExportBucketName { get; set; }
        public int MinutesBeforeExpire { get; set; }
        public string StateMachineArn { get; set; }
        public List<string> LambdaFunctionArns { get; set; }
        public int LambdaFunctionsToExport { get { return LambdaFunctionArns.Count();  } set {  } }
        public List<FunctionConfiguration> LambdaFunctionConfigs { get; set; }
        public List<FunctionCodeLocation> LambdaFunctionCodeLocations { get; set; }
        public string StepFunctionDefinition { get;set; }
    }


}
