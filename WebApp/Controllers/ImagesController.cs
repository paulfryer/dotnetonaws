using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.EC2;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using System.IO;

namespace WebApp.Controllers
{ 
    [Route("api/[controller]")]
    public class ImagesController : Controller {


        IAmazonRekognition rekognitionClient;

        public ImagesController(IAmazonRekognition rekognitionClient)
        {
            this.rekognitionClient = rekognitionClient;
        }

       [HttpPost]
       [Route("faces")]
        public async Task<IActionResult> Index()
        {

            var seekableStream = new MemoryStream();
            await this.Request.Body.CopyToAsync(seekableStream);
            seekableStream.Position = 0;

            var detectFacesRequest = new DetectFacesRequest
            {
                Image = new Image {
                Bytes = seekableStream
                }
            };
            var result = await rekognitionClient.DetectFacesAsync(detectFacesRequest);

            return new ContentResult
            {
                ContentType = "application/json",
                Content = JsonConvert.SerializeObject(result.FaceDetails),
                StatusCode = 200
            };

        }

        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}
