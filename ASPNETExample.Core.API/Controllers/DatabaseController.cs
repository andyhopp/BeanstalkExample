using System;
using System.Collections.Generic;
using System.Linq;
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

        [HttpGet]
        public async Task<IEnumerable<string>> Get()
        {
            var tags = await new Amazon.EC2.AmazonEC2Client().DescribeTagsAsync();
            var databaseServerTag = tags.Tags.FirstOrDefault(T => T.Key == "DatabaseAddress");
            var serverAddress = databaseServerTag.Value;
            var passwordArnTag = tags.Tags.FirstOrDefault(T => T.Key == "PasswordArn");
            var password = await new Amazon.SecretsManager.AmazonSecretsManagerClient().GetSecretValueAsync(new Amazon.SecretsManager.Model.GetSecretValueRequest
            {
                SecretId = passwordArnTag.Value
            });

            var databases = new List<string>();
            var connectionStringBuilder = new System.Data.SqlClient.SqlConnectionStringBuilder();
            connectionStringBuilder.DataSource = serverAddress;
            connectionStringBuilder.UserID = "sa";
            connectionStringBuilder.Password = password.SecretString;
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
