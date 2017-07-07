using System;
using System.Collections.Generic;

namespace WebApp.Models
{


    public class RespObj
    {
        public string PA { get; set; }
        public Dictionary<string, string> NM { get; set; }
        public List<dynamic> IT { get; set; }
    }

    public class StatObj
    {
        public string NA { get; set; }
        public string CO { get; set; }
        public List<Stat> ST { get; set; }
    }

    public class Stat
    {
        public decimal AV { get; set; }
        public DateTime TM { get; set; }
    }



}
