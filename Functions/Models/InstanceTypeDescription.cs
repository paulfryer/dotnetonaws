namespace Functions
{
    public class InstanceTypeDescription
    {
        public string Code { get; set; }
        public decimal Memory { get; set; }
        public decimal ECU { get; set; }
        public int CPU { get; set; }
        public int MaxIPs { get; set; }

        public string Family
        {
            get
            {
                return Code.Substring(0, Code.IndexOf('.') - 1);
            }
        }

        public string Generation
        {
            get { return Code.Substring(Code.IndexOf('.') - 1, 1); }
        }

        public string Size
        {
            get
            {
                return Code.Substring(Code.IndexOf('.') + 1, Code.Length - Code.IndexOf('.') - 1);
            }
        }

    }


}
