using System;
using System.Net;
using System.Net.Mail;
using System.Web.Mvc; 

namespace ELearning.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (Session["UserName"] != null)
            {
                return RedirectToAction("Index", "Story");
            }

            return View();
        }

        public ActionResult About()
        {
            if (Session["UserName"] != null)
            {
                return RedirectToAction("Index", "Story");
            }

            return View();
        }
         
    }
}