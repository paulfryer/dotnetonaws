namespace Functions
{
    public class AvailabilityZone
    {

        public AvailabilityZone(string availabilityZone)
        {
            var azParts = availabilityZone.Split('-');
            Area = azParts[0];
            Region = azParts[1];
            RegionInstance = azParts[2].Substring(0, 1);
            AZ = azParts[2].Substring(1, 1);
        }

        public string Area { get; private set; }
        public string Region { get; private set; }
        public string AZ { get; private set; }
        public string RegionInstance { get; private set; }
    }


}
