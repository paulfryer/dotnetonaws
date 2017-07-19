using System;

namespace Functions
{
    public class Choice 
    {
        public string Variable { get; set; }
        public string Operator { get; set; }
        public object Value { get; set; }
        public Type Next { get; set; }
    }


}
