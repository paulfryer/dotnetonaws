using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Functions
{
    public class ImageNetController
    {

        List<string[]> isARows;

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> TagImage(dynamic @event, ILambdaContext context)
        {


            isARows = File.OpenText("wordnet.is_a.txt").ReadToEnd()
                            .Split('\n')
                            .Select(r => r.Split(' '))
                            .Where(r => r.Length == 2)
                            .ToList();

            var lineage = new Dictionary<string, List<Relationship>>();



            foreach (var child in isARows.Select(r => r[1]).Distinct().Skip(70000).Take(205)) {

    
                
                var ansestry = GetLineage(child);

                //var ansestryString = string.Join("|", ansestry);
               // Console.WriteLine(ansestryString);

                lineage.Add(child, ansestry);
            }

            var lineageString = string.Join("|", lineage.Values);


            throw new NotImplementedException();
        }


        public List<Relationship> GetLineage(string child, List<Relationship> lineage = null, List<string> keys = null)
        {

            if (lineage == null)
                lineage = new List<Relationship>();
            if (keys == null)
                keys = new List<string>();



            var matches = isARows.Where(r => r[1] == child);

            if (matches.Count() == 0)
                return lineage;



            var rel = new Relationship
            {
                Child = child,
                Parents = matches.Select(match => new Relationship
                {
                    Child = match[0],
                    Parents = GetLineage(match[0])
                }).ToList()
            };

            lineage.Add(rel);

            return lineage;

            //return rel;

            /*
            var match = new string[2];

            if (matches.Count() > 1) {
                Console.WriteLine("Child: " + child + " found " + matches.Count() + " times.");
            }
            match = matches.FirstOrDefault();

            if (match == null)
                return lineage;

            var parent = match[0];

            if (lineage == null)
                lineage = new List<string>();
            lineage.Add(parent);

            return GetLineage(parent, lineage);
            */
        }

    }

    public class Relationship {
        public List<Relationship> Parents { get; set; }
        public string Child { get; set; }
    }

 

}
