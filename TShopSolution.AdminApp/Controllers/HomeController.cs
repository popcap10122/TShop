﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TShopSolution.AdminApp.Models;
using Microsoft.AspNetCore.Authorization;
using TShopSolution.Utilities.Constant;
using Microsoft.AspNetCore.Http;

namespace TShopSolution.AdminApp.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var user = User.Identity.Name;
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult Language(NavigationViewModel navigationViewModel)
        {
            HttpContext.Session.SetString(SystemConstant.AppSettings.DefaultLanguageId, navigationViewModel.CurrentLanguageId);

            return Redirect(navigationViewModel.ReturnURL);
        }
    }
}