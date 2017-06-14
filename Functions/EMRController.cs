using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.ElasticMapReduce;
using Amazon.ElasticMapReduce.Model;
using System.Collections.Generic;
using System.Linq;

namespace Functions
{
    public class EMRController
    {
        IAmazonElasticMapReduce emr = new AmazonElasticMapReduceClient();

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> StartCluster(ProcessJobState e)
        {

            if (string.IsNullOrEmpty(e.ClusterServiceRole))
                e.ClusterServiceRole = "EMR_DefaultRole";
            if (string.IsNullOrEmpty(e.ClusterJobFlowRole))
                e.ClusterJobFlowRole = "EMR_EC2_DefaultRole";

            var masterFleet = new InstanceFleetConfig
            {
                InstanceFleetType = "MASTER",
                TargetOnDemandCapacity = 1,
                InstanceTypeConfigs = new List<InstanceTypeConfig>{
                                new InstanceTypeConfig{
                                    InstanceType = "m1.medium"
                                }
                            }
            };

            var coreFleet = new InstanceFleetConfig
            {
                InstanceFleetType = "CORE",
                TargetOnDemandCapacity = 1,
                InstanceTypeConfigs = new List<InstanceTypeConfig>{
                                new InstanceTypeConfig{
                                    InstanceType = "m1.medium"
                                }
                            }
            };

            var taskFleet = new InstanceFleetConfig
            {
                InstanceFleetType = "TASK",
                TargetSpotCapacity = 2,
                InstanceTypeConfigs = new List<InstanceTypeConfig>{
                                new InstanceTypeConfig{
                                    InstanceType = "m1.medium"
                                }
                            }
            };

            var runResp = await emr.RunJobFlowAsync(
                new RunJobFlowRequest
                {
                    ReleaseLabel = "emr-5.2.0",
                    Name = "AutoProvisionedEmrCluster",
                    ServiceRole = e.ClusterServiceRole,
                    JobFlowRole = e.ClusterJobFlowRole,
                    Instances = new JobFlowInstancesConfig
                    {
                        KeepJobFlowAliveWhenNoSteps = true,
                        InstanceFleets = new List<InstanceFleetConfig>
                        {
                        masterFleet,
                        coreFleet,
                        taskFleet
                    }
                    }
                });


            e.JobFlowId = runResp.JobFlowId;

            return e;
        }



        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> GetClusterStatus(ProcessJobState e)
        {

            var jobFlowsResp = await emr.DescribeJobFlowsAsync(new DescribeJobFlowsRequest
            {
                JobFlowIds = new List<string> { e.JobFlowId }
            });

            e.ClusterStatus = jobFlowsResp.JobFlows.First().ExecutionStatusDetail.State.Value;

            /*
            var resp = await emr.DescribeClusterAsync(new DescribeClusterRequest{
                ClusterId = e.JobFlowId
            });

            e.ClusterStatus = resp.Cluster.Status.State.Value;
            */

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> SubmitJob(ProcessJobState e)
        {
            // TODO: submit job here.
            await Task.Delay(1000);

            return e;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> TerminateCluster(ProcessJobState e)
        {
            var resp = await emr.TerminateJobFlowsAsync(new TerminateJobFlowsRequest { JobFlowIds = new List<string> { e.JobFlowId } });

            return e;
        }

        public class ProcessJobState
        {

            public string ClusterServiceRole { get; set; }
            public string ClusterJobFlowRole { get; set; }
            public string JobFlowId { get; set; }
            public string ClusterStatus { get; set; }
        }
    }
}
