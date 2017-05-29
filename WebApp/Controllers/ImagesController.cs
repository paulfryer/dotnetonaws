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
    public class ImagesController : Controller
    {
        
        IAmazonRekognition rekognitionClient;

        public ImagesController(IAmazonRekognition rekognitionClient)
        {
            this.rekognitionClient = rekognitionClient;
        }

        [HttpPost]
        [Route("detect")]
        public async Task<IActionResult> Index()
        {
            var image = new Image
            {
                Bytes = ToMemoryStream(Request.Body)
            };

            var faceDetectionTask = rekognitionClient.DetectFacesAsync(new DetectFacesRequest
            {
                Image = image
            });

            var labelDetectionTask = rekognitionClient.DetectLabelsAsync(new DetectLabelsRequest
            {
                Image = image
            });

            var moderationLableDetectionTask = rekognitionClient.DetectModerationLabelsAsync(new DetectModerationLabelsRequest
            {
                Image = image
            });

            await Task.WhenAll(faceDetectionTask, labelDetectionTask, moderationLableDetectionTask);

            var result = new
            {
                FaceDetails = faceDetectionTask.Result.FaceDetails,
                Labels = labelDetectionTask.Result.Labels,
                ModerationLabels = moderationLableDetectionTask.Result.ModerationLabels
            }; 

            return new ContentResult
            {
                ContentType = "application/json",
                Content = JsonConvert.SerializeObject(result),
                StatusCode = 200
            };

        }

        private MemoryStream ToMemoryStream(Stream stream) {
            var seekableStream = new MemoryStream();
            stream.CopyTo(seekableStream);
            seekableStream.Position = 0;
            return seekableStream;
        }
    }
}
