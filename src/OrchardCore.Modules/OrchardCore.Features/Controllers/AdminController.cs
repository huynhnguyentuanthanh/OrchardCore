using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Extensions;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor;
using OrchardCore.Features.Models;
using OrchardCore.Features.Services;
using OrchardCore.Features.ViewModels;
using OrchardCore.Mvc.ActionConstraints;

namespace OrchardCore.Features.Controllers
{
    [Admin]
    public class AdminController : Controller
    {
        private readonly IModuleService _moduleService;
        private readonly IExtensionManager _extensionManager;
        private readonly IShellFeaturesManager _shellFeaturesManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly ShellSettings _shellSettings;
        private readonly INotifier _notifier;

        public AdminController(
            IModuleService moduleService,
            IExtensionManager extensionManager,
            IHtmlLocalizer<AdminController> localizer,
            IShellDescriptorManager shellDescriptorManager,
            IShellFeaturesManager shellFeaturesManager,
            IAuthorizationService authorizationService,
            ShellSettings shellSettings,
            INotifier notifier)
        {
            _moduleService = moduleService;
            _extensionManager = extensionManager;
            _shellFeaturesManager = shellFeaturesManager;
            _authorizationService = authorizationService;
            _shellSettings = shellSettings;
            _notifier = notifier;

            T = localizer;
        }

        public IHtmlLocalizer T { get; }

        public async Task<ActionResult> Features()
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageFeatures)) // , T["Not allowed to manage features."]
            {
                return Unauthorized();
            }

            var enabledFeatures = await _shellFeaturesManager.GetEnabledFeaturesAsync();
            var alwaysEnabledFeatures = await _shellFeaturesManager.GetAlwaysEnabledFeaturesAsync();

            var moduleFeatures = new List<ModuleFeature>();
            foreach (var moduleFeatureInfo in _extensionManager
                .GetFeatures()
                .Where(f => !f.Extension.IsTheme() && FeatureIsAllowed(f)))
            {
                var dependentFeatures = _extensionManager.GetDependentFeatures(moduleFeatureInfo.Id);
                var featureDependencies = _extensionManager.GetFeatureDependencies(moduleFeatureInfo.Id);

                var moduleFeature = new ModuleFeature
                {
                    Descriptor = moduleFeatureInfo,
                    IsEnabled = enabledFeatures.Contains(moduleFeatureInfo),
                    IsAlwaysEnabled = alwaysEnabledFeatures.Contains(moduleFeatureInfo),
                    //IsRecentlyInstalled = _moduleService.IsRecentlyInstalled(f.Extension),
                    //NeedsUpdate = featuresThatNeedUpdate.Contains(f.Id),
                    DependentFeatures = dependentFeatures.Where(x => x.Id != moduleFeatureInfo.Id).ToList(),
                    FeatureDependencies = featureDependencies.Where(d => d.Id != moduleFeatureInfo.Id).ToList()
                };

                moduleFeatures.Add(moduleFeature);
            }

            return View(new FeaturesViewModel
            {
                Features = moduleFeatures,
                IsAllowed = FeatureIsAllowed
            });
        }

        [HttpPost]
        [FormValueRequired("submit.BulkAction")]
        public async Task<ActionResult> Features(BulkActionViewModel model, bool? force)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageFeatures))
            {
                return Unauthorized();
            }

            if (model.FeatureIds == null || !model.FeatureIds.Any())
            {
                ModelState.AddModelError("featureIds", T["Please select one or more features."].ToString());
            }

            if (ModelState.IsValid)
            {
                var features = _extensionManager.GetFeatures().Where(f => FeatureIsAllowed(f)).ToList();
                var selectedFeatures = features.Where(f => model.FeatureIds.Contains(f.Id)).ToList();

                await EnableOrDisableFeaturesAsync(selectedFeatures, model.BulkAction, force);
            }

            return RedirectToAction(nameof(Features));
        }

        [HttpPost]
        public async Task<IActionResult> Disable(string id)
        {
            var feature = _extensionManager.GetFeatures().FirstOrDefault(f => FeatureIsAllowed(f) && f.Id == id);

            if (feature == null)
            {
                return NotFound();
            }

            // Generating routes can fail while the tenant is recycled as routes can use services.
            // It could be fixed by waiting for the next request or the end of the current one
            // to actually release the tenant. Right now we render the url before recycling the tenant.

            var nextUrl = Url.Action(nameof(Features));

            await EnableOrDisableFeaturesAsync(new[] { feature }, FeaturesBulkAction.Disable, force: true);

            return Redirect(nextUrl);
        }

        [HttpPost]
        public async Task<IActionResult> Enable(string id)
        {
            var feature = _extensionManager.GetFeatures().FirstOrDefault(f => FeatureIsAllowed(f) && f.Id == id);

            if (feature == null)
            {
                return NotFound();
            }

            // Generating routes can fail while the tenant is recycled as routes can use services.
            // It could be fixed by waiting for the next request or the end of the current one
            // to actually release the tenant. Right now we render the url before recycling the tenant.

            var nextUrl = Url.Action(nameof(Features));

            await EnableOrDisableFeaturesAsync(new[] { feature }, FeaturesBulkAction.Enable, force: true);

            return Redirect(nextUrl);
        }

        /// <summary>
        /// Checks whether the feature is allowed for the current tenant
        /// </summary>
        private bool FeatureIsAllowed(IFeatureInfo feature)
        {
            // TODO: Implement white-list of modules allowed in the shell settings

            // Checks if the feature is only allowed on the Default tenant
            return _shellSettings.Name == ShellHelper.DefaultShellName || !feature.DefaultTenantOnly;
        }

        private async Task EnableOrDisableFeaturesAsync(IEnumerable<IFeatureInfo> features, FeaturesBulkAction action, bool? force)
        {
            switch (action)
            {
                case FeaturesBulkAction.None:
                    break;
                case FeaturesBulkAction.Enable:
                    await _shellFeaturesManager.EnableFeaturesAsync(features, force == true);
                    Notify(features);
                    break;
                case FeaturesBulkAction.Disable:
                    await _shellFeaturesManager.DisableFeaturesAsync(features, force == true);
                    Notify(features, enabled: false);
                    break;
                case FeaturesBulkAction.Toggle:
                    var enabledFeatures = await _shellFeaturesManager.GetEnabledFeaturesAsync();
                    var disabledFeatures = await _shellFeaturesManager.GetDisabledFeaturesAsync();
                    var featuresToEnable = disabledFeatures.Intersect(features);
                    var featuresToDisable = enabledFeatures.Intersect(features);

                    await _shellFeaturesManager.UpdateFeaturesAsync(featuresToDisable, featuresToEnable, force == true);
                    Notify(featuresToEnable);
                    Notify(featuresToDisable, enabled: false);
                    return;
                default:
                    break;
            }
        }

        private void Notify(IEnumerable<IFeatureInfo> features, bool enabled = true)
        {
            foreach (var feature in features)
            {
                _notifier.Success(T["{0} was {1}", feature.Name ?? feature.Id, enabled ? "enabled" : "disabled"]);
            }
        }
    }
}