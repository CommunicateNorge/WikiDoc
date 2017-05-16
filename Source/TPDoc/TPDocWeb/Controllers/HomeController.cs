using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace TPDocWeb.Controllers
{
    /// <summary>
    /// Entry point for sharepoint app
    /// </summary>
    public class HomeController : Controller
    {

        /// <summary>
        /// Entry point for sharepoint app
        /// </summary>
        /// <returns></returns>
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Wiki");
        }
    }
}