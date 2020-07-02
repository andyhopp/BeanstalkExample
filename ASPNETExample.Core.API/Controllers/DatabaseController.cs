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
    public partial class DatabaseController : ControllerBase
    {
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseConnectionInfo ConnectionInfo { get; }

        public DatabaseController(ILogger<DatabaseController> logger, DatabaseConnectionInfo connectionInfo)
        {
            _logger = logger;
            ConnectionInfo = connectionInfo;
        }

        [HttpGet]
        public async Task<IEnumerable<string>> Get()
        {
            var databases = new List<string>();
            var connectionStringBuilder = new System.Data.SqlClient.SqlConnectionStringBuilder();
            connectionStringBuilder.DataSource = ConnectionInfo.Host;
            connectionStringBuilder.UserID = ConnectionInfo.Username;
            connectionStringBuilder.Password = ConnectionInfo.Password;
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
