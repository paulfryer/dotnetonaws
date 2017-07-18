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
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon.Route53;
using Amazon.Route53.Model;
using System.Reflection;
using System.Text;

namespace Functions
{


    public sealed class CFProxyStateMachine : StateMachine<ValidateInputParameters>{
        
    }

    public interface IStateMachine {
        Type StartAt { get; }
        string Describe(string region, string accountId);
    }

    public abstract class StateMachine<TStartsAt> : IStateMachine 
        where TStartsAt : IState
    {
        public Type StartAt {
            get { 
                return this.GetType().GetTypeInfo().BaseType.GetGenericArguments()[0]; 
            }
        }

        public string Describe(string region, string accountId)
        {
            

            var sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine("\"StartAt\": \"" + StartAt.Name + "\",");
            sb.AppendLine("\"States\": {");

            var states = Assembly.GetEntryAssembly().GetTypes()
                                 .Where(t => typeof(IState).IsAssignableFrom(t) && 
                                        t.GetTypeInfo().IsClass &&
                                        t.GetTypeInfo().IsSealed)
                                     .Select(t => (IState)Activator.CreateInstance(t));
            
            var appendComma = false;
            foreach (var state in states){
                if (appendComma) sb.Append(",");
                DescribeState(sb, state, region, accountId);
                appendComma = true;
            }
   
            sb.Append("}");
            sb.AppendLine("}");

            return sb.ToString();

        }

        void DescribeState(StringBuilder sb, IState state, string region, string accountId)
        {
            sb.AppendLine("\"" + state.GetType().Name + "\" : { ");

            if (state is ITaskState)
            {
                var taskState = state as ITaskState;
                sb.AppendLine("\"Type\":\"Task\",");
                sb.AppendLine($"\"Resource\":\"arn:aws:lambda:{region}:{accountId}:function:{GetType().Name}-{state.Name}\",");
                sb.AppendLine($"\"Next\":\"{taskState.Next.Name}\"");
            }
            if (state is IChoiceState){
                var choiceState = state as IChoiceState;
                sb.AppendLine("\"Type\":\"Choice\",");
                sb.AppendLine("\"Choices\": [");
                var appendComma = false;
                foreach(var choice in choiceState.Choices){
                    if (appendComma) sb.Append(",");
                    sb.AppendLine("{");
                    sb.AppendLine("\"Variable\":\"$." + choice.Variable + "\",");
                    var stringValue = Convert.ToString(choice.Value);
                    if (choice.Operator.ToUpper().StartsWith("ST"))
                        stringValue = "\"" + stringValue + "\"";
                    if (choice.Operator.ToUpper().StartsWith("BO"))
                        stringValue = stringValue.ToLower();
                    sb.AppendLine($"\"{choice.Operator}\": {stringValue},");
                    sb.AppendLine($"\"Next\":\"{choice.Next.Name}\"");
                    sb.AppendLine("}");
                    appendComma = true;
                }
                sb.AppendLine("]");
            }
            if (state is IPassState){
                sb.AppendLine("\"Type\":\"Pass\",");
            }
            if (state is IWaitState){
                var waitState = state as IWaitState;
                sb.AppendLine("\"Type\":\"Wait\",");
                sb.AppendLine("\"Seconds\": " + waitState.Seconds + ",");
                sb.AppendLine($"\"Next\":\"{waitState.Next.Name}\"");

            }

            if (state.End)
                sb.AppendLine("\"End\": true");

            sb.AppendLine("}");
        }
    }

    public class StateMachineEngine<TStateMachine, TContext>
        where TStateMachine : IStateMachine
        where TContext : IContext
    {
        TStateMachine stateMachine;
        TContext context;

        public StateMachineEngine(TContext context)
        {
            stateMachine = (TStateMachine)Activator.CreateInstance(typeof(TStateMachine));
            this.context = context;
        }

        public async Task Start(){
            await ChangeState(stateMachine.StartAt);
        }

        private async Task ChangeState(Type type)
        {
            if (typeof(ITaskState<TContext>).IsAssignableFrom(type))
            {
                var taskState = Activator.CreateInstance(type) as ITaskState<TContext>;
                context = await taskState.Execute(context);
                if (!taskState.End)
                    await ChangeState(taskState.Next);
            }
            else if (typeof(IChoiceState).IsAssignableFrom(type))
            {
                var choiceState = Activator.CreateInstance(type) as IChoiceState;
                foreach (var choice in choiceState.Choices)
                {
                    var compairValue = typeof(TContext).GetProperty(choice.Variable).GetValue(context);

                    var operatorStart = choice.Operator.Substring(0, 2).ToUpper();

                    switch (operatorStart){
                        case "BO":
                            if ((bool)compairValue == (bool)choice.Value)
                                await ChangeState(choice.Next);
                            break;
                        case "NU":
                            var numericCompairValue = Convert.ToDecimal(compairValue);
                            var numericValue = Convert.ToDecimal(choice.Value);
                            switch (choice.Operator){
                                case Operator.NumericEquals:
                                    if (numericCompairValue == numericValue)
                                        await ChangeState(choice.Next);
                                    break;
                                case Operator.NumericGreaterThan:
                                    if (numericCompairValue > numericValue)
                                        await ChangeState(choice.Next);
                                    break;
                                default: throw new NotImplementedException("Not implemented: " + choice.Operator);
                            }
                            break;
                        default: throw new NotImplementedException("Operator not supported: " + choice.Operator);
                    }
                }

            }
            else throw new NotImplementedException("State type not implemented: " + type.Name);
        }

    }

