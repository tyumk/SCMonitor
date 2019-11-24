using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SCMonitor.Models;
using System.Web.Hosting;

namespace SCMonitor.Controllers
{
    public class MonitorController : Controller
    {
        // GET: Monitor
        public ActionResult Index()
        {
            MonitorModel model = new MonitorModel(HostingEnvironment.MapPath(Common.JSON_FILE_VPATH));
            return View(model);
        }

        public ActionResult GetTablePartial()
        {
            MonitorModel model = new MonitorModel(HostingEnvironment.MapPath(Common.JSON_FILE_VPATH));
            return PartialView("_Table", model);
        }
    }
}