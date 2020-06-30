using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace ASPNETExample.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "This is my ASP.NET application. There are many like it, but this one is mine.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "We want to hear from you!";

            return View();
        }

        public ActionResult Databases()
        {
            var databases = new List<string>();
            using (var sqlConnection = new System.Data.SqlClient.SqlConnection("Server=sqldemo.cym2zbbhngsn.us-east-1.rds.amazonaws.com;Initial Catalog=master;User ID=sa;Password=P0rsche1"))
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
            return View(databases);
        }
    }
}