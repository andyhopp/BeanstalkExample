using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace BeanstalkExample.Controllers
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
    }
}