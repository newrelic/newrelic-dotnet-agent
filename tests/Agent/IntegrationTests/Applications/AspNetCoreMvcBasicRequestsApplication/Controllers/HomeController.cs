using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreMvcBasicRequestsApplication.Models;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreMvcBasicRequestsApplication.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Query(string data)
        {
            return View();
        }

        public void ThrowException()
        {
            throw new Exception("ExceptionMessage");
        }
    }
}
