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

namespace Functions
{
    public class CFProxyController
    {
        IAmazonCertificateManager certManager;

        public CFProxyController()
        {
            certManager = new AmazonCertificateManagerClient(Amazon.RegionEndpoint.USEast1);
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


            foreach (var cert in certs.CertificateSummaryList){
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

            //e.CertIsApproved = resp.

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
        public string CertArn { get; set; }
        public bool CertIsApproved { get; set; }

        public int RegionsToProcess { get; set; }
        public int ServicesToProcess { get; set; }

        public bool DistributionExists { get; set; }
        public bool CNAMEExists { get; set; }

    }


}
