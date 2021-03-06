﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using DotNetNuke.Entities.Portals;
using ToSic.Eav;
using ToSic.Eav.BLL;
using ToSic.Eav.DataSources.Caches;

namespace ToSic.SexyContent.Internal
{
    internal class AppManagement
    {
        /// <summary>
        /// Returns all Apps for the current zone
        /// </summary>
        /// <param name="includeDefaultApp"></param>
        /// <returns></returns>
        public static List<App> GetApps(int zoneId, bool includeDefaultApp, PortalSettings ownerPS)
        {
            var eavApps = ((BaseCache)DataSource.GetCache(zoneId, null)).ZoneApps[zoneId].Apps;
            var sexyApps = eavApps.Select<KeyValuePair<int, string>, App>(eavApp => new App(zoneId, eavApp.Key, ownerPS));

            if (!includeDefaultApp)
                sexyApps = sexyApps.Where(a => a.Name != Constants.ContentAppName);

            return sexyApps.OrderBy(a => a.Name).ToList();
        }

        /// <summary>
        /// Create app-describing entity for configuration and add Settings and Resources Content Type
        /// </summary>
        /// <param name="zoneId"></param>
        /// <param name="appId"></param>
        internal static void EnsureAppIsConfigured(int zoneId, int appId, string appName = null)
        {
            var appMetaData = DataSource.GetMetaDataSource(zoneId, appId).GetAssignedEntities(ContentTypeHelpers.AssignmentObjectTypeIDSexyContentApp, appId, Settings.AttributeSetStaticNameApps).FirstOrDefault();
            var appResources = DataSource.GetMetaDataSource(zoneId, appId).GetAssignedEntities(ContentTypeHelpers.AssignmentObjectTypeIDSexyContentApp, appId, Settings.AttributeSetStaticNameAppResources).FirstOrDefault();
            var appSettings = DataSource.GetMetaDataSource(zoneId, appId).GetAssignedEntities(ContentTypeHelpers.AssignmentObjectTypeIDSexyContentApp, appId, Settings.AttributeSetStaticNameAppSettings).FirstOrDefault();

            // Get appName from cache
            var eavAppName = ((BaseCache)DataSource.GetCache(zoneId, null)).ZoneApps[zoneId].Apps[appId];

            if (eavAppName == Constants.DefaultAppName)
                return;

            var EavContext = EavDataController.Instance(zoneId, appId);

            if (appMetaData == null)
            {
                // Add app-describing entity
                var appAttributeSet = EavContext.AttribSet.GetAttributeSet(Settings.AttributeSetStaticNameApps).AttributeSetID;
                var values = new OrderedDictionary
                {
                    {"DisplayName", String.IsNullOrEmpty(appName) ? eavAppName : appName },
                    {"Folder", String.IsNullOrEmpty(appName) ? eavAppName : RemoveIllegalCharsFromPath(appName) },
                    {"AllowTokenTemplates", "False"},
                    {"AllowRazorTemplates", "False"},
                    {"Version", "00.00.01"},
                    {"OriginalId", ""}
                };
                EavContext.Entities.AddEntity(appAttributeSet, values, null, appId, ContentTypeHelpers.AssignmentObjectTypeIDSexyContentApp);
            }

            if (appSettings == null)
            {

                AttributeSet settingsAttributeSet;
                // Add new (empty) ContentType for Settings
                if (!EavContext.AttribSet.AttributeSetExists(Settings.AttributeSetStaticNameAppSettings, appId))
                    settingsAttributeSet = EavContext.AttribSet.AddAttributeSet(Settings.AttributeSetStaticNameAppSettings,
                        "Stores settings for an app", Settings.AttributeSetStaticNameAppSettings, Settings.AttributeSetScopeApps);
                else
                    settingsAttributeSet = EavContext.AttribSet.GetAttributeSet(Settings.AttributeSetStaticNameAppSettings);

                DataSource.GetCache(zoneId, appId).PurgeCache(zoneId, appId);
                EavContext.Entities.AddEntity(settingsAttributeSet, new OrderedDictionary(), null, appId, ContentTypeHelpers.AssignmentObjectTypeIDSexyContentApp);
            }

            if (appResources == null)
            {
                AttributeSet resourcesAttributeSet;

                // Add new (empty) ContentType for Resources
                if (!EavContext.AttribSet.AttributeSetExists(Settings.AttributeSetStaticNameAppResources, appId))
                    resourcesAttributeSet = EavContext.AttribSet.AddAttributeSet(
                        Settings.AttributeSetStaticNameAppResources, "Stores resources like translations for an app",
                        Settings.AttributeSetStaticNameAppResources, Settings.AttributeSetScopeApps);
                else
                    resourcesAttributeSet = EavContext.AttribSet.GetAttributeSet(Settings.AttributeSetStaticNameAppResources);

                DataSource.GetCache(zoneId, appId).PurgeCache(zoneId, appId);
                EavContext.Entities.AddEntity(resourcesAttributeSet, new OrderedDictionary(), null, appId, ContentTypeHelpers.AssignmentObjectTypeIDSexyContentApp);
            }

            if (appMetaData == null || appSettings == null || appResources == null)
                DataSource.GetCache(zoneId, appId).PurgeCache(zoneId, appId);
        }

        /// <summary>
        /// Will create a new app in the system and initialize the basic settings incl. the 
        /// app-definition
        /// </summary>
        /// <param name="zoneId"></param>
        /// <param name="appName"></param>
        /// <param name="ownerPS"></param>
        /// <returns></returns>
        internal static App AddBrandNewApp(int zoneId, string appName, PortalSettings ownerPS)
        {
            if (appName == Constants.ContentAppName || appName == "Default" || String.IsNullOrEmpty(appName) || !Regex.IsMatch(appName, "^[0-9A-Za-z -_]+$"))
                throw new ArgumentOutOfRangeException("appName '" + appName + "' not allowed");

            // Adding app to EAV
            var eavContext = EavDataController.Instance(zoneId, AppHelpers.GetDefaultAppId(zoneId));
            var app = eavContext.App.AddApp(Guid.NewGuid().ToString());
            eavContext.SqlDb.SaveChanges();

            EnsureAppIsConfigured(zoneId, app.AppID, appName);

            return new App(zoneId, app.AppID, ownerPS );
        }

        internal static void RemoveApp(int zoneId, int appId, PortalSettings ps, int userId)
        {
            if (zoneId != ZoneHelpers.GetZoneID(ps.PortalId))
                throw new Exception("This app does not belong to portal " + ps.PortalId);

            //var sexy = new SxcInstance(zoneId, appId);// 2016-03-26 2dm this used to have a third parameter false = don't enable caching, which hasn't been respected for a while; removed it
            var eavContext = EavDataController.Instance(zoneId, appId);

            if (appId != eavContext.AppId)  // this only happens if there is some kind of id-fallback
                throw new Exception("An app can only be removed inside of it's own context.");

            if (appId == AppHelpers.GetDefaultAppId(zoneId))
                throw new Exception("The default app of a zone cannot be removed.");

            var sexyApp = new App(zoneId, appId, ps);

            // Delete folder
            if (!String.IsNullOrEmpty(sexyApp.Folder) && Directory.Exists(sexyApp.PhysicalPath))
                Directory.Delete(sexyApp.PhysicalPath, true);

            // Delete the app
            eavContext.App.DeleteApp(appId);
        }


        internal static string RemoveIllegalCharsFromPath(string path)
        {
            var regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(path, "");
        }
    }
}