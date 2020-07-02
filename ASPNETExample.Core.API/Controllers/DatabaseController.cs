using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ASPNETExample.Core.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DatabaseController : ControllerBase
    {
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(ILogger<DatabaseController> logger)
        {
            _logger = logger;
        }

        class DatabaseConnectionInfo
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Engine { get; set; }
            public string Host { get; set; }
        }

        [HttpGet]
        public async Task<IEnumerable<string>> Get()
        {
            var tags = await new Amazon.EC2.AmazonEC2Client().DescribeTagsAsync();
            var passwordArnTag = tags.Tags.FirstOrDefault(T => T.Key == "DBSecretArn");
            var password = await new Amazon.SecretsManager.AmazonSecretsManagerClient().GetSecretValueAsync(new Amazon.SecretsManager.Model.GetSecretValueRequest
            {
                SecretId = passwordArnTag.Value
            });

            var secretString = password.SecretString;
            var dbInfo = System.Text.Json.JsonSerializer.Deserialize<DatabaseConnectionInfo>(
                secretString, 
                new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var databases = new List<string>();
            var connectionStringBuilder = new System.Data.SqlClient.SqlConnectionStringBuilder();
            connectionStringBuilder.DataSource = dbInfo.Host;
            connectionStringBuilder.UserID = dbInfo.Username;
            connectionStringBuilder.Password = dbInfo.Password;
            connectionStringBuilder.InitialCatalog = "master";
            using (var sqlConnection = new System.Data.SqlClient.SqlConnection(connectionStringBuilder.ConnectionString))
            using (var sqlCommand = new Amazon.XRay.Recorder.Handlers.SqlServer.TraceableSqlCommand("SELECT name from sys.databases", sqlConnection, true))
            {
                sqlCommand.Connection.Open();
                using (var reader = sqlCommand.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        databases.Add(reader.GetString(0));
                    }
                }
            }
            return databases;
        }
    }
}
