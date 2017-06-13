using Amazon;
using Amazon.Athena;
using Amazon.Athena.Model;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Functions
{


    public class BigFileController {

        IAmazonAthena athena = new AmazonAthenaClient();
        IAmazonSQS sqs = new AmazonSQSClient();


        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> StartQueryExecution(dynamic @event, ILambdaContext context)
        {
            try
            {
                var resp = await athena.StartQueryExecutionAsync(new StartQueryExecutionRequest
                {
                    QueryExecutionContext = new QueryExecutionContext
                    {
                        Database = @event.Database
                    },
                    ResultConfiguration = new ResultConfiguration
                    {
                        OutputLocation = @event.OutputLocation                        
                    },
                    QueryString = @event.QueryString
                });

                @event.QueryExecutionId = resp.QueryExecutionId;
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                @event.ErrorMessage = ex.Message;
            }


            return @event;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> ProcessQueryResults(dynamic @event, ILambdaContext context)
        {

            try
            {
                var resp = await athena.GetQueryResultsAsync(new GetQueryResultsRequest
                {
                    QueryExecutionId = @event.QueryExecutionId,
                    NextToken = @event.NextToken,
                    MaxResults = 100
                });
                
                var sqsTasks = new List<Task>();
                // it looks like the first row is always the headers, so we'lls skip this.
                // however a better method would be to NOT skip this and check if the row looks/feels like a 
                // header and skip it that way. This way we don't throw away a row that might actually be a 
                // non header.                
                foreach (var r in resp.ResultSet.Rows.Skip(1))
                {
                    var record = new List<string>();
                    foreach (var d in r.Data)
                    {
                        record.Add(d.VarCharValue);
                    }

                    /*
                    sqs.SendMessageBatchAsync(new SendMessageBatchRequest {
                        Entries = 
                    });
                    */

                    var t = sqs.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = @event.QueueUrl,
                        MessageBody = JsonConvert.SerializeObject(record.ToArray())
                    });
                    sqsTasks.Add(t);
                }

                await Task.WhenAll(sqsTasks);

                if (string.IsNullOrEmpty(resp.NextToken)) 
                    @event.QueryState = "DONE";
                else
                    @event.QueryState = "COMPLETED_WITH_MORE";


                @event.NextToken = resp.NextToken;                
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                @event.QueryStateMessage = ex.Message;
                if (ex.Message.Contains("RUNNING"))
                    @event.QueryState = "RUNNING";
                else if (ex.Message.Contains("FAILED"))
                    @event.QueryState = "FAILED";
                else @event.QueryState = "UNKNOWN";
            }


            return @event;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task DeleteFile(object @event, ILambdaContext context)
        {

            throw new NotImplementedException();
        }


    }


}