    public interface IContext
    {

    }




    // public interface IPassState : IStat

    public interface IState
    {
        bool End { get; }
        string Name { get; }
    }

    public abstract class State : IState
    {
        public virtual bool End { get { return false; } }

        public virtual string Name
        {
            get { return this.GetType().Name; }
        }
    }

    public abstract class TaskState<TContext, TNext> : State, ITaskState<TContext>
        where TContext : IContext
        where TNext : IState
    {
        public Type Next {
            get {
                return GetType().GetTypeInfo().BaseType.GenericTypeArguments[1];
            }
        }

        public abstract Task<TContext> Execute(TContext context);
    }

    public interface ITaskState : IState {
        Type Next { get; }
    }

    public interface ITaskState<TContext> : ITaskState
    {
        Task<TContext> Execute(TContext context);
    }


    public interface IWaitState {
        int Seconds { get; }
        Type Next { get; }
    }

    public interface IChoiceState
    {

        List<Choice> Choices { get; }
    }

    public interface IPassState
    {

    }

    public class Choice 
    {
        public string Variable { get; set; }
        public string Operator { get; set; }
        public object Value { get; set; }
        public Type Next { get; set; }
    }


    public static class Operator
    {
        public const string And = "And";
        public const string BooleanEquals = "BooleanEquals";
        public const string Not = "Not";
        public const string NumericEquals = "NumericEquals";
        public const string NumericGreaterThan = "NumericGreaterThan";
        public const string NumericGreaterThanEquals = "NumericGreaterThanEquals";
        public const string NumericLessThan = "NumericLessThan";
        public const string NumericLessThanEquals = "NumericLessThanEquals";
        public const string Or = "Or";
        public const string StringEquals = "StringEquals";
        public const string StringGreaterThan = "StringGreaterThan";
        public const string StringGreaterThanEquals = "StringGreaterThanEquals";
        public const string StringLessThan = "StringLessThan";
        public const string StringLessThanEquals = "StringLessThanEquals";
        public const string TimestampEquals = "TimestampEquals";
        public const string TimestampGreaterThan = "TimestampGreaterThan";
        public const string TimestampGreaterThanEquals = "TimestampGreaterThanEquals";
        public const string TimestampLessThan = "TimestampLessThan";
        public const string TimestampLessThanEquals = "TimestampLessThanEquals";
    }

    public sealed class GetCert : TaskState<CFProxyState, CheckIfCertExists>
    {

        IAmazonCertificateManager certManager = new AmazonCertificateManagerClient(Amazon.RegionEndpoint.USEast1);


        public override async Task<CFProxyState> Execute(CFProxyState e)
        {
            var certs = await certManager.ListCertificatesAsync(
                new ListCertificatesRequest
                {

                }
            );

            // TODO: recursive iterate the results if there is a next token.


            foreach (var cert in certs.CertificateSummaryList)
            {
                if (cert.DomainName.ToLower() == "*." + e.DomainName.ToLower())
                {
                    e.CertExists = true;
                    e.CertArn = cert.CertificateArn;
                }
            }

            return e;
        }
    }

    public sealed class CheckIfCertExists : State, IChoiceState
    {
        public List<Choice> Choices
        {
            get
            {
                return new List<Choice>
                {
                    new Choice {
                        Variable = "CertExists",
                        Operator = Operator.BooleanEquals,
                        Value = false,
                        Next = typeof(RequestCert)
                    },
                    new Choice {
                        Variable = "CertExists",
                        Operator = Operator.BooleanEquals,
                        Value = true,
                        Next = typeof(GetCertApprovalStatus)
                    }
                };
            }
        }
    }

