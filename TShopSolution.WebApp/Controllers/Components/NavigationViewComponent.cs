﻿using System.Threading.Tasks;
using TShopSolution.ApiIntegration.Interface;
using TShopSolution.Utilities.Constant;
using TShopSolution.WebApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TShopSolution.WebApp.Controllers.Components
{
    public class NavigationViewComponent : ViewComponent
    {
        private readonly ILanguageApiClient _languageApiClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NavigationViewComponent(ILanguageApiClient languageApiClient,
            IHttpContextAccessor httpContextAccessor)
        {
            _languageApiClient = languageApiClient;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var resultLanguage = await _languageApiClient.GetAll();
            var sessionDefaultLanguageId = _httpContextAccessor.HttpContext.Session.GetString(SystemConstant.AppSettings.DefaultLanguageId);
            var listLanguages = resultLanguage.ResultObject;
            var index = listLanguages.FindIndex(x => x.IsDefault);
            var item = listLanguages[index];

            listLanguages[index] = listLanguages[0];
            listLanguages[0] = item;

            var navigation = new NavigationViewModel()
            {
                CurrentLanguageId = sessionDefaultLanguageId,
                Languages = listLanguages,
            };

            return View("Default", navigation);
        }
    }
}