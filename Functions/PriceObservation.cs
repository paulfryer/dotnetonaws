using Amazon.EC2.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Functions
{
    public class PriceObservation
    {

        public InstanceTypeDescription InstanceType { get; private set; }
        public AvailabilityZone AvailabilityZone { get; private set; }

        public PriceObservation(SpotPrice spotPrice, InstanceTypeDescription instanceType)
        {
            InstanceType = instanceType;
            AvailabilityZone = new AvailabilityZone(spotPrice.AvailabilityZone);
            Price = Convert.ToDecimal(String.Format("{0:0.0000}", Convert.ToDecimal(spotPrice.Price)));
            Timestamp = spotPrice.Timestamp.ToUniversalTime();
            Product = spotPrice.ProductDescription;

            PricePerCPU = Decimal.Parse(String.Format("{0:0.00000}", Price / InstanceType.CPU));
            PricePerECU = Decimal.Parse(String.Format("{0:0.00000}", Price / InstanceType.ECU));
            PricePerGB = Decimal.Parse(String.Format("{0:0.00000}", Price / InstanceType.Memory));

        }

        public DateTime Timestamp { get; private set; }
        public decimal Price { get; private set; }
        public string Product { get; private set; }

        public decimal PricePerCPU { get; private set; }
        public decimal PricePerECU { get; private set; }
        public decimal PricePerGB { get; private set; }

        public string ToJSON() {
            var miniObservation = new FlatPriceObservation(this);
            return JsonConvert.SerializeObject(miniObservation);
        }

        public string ToCSV()
        {
            var parts = new List<string>
            {
                AvailabilityZone.Area,
                AvailabilityZone.Region,
                AvailabilityZone.RegionInstance,
                AvailabilityZone.AZ,
                InstanceType.Family,
                InstanceType.Generation,
                InstanceType.Size,
                Product,
                Convert.ToString(Timestamp),
                Convert.ToString(Price),
                Convert.ToString(PricePerCPU),
                Convert.ToString(PricePerECU),
                Convert.ToString(PricePerGB)
            };
            return string.Join(",", parts);
        }
    }


}
