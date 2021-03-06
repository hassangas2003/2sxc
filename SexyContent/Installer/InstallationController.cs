﻿using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using DotNetNuke.Web.Client.ClientResourceManagement;
using ToSic.Eav;

namespace ToSic.SexyContent.Installer
{
    internal class InstallationController
    {
        public bool SaveUnimportantDetails = true;

        private readonly Logger _logger;

        /// <summary>
        /// This static initializer will do a one-time check to see if everything is ready,
        /// so subsequent access to this property will not need to do anything any more
        /// </summary>
        static InstallationController()
        {
            UpdateUpgradeCompleteStatus();
        }

        private static void UpdateUpgradeCompleteStatus()
        {
            UpgradeComplete = new InstallationController().IsUpgradeComplete(Settings.Installation.LastVersionWithServerChanges /* Settings.ModuleVersion */, "- static check");
        }

        /// <summary>
        /// Instance initializers...
        /// </summary>
        public InstallationController()
        {
            _logger = new Logger(SaveUnimportantDetails);
        }

        internal string UpgradeModule(string version)
        {
            _logger.LogStep(version, "UpgradeModule starting", false);

            // Configure Unity / eav, etc.
            Settings.EnsureSystemIsInitialized();
            // new UnityConfig().Configure();

            // Abort upgrade if it's already done - if version is 01.00.00, the module has probably been uninstalled - continue in this case.
            if (version != "01.00.00" && IsUpgradeComplete(version, "- Check on Start UpgradeModule"))
            {
                _logger.LogStep(version, "Apparently trying to update this version, but this versions upgrade is apparently compeleted, will abort");
                throw new Exception("2sxc upgrade for version " + version +
                                    " started, but it looks like the upgrade for this version is already complete. Aborting upgrade.");
            }
            _logger.LogStep(version, "version / upgrade-complete test passed");

            if (IsUpgradeRunning)
            {
                _logger.LogStep(version, "Apparently upgrade is running, will abort");
                throw new Exception("2sxc upgrade for version " + version +
                                    " started, but the upgrade is already running. Aborting upgrade.");
            }
            _logger.LogStep(version, "is-upgrade-running test passed");

            IsUpgradeRunning = true;
            _logger.LogStep(version, "----- Upgrade to " + version + " started -----");

            try
            {

                switch (version)
                {
                    case "01.00.00": // Make sure that log folder empty on new installations (could happen if 2sxc was already installed on a system)
                        MaybeResetUpgradeLogsToStartAgainFromV1();
                        break;
                    case "05.05.00":
                        new V5(version, _logger).Version050500();
                        break;
                    case "06.06.00":
                    case "06.06.04":
                        new V6(version, _logger).EnsurePipelineDesignerAttributeSets();
                        break;
                    case "07.00.00":
                        new V7(version, _logger).Version070000();
                        break;
                    case "07.00.03":
                        new V7(version, _logger).Version070003();
                        break;
                    case "07.02.00":
                        new V7(version, _logger).Version070200();
                        break;
                    case "07.02.02":
                        // Make sure upgrades FROM BETWEEN 07.00.00 and 07.02.02 do not run again (create log files for each of them)
                        _logger.LogVersionCompletedToPreventRerunningTheUpgrade("07.00.00", false);
                        _logger.LogVersionCompletedToPreventRerunningTheUpgrade("07.00.03", false);
                        _logger.LogVersionCompletedToPreventRerunningTheUpgrade("07.02.00", false);
                        break;
                    case "07.03.01":
                        new V6(version, _logger).EnsurePipelineDesignerAttributeSets(); // Need to ensure this again because of upgrade problems
                        break;
                    case "07.03.03":
                        new V7(version, _logger).Version070303();
                        break;
                    case "08.00.02":
                        new V8(version, _logger).Version080002();
                        break;
                    case "08.00.04":
                        new V8(version, _logger).Version080004();
                        break;
                    case "08.00.07":
                        new V8(version, _logger).Version080007();
                        break;
                    case "08.01.00":
                        new V8(version, _logger).Version080100();
                        break;
                    case "08.03.00":
                        break;
                    case "08.03.02":
                        new V8(version, _logger).Version080302();
                        break;
                    case "08.03.03":
                        new V8(version, _logger).Version080303();
                        break;
                    case "08.03.05":
                        Helpers.ImportXmlSchemaOfVersion("08.03.05", false);
                        break;
                    case "08.04.00":
                        Helpers.ImportXmlSchemaOfVersion("08.04.00", false);
                        break;
                    case "08.04.03":
                        Helpers.ImportXmlSchemaOfVersion("08.04.03", false);
                        break;
                    case "08.04.05":
                        Helpers.ImportXmlSchemaOfVersion("08.04.03", false); // this is correct, RE-RUN 08.04.03 as there was a problem in that version number before
                        break;
                    case "08.05.00":
                        Helpers.ImportXmlSchemaOfVersion("08.05.00", false);
                        break;
                    case "08.05.01":
                        new V8(version, _logger).Version080501();
                        break;
                    case "08.05.02":
                        Helpers.ImportXmlSchemaOfVersion("08.05.02", false);
                        break;
                    case "08.05.03":
                        Helpers.ImportXmlSchemaOfVersion("08.05.03", false);
                        break;
                    case "08.05.05":
                        Helpers.ImportXmlSchemaOfVersion("08.05.05", false);
                        break;

                        // warning!!! when you add a new case, make sure you upgrade the version number on Settings.Installation.LastVersionWithServerChanges!!!
                }
                _logger.LogStep(version, "version-list check / switch done", false);

                // Increase ClientDependency version upon each upgrade (System and all Portals)
                // prevents browsers caching old JS and CSS files for editing, which could cause several errors
                // only set this on the last upgraded version, to prevent crazy updating the client-resource-cache while upgrading
                if (version == Settings.Installation.UpgradeVersionList.Last())
                {
                    _logger.LogStep(version, "ClientResourceManager- seems to be last item in version-list, will clear");
                    ClientResourceManager.UpdateVersion();
                    _logger.LogStep(version, "ClientResourceManager- done clearing");

                    UpdateUpgradeCompleteStatus();
                    _logger.LogStep(version, "updated upgrade-complete status");
                }

                _logger.LogVersionCompletedToPreventRerunningTheUpgrade(version);
                _logger.LogStep(version, "----- Upgrade to " + version + " completed -----");

            }
            catch (Exception e)
            {
                _logger.LogStep(version, "Upgrade failed - " + e.Message);
                throw;
            }
            finally
            {
                IsUpgradeRunning = false;
            }
            _logger.LogStep(version, "UpgradeModule done / returning");
            return version;
        }