    public sealed class GetCertApprovalStatus : TaskState<CFProxyState, CheckIfCertIsApproved>
    {
        IAmazonCertificateManager certManager = new AmazonCertificateManagerClient(Amazon.RegionEndpoint.USEast1);

        public override async Task<CFProxyState> Execute(CFProxyState e)
        {
            var resp = await certManager.DescribeCertificateAsync(new DescribeCertificateRequest
            {
                CertificateArn = e.CertArn
            });

            if (resp.Certificate.Status == CertificateStatus.PENDING_VALIDATION)
                e.CertIsApproved = false;
            else if (resp.Certificate.Status == CertificateStatus.ISSUED)
                e.CertIsApproved = true;
            else throw new Exception("Unsupported certificate status: " + resp.Certificate.Status);

            return e;
        }
    }

    public sealed class CheckIfCertIsApproved : State, IChoiceState
    {
        public List<Choice> Choices {
            get {
                return new List<Choice>
                {
                    new Choice{
                        Variable = "CertIsApproved",
                        Operator = Operator.BooleanEquals,
                        Value = false,
                        Next = typeof(WaitForCertApproval)
                    },
                    new Choice{
                        Variable = "CertIsApproved",
                        Operator = Operator.BooleanEquals,
                        Value = true,
                        Next = typeof(ForEachRegion)
                    } 
                };
            }
        }
    }

    public sealed class ForEachRegion : State, IChoiceState
    {
        public List<Choice> Choices {
            get {
                return new List<Choice>{
                    new Choice{
                        Variable = "RegionsToProcess",
                        Operator = Operator.NumericGreaterThan,
                        Value = 0,
                        Next = typeof(ForEachService)
                    },
                    new Choice(){
                        Variable = "RegionsToProcess",
                        Operator = Operator.NumericEquals,
                        Value = 0,
                        Next = typeof(Done)
                    }
                };
            }
        }
    }

    public sealed class ForEachService : State, IChoiceState
    {
        public List<Choice> Choices
        {
            get
            {
                return new List<Choice>{
                    new Choice{
                        Variable = "ServicesToProcess",
                        Operator = Operator.NumericGreaterThan,
                        Value = 0,
                        Next = typeof(GetCloudFrontDistribution)
                    },
                    new Choice{
                        Variable = "ServicesToProcess",
                        Operator = Operator.NumericEquals,
                        Value = 0,
                        Next = typeof(ForEachRegion)
                    }
                };
            }
        }
    }

    public sealed class GetCloudFrontDistribution : TaskState<CFProxyState, CheckIfCloudFrontDistributionExists>
    {
        public override Task<CFProxyState> Execute(CFProxyState context)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class CheckIfCloudFrontDistributionExists : State, IChoiceState
    {
        public List<Choice> Choices {
            get {
                return new List<Choice>
                {
                    new Choice{
                        Variable = "DistributionExists",
                        Operator = Operator.BooleanEquals,
                        Value = true,
                        Next = typeof(GetDomainRecords)
                    },
                    new Choice{
                        Variable = "DistributionExists",
                        Operator = Operator.BooleanEquals,
                        Value = false,
                        Next = typeof(CreateCloudFrontDistribution)
                    }
                };
            }
        }
    }

    public sealed class GetDomainRecords : TaskState<CFProxyState, CheckIfRoute53CNAMEExists>
    {
        public override Task<CFProxyState> Execute(CFProxyState context)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class CheckIfRoute53CNAMEExists : State, IChoiceState
    {
        public List<Choice> Choices {get{
                return new List<Choice>
                {
                    new Choice{
                        Variable = "CNAMEExists",
                        Operator = Operator.BooleanEquals,
                        Value = true,
                        Next = typeof(ForEachService)
                    },
                    new Choice{
                        Variable = "CNAMEExists",
                        Operator = Operator.BooleanEquals,
                        Value = false,
                        Next = typeof(CreateRoute53CNAME)
                    }
                };
            }}
    }

