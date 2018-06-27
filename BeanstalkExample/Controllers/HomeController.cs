using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace BeanstalkExample.Controllers
{
    public class HomeController : Controller
    {
        public async Task<ActionResult> ListBuckets()
        {
            var client = new Amazon.S3.AmazonS3Client();
            var list = await client.ListBucketsAsync();
            return View(list.Buckets);
        }
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
    }
}