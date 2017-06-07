using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Amazon.S3;
using Amazon.KeyManagementService;
using Amazon.DynamoDBv2;
using Amazon.Rekognition;
using Amazon.EC2;
using Amazon.Athena;

namespace WebApp
{
    public class Startup
    {
        public const string AppS3BucketKey = "AppS3Bucket";

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public static IConfigurationRoot Configuration { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            // Pull in any SDK configuration from Configuration object
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());

            // Add S3 to the ASP.NET Core dependency injection framework.
            services.AddAWSService<IAmazonS3>();
            services.AddAWSService<IAmazonDynamoDB>();
            services.AddAWSService<IAmazonKeyManagementService>();
            services.AddAWSService<IAmazonRekognition>();
            services.AddAWSService<IAmazonEC2>();

            services.AddAWSService<IAmazonAthena>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddLambdaLogger(Configuration.GetLambdaLoggerOptions());
            app.UseMvc();
        }
    }
}