    public sealed class CreateRoute53CNAME : TaskState<CFProxyState, ForEachService>
    {
        public override Task<CFProxyState> Execute(CFProxyState context)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class CreateCloudFrontDistribution : TaskState<CFProxyState, GetDomainRecords>
    {
        public override Task<CFProxyState> Execute(CFProxyState context)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class Done : State, IPassState{
        public override bool End
        {
            get
            {
                return true;
            }
        }
    }

    public sealed class WaitForCertApproval : State, IWaitState
    {
        public int Seconds { get { return 120; }}

        public Type Next { get { return typeof(GetCertApprovalStatus); }}

    }

    public sealed class RequestCert : TaskState<CFProxyState, WaitForCertApproval>
    {
        public override async Task<CFProxyState> Execute(CFProxyState context)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class ValidateInputParameters : TaskState<CFProxyState, GetCert>
    {
        public override async Task<CFProxyState> Execute(CFProxyState e)
        {
            if (string.IsNullOrEmpty(e.DomainName))
                throw new ArgumentException("DomainName is required.");

            if (string.IsNullOrEmpty(e.Regions))
                throw new Exception("Regions is required.");

            if (string.IsNullOrEmpty(e.Services))
                throw new ArgumentException("Services is required.");

            e.RegionsToProcess = e.Regions.Split(',').Count();
            e.ServicesToProcess = e.Services.Split(',').Count();

            return e;
        }
    }


    public class CFProxyController
    {
        IAmazonCertificateManager certManager;
        IAmazonCloudFront cloudFront;
        IAmazonRoute53 route53;

        public CFProxyController()
        {
            certManager = new AmazonCertificateManagerClient(Amazon.RegionEndpoint.USEast1);
            cloudFront = new AmazonCloudFrontClient();
            route53 = new AmazonRoute53Client();
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> ValidateInputParameters(CFProxyState e)
        {

            if (string.IsNullOrEmpty(e.DomainName))
                throw new ArgumentException("DomainName is required.");

            if (string.IsNullOrEmpty(e.Regions))
                throw new Exception("Regions is required.");

            if (string.IsNullOrEmpty(e.Services))
                throw new ArgumentException("Services is required.");

            e.RegionsToProcess = e.Regions.Split(',').Count();
            e.ServicesToProcess = e.Services.Split(',').Count();

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> GetCert(CFProxyState e)
        {
            var certs = await certManager.ListCertificatesAsync(
                new ListCertificatesRequest
                {

                }
            );

            // TODO: recursive iterate the results if there is a next token.


            foreach (var cert in certs.CertificateSummaryList)
            {
                if (cert.DomainName.ToLower() == "*." + e.DomainName.ToLower())
                {
                    e.CertExists = true;
                    e.CertArn = cert.CertificateArn;
                }
                else e.CertExists = false;
            }

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> RequestCert(CFProxyState e)
        {

            throw new NotImplementedException();

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> GetCertApprovalStatus(CFProxyState e)
        {


            var resp = await certManager.DescribeCertificateAsync(new DescribeCertificateRequest
            {
                CertificateArn = e.CertArn
            });

            if (resp.Certificate.Status == CertificateStatus.PENDING_VALIDATION)
                e.CertIsApproved = false;
            else if (resp.Certificate.Status == CertificateStatus.ISSUED)
                e.CertIsApproved = true;
            else throw new Exception("Unsupported certificate status: " + resp.Certificate.Status);

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> GetCloudFrontDistribution(CFProxyState e)
        {


            var regions = e.Regions.Split(',').ToList();
            var region = regions.First();
            regions.RemoveAt(0);
            e.Regions = string.Join(",", regions);
            e.RegionsToProcess = e.Regions.Count();

            var services = e.Services.Split(',').ToList();
            var service = services.First();
            services.RemoveAt(0);
            e.Services = string.Join(",", services);
            e.ServicesToProcess = e.Services.Count();

            e.DistributionDomainName = string.Format($"aws-{region}-{service}.{e.DomainName}");
            e.OriginDomainName = string.Format($"{service}.{region}.amazonaws.com");

            // TODO: implement paging.
            var resp = await cloudFront.ListDistributionsAsync(new ListDistributionsRequest
            {
                MaxItems = "100"
            });


            e.DistributionExists = resp.DistributionList.Items
                .Where(d => d.Aliases.Items.Any())
                .Any(d => d.Aliases.Items.First() == e.DistributionDomainName);

            if (e.DistributionExists)
            {
                foreach (var distribution in resp.DistributionList.Items)
                {
                    if (distribution.Aliases.Items.Contains(e.DistributionDomainName))
                    {
                        e.CloudFrontDistributionName = distribution.DomainName;
                    }
                }
            }

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> CreateCloudFrontDistribution(CFProxyState e)
        {

            var resp = await cloudFront.CreateDistributionAsync(new CreateDistributionRequest
            {
                DistributionConfig = new DistributionConfig
                {
                    CallerReference = e.DistributionDomainName,
                    Enabled = true,

                    DefaultCacheBehavior = new DefaultCacheBehavior
                    {
                        TrustedSigners = new TrustedSigners
                        {
                            Quantity = 0,
                            Enabled = false
                        },
                        MinTTL = 0,
                        ViewerProtocolPolicy = ViewerProtocolPolicy.HttpsOnly,
                        ForwardedValues = new ForwardedValues
                        {
                            Cookies = new CookiePreference
                            {
                                Forward = ItemSelection.All
                            },
                            Headers = new Headers
                            {
                                Items = new List<string> { "*" },
                                Quantity = 1
                            },
                            QueryString = true,
                            QueryStringCacheKeys = new QueryStringCacheKeys
                            {
                                Quantity = 0
                            }
                        },
                        TargetOriginId = e.OriginDomainName,
                        AllowedMethods = new AllowedMethods
                        {
                            Quantity = 7,
                            Items = new List<string>{
                                "HEAD", "DELETE", "POST", "GET", "OPTIONS", "PUT", "PATCH"
                            },
                            CachedMethods = new CachedMethods
                            {
                                Quantity = 3,
                                Items = new List<string>{
                                    "GET", "HEAD", "OPTIONS"
                                }
                            }
                        }
                    },
                    Comment = string.Format($"AWS Service Proxy. AWS Service: {e.OriginDomainName}, CNAME: {e.DistributionDomainName}"),
                    Aliases = new Aliases
                    {
                        Quantity = 1,
                        Items = new List<string>{
                            e.DistributionDomainName
                        }
                    },
                    Origins = new Origins
                    {

                        Quantity = 1,
                        Items = new List<Origin>{
                            new Origin{

                                CustomOriginConfig = new CustomOriginConfig{
                                    OriginProtocolPolicy = OriginProtocolPolicy.HttpsOnly,
                                    OriginSslProtocols = new OriginSslProtocols{
                                        Items = new List<string>{
                                            "TLSv1.2", "TLSv1.1", "TLSv1"
                                        },
                                        Quantity = 3
                                    },
                                    HTTPPort = 80,
                                    HTTPSPort = 443
                                },
                                DomainName = e.OriginDomainName,
                                Id = e.OriginDomainName
                            }
                        }
                    },
                    ViewerCertificate = new ViewerCertificate
                    {
                        ACMCertificateArn = e.CertArn,
                        SSLSupportMethod = SSLSupportMethod.SniOnly
                    }
                }
            });

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> GetDomainRecords(CFProxyState e)
        {
            var resp = await route53.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
            {
            });
            Console.Write(JsonConvert.SerializeObject(resp));

            var hostedZone = resp.HostedZones.SingleOrDefault(h => h.Name == e.DomainName + ".");

            if (hostedZone == null)
            {
                Console.Write("Could not find hosted zone with domain name: " + e.DomainName);
                throw new Exception("Could not find hosted zone with domain name: " + e.DomainName);
            }
            else Console.Write("Found hosted zone: " + hostedZone.Id);

            var resp2 = await route53.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
            {
                HostedZoneId = hostedZone.Id
            });
            Console.Write(JsonConvert.SerializeObject(resp2.ResourceRecordSets));
            foreach (var recordSet in resp2.ResourceRecordSets)
            {
                if (recordSet.Name == e.DistributionDomainName + ".")
                {
                    e.CNAMEExists = true;
                    e.HostedZoneId = resp.HostedZoneId;
                }
            }

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> CreateRoute53CNAME(CFProxyState e)
        {
            var resp = await route53.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = e.HostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>{
                        new Change{
                            Action = ChangeAction.CREATE,
                            ResourceRecordSet = new ResourceRecordSet{
                                Type = RRType.A,
                                Name = e.DistributionDomainName,
                                AliasTarget = new AliasTarget{
                                    DNSName = e.CloudFrontDistributionName
                                },
                                TTL = 500
                            }
                        }
                    }
                }
            });



            return e;
        }
    }

    public class CFProxyState : IContext
    {

        public string WAFARN { get; set; }
        public string Regions { get; set; }
        public string Services { get; set; }
        public string DomainName { get; set; }

        public bool CertExists { get; set; }
        public string CertArn { get; set; }
        public bool CertIsApproved { get; set; }

        public int RegionsToProcess { get; set; }
        public int ServicesToProcess { get; set; }
        public string DistributionDomainName { get; set; }
        public string CloudFrontDistributionName { get; set; }
        public string OriginDomainName { get; set; }
        public bool DistributionExists { get; set; }
        public bool CNAMEExists { get; set; }

        public string HostedZoneId { get; set; }

    }


}
