using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.ElasticMapReduce;
using Amazon.ElasticMapReduce.Model;
using System.Collections.Generic;

namespace Functions
{
    public class EMRController
    {
        IAmazonElasticMapReduce emr = new AmazonElasticMapReduceClient();

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> CheckForJobs(ProcessJobState e)
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
                                    InstanceType = "m3.xlarge"
                                }
                            }
            };

            var coreFleet = new InstanceFleetConfig
            {
                InstanceFleetType = "CORE",
                TargetOnDemandCapacity = 1,
                InstanceTypeConfigs = new List<InstanceTypeConfig>{
                                new InstanceTypeConfig{
                                    InstanceType = "m3.xlarge"
                                }
                            }
            };

            var taskFleet = new InstanceFleetConfig
            {
                InstanceFleetType = "TASK",
                TargetSpotCapacity = 2,
                InstanceTypeConfigs = new List<InstanceTypeConfig>{
                                new InstanceTypeConfig{
                                    InstanceType = "m3.xlarge"
                                }
                            }
            };

            var runResp = await emr.RunJobFlowAsync(
                new RunJobFlowRequest
                {
                    AmiVersion = "emr-5.2.0",
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

    }

    public class ProcessJobState
    {

        public string ClusterServiceRole { get; set; }
        public string ClusterJobFlowRole { get; set; }
        public string JobFlowId { get; set; }

    }
}
