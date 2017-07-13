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
    public class CFProxyController
    {

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> ValidateInputParameters(CFProxyState e)
        {
            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> RequestCert(CFProxyState e)
        {
            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> GetCertApprovalStatus(CFProxyState e)
        {
            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> GetCloudFrontDistribution(CFProxyState e)
        {
            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> CreateCloudFrontDistribution(CFProxyState e)
        {
            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> GetDomainRecords(CFProxyState e)
        {
            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CFProxyState> CreateRoute53CNAME(CFProxyState e)
        {
            return e;
        }
    }

    public class CFProxyState {

        public string WAFARN { get; set; }
        public string Regions { get; set; }
        public string Services { get; set; }
        public string DomainName { get; set; }

        public bool CertExists { get; set; }
        public bool CertIsApproved { get; set; }

        public int RegionsToProcess { get; set; }
        public int ServicesToProcess { get; set; }

        public bool DistributionExists { get; set; }
        public bool CNAMEExists { get; set; }

    }


}
