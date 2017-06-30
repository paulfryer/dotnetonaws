using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.ElasticMapReduce;
using Amazon.ElasticMapReduce.Model;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3;
using System.Net.Http;
using Newtonsoft.Json;

namespace Functions
{
    public class FlickrController
    {
        IAmazonS3 s3 = new AmazonS3Client(Amazon.RegionEndpoint.USWest2);

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> DownloadImages(ImageProcessingState e)
        {

            using (var http = new HttpClient()){


            }


            return e;
        }


		[LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<dynamic> SearchImages(ImageProcessingState e)
		{

            var url = $"https://api.flickr.com/services/rest/?method=flickr.photos.search&api_key={e.APIKey}&tags={e.Tags}&format=json&nojsoncallback=1&page={e.Page}&pageSize={e.PageSize}'";

            var urls = new List<string>();
            using (var http = new HttpClient())
			{
                var resp = await http.GetAsync(url);
                var json = await resp.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<IEnumerable<dynamic>>(json);

                Console.Write("got response");

                /*
                var photos = obj.photos.photo.ToArray();
                foreach (var photo in photos)
                    urls.Add("https://farm" + photo.farm + ".staticflickr.com/" + photo.server + "/" + photo.id + "_" + photo.secret + "_m.jpg");
                    */
			}

			return e;
		}

        public class ImageProcessingState {
            public string APIKey { get; set; }
            public string Tags { get; set; }
            public int Page { get; set; }
            public bool HasMore { get; set; }
            public int PageSize { get; set; }

        }

    }
}