        internal void MaybeResetUpgradeLogsToStartAgainFromV1()
        {
            _logger.LogStep("", "Maybe reset logs start");
            // this condition only applies, if 2sxc upgrade 7 didn't happen yet
            if (DataSource.GetCache(Constants.DefaultZoneId, Constants.MetaDataAppId)
                    .GetContentType("2SexyContent-Template") != null) return;

            _logger.LogStep("", "Will reset all logs now");
            _logger.DeleteAllLogFiles();
            _logger.LogStep("", "Maybe Reset logs done");
        }


        internal void FinishAbortedUpgrade()
        {
            _logger.LogStep("", "FinishAbortedUpgrade starting", false);
            _logger.LogStep("", "Will handle " + Settings.Installation.UpgradeVersionList.Length + " versions");
            // Run upgrade again for all versions that do not have a corresponding logfile
            foreach (var upgradeVersion in Settings.Installation.UpgradeVersionList)
            {
                var complete = IsUpgradeComplete(upgradeVersion, "- check for FinishAbortedUpgrade");
                _logger.LogStep("", "Status for version " + upgradeVersion + " is " + complete);
                if (!complete)
                    UpgradeModule(upgradeVersion);
            }

            _logger.LogStep("", "FinishAbortedUpgrade done", false);

            //_logger.SaveDetailedLog();
            // Restart application
            HttpRuntime.UnloadAppDomain();
        }




        internal string CheckUpgradeMessage(bool isSuperUser)
        {
            // Upgrade success check - show message if upgrade did not run successfully
            if (UpgradeComplete) return null;

            return IsUpgradeRunning
                ? "It looks like a 2sxc upgrade is currently running.Please wait for the operation to complete(the upgrade may take a few minutes)."
                : isSuperUser
                    ? "Module upgrade did not complete (<a href='http://2sxc.org/en/help/tag/install' target='_blank'>read more</a>). Click to complete: <br><a class='dnnPrimaryAction' onclick='$2sxc.system.finishUpgrade(this)'>complete upgrade</a>"
                    : "Module upgrade did not complete successfully. Please login as host user to finish the upgrade.";
        }

        #region Status Stuff
        internal static bool UpgradeComplete;

        internal bool IsUpgradeComplete(string version, string note = "")
        {
            _logger.LogStep(version, "IsUgradeComplete checking " + note, false);
            var logFilePath = HostingEnvironment.MapPath(Settings.Installation.LogDirectory + version + ".resources");
            var complete = File.Exists(logFilePath);
            _logger.LogStep(version, "IsUgradeComplete: " + complete, false);
            return complete;
        }

        // cache the status
        private static bool? _running;
        /// <summary>
        /// Set / Check if it's running, by storing the static info but also creating/releasing a lock-file
        /// We need the lock file in case another system would try to read the status, which doesn't share
        /// this running static instance
        /// </summary>
        internal bool IsUpgradeRunning
        {
            get
            {
                return _running ?? (_running = new Lock().IsSet).Value;
            }
            private set
            {
                try
                {
                    _logger.LogStep("", "set upgrade running - " + value);

                    if (value)
                        new Lock().Set();
                    else
                        new Lock().Release();
                    _logger.LogStep("", "set upgrade running - " + value + " - done");
                }
                catch
                {
                    _logger.LogStep("", "set upgrade running - " + value + " - error!");
                }
                finally
                {
                    _running = value;
                }
            }
        }
        #endregion


    }
}