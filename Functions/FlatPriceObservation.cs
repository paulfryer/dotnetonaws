using Amazon.DynamoDBv2.DataModel;
using System;

namespace Functions
{
    [DynamoDBTable("SpotPrice")]
    public class FlatPriceObservation {

        public FlatPriceObservation()
        {

        }

        public FlatPriceObservation(PriceObservation o)
        {
            AR = o.AvailabilityZone.Area;
            RE = o.AvailabilityZone.Region;
            RI = o.AvailabilityZone.RegionInstance;
            AZ = o.AvailabilityZone.AZ;
            FA = o.InstanceType.Family;
            GE = o.InstanceType.Generation;
            SI = o.InstanceType.Size;
            PR = o.Product;            
            PU = o.Price;
            PC = o.PricePerCPU;
            PE = o.PricePerECU;
            PM = o.PricePerGB;
            LT = o.Timestamp;
            IT = DateTime.UtcNow;
            PK = "PR|AR|RE|RI|FA|GE|SI|AZ";
            SK = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}", PR, AR, RE, RI, FA, GE, SI, AZ);
        }

        public string PK { get; set; }
        public string SK { get; set; }
        public string AR { get; set; }
        public string RE { get; set; }
        public string RI { get; set; }
        public string AZ { get; set; }
        public string FA { get; set; }
        public string GE { get; set; }
        public string SI { get; set; }
        public string PR { get; set; }
        public DateTime LT { get; set; }
        public decimal PU { get; set; }
        public decimal PC { get; set; }
        public decimal PE { get; set; }
        public decimal PM { get; set; }
        public DateTime IT { get; set; }
    };


}
