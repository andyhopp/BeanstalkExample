using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASPNETExample.Core.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            RegisterDatabaseInfo(services).Wait();
            services.AddControllers();
            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
        }

        private async Task RegisterDatabaseInfo(IServiceCollection services)
        {
            var tags = await new Amazon.EC2.AmazonEC2Client().DescribeTagsAsync();
            var passwordArnTag = tags.Tags.FirstOrDefault(T => T.Key == "DBSecretArn");
            Console.WriteLine($"DBSecretArn: {passwordArnTag.Value}");
            var password = await new Amazon.SecretsManager.AmazonSecretsManagerClient().GetSecretValueAsync(new Amazon.SecretsManager.Model.GetSecretValueRequest
            {
                SecretId = passwordArnTag.Value
            });

            var secretString = password.SecretString;
            var dbInfo = System.Text.Json.JsonSerializer.Deserialize<DatabaseConnectionInfo>(
                secretString,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

            services.AddSingleton(dbInfo);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseXRay("ASPNETExample-RESTAPI");

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
