using Amazon.Runtime.Internal.Auth;
using Amazon.XRay.Recorder.Handlers.System.Net;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net.Http;
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

        public async Task<ActionResult> Databases()
        {
            var tags = await new Amazon.EC2.AmazonEC2Client().DescribeTagsAsync();
            var restApiAddressTag = tags.Tags.FirstOrDefault(T => T.Key == "RESTAPIAddress");
            var serverAddress = new UriBuilder(restApiAddressTag.Value);
            serverAddress.Path = "Database";

            var client = new HttpClient(new HttpClientXRayTracingHandler(new HttpClientHandler()));
            var databasesResponse = await client.GetAsync(serverAddress.Uri);
            var databases = System.Text.Json.JsonSerializer.Deserialize<IEnumerable<string>>(await databasesResponse.Content.ReadAsStringAsync());
            return View(databases);
        }
    }
}