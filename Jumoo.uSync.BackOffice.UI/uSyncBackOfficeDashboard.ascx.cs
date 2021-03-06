﻿using Jumoo.uSync.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Jumoo.uSync.BackOffice.UI
{
    public partial class uSyncBackOfficeDashboard : System.Web.UI.UserControl
    {
        protected string TypeString(object type)
        {
            var typeName = type.ToString();
            return typeName.Substring(typeName.LastIndexOf('.')+1);
        }

        protected string ResultIcon(object result)
        {
            var r = (bool)result;

            if (r)
                return "<i class=\"icon-checkbox\"></i>";
            else
                return "<i class=\"icon-checkbox-dotted\"></i>";
        }

        protected string Details(object details)
        {
            string changes = "";
            if (details != null)
            {
                var d = (IEnumerable<uSyncChange>)details;

                foreach(var change in d)
                {
                    changes += string.Format("[{0}] [{1}] Name: {2}  Old: ({3}) New: ({4}) <br/>", change.Change, change.Path, change.Name, change.OldVal, change.NewVal);
                }
            }

            return changes; 
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var licence = new Jumoo.uSync.BackOffice.Licence.GoodwillLicence();
            goodwillLicence.Visible = !licence.IsLicenced();
            
            if (!IsPostBack)
            {
                SetupPage();
                WriteSettings();
            }
        }

        private void WriteSettings()
        {
            var settings = uSyncBackOfficeContext.Instance.Configuration.Settings;

            rbAutoSync.Checked = false;
            rbTarget.Checked = false;
            rbManual.Checked = false;
            rbOther.Checked = false;
            rbSource.Checked = false;

            if (settings.Import == true)
            {
                if (settings.ExportOnSave == true)
                {
                    // import true, export on save true,
                    rbAutoSync.Checked = true;
                }
                else
                {
                    // import true, export on save false
                    rbTarget.Checked = true;
                }
            }
            else
            {
                // import false
                if (settings.ExportAtStartup == false)
                {
                    // import false, export false
                    if (settings.ExportOnSave == true)
                    {
                        // import false, export false, exportonSave true 
                        rbSource.Checked = true;
                    }
                    else
                    {
                        // import false, export, false, export on save false
                        rbManual.Checked = true;
                    }
                }
                else
                {
                    // import false,export true, ?;
                    rbOther.Checked = true;
                }
            }
        }

        private void SetupPage()
        {
            var settings = uSyncBackOfficeContext.Instance.Configuration.Settings;


            uSyncVersionNumber.Text = uSyncBackOfficeContext.Instance.Version;
            uSyncCoreVersion.Text = Jumoo.uSync.Core.uSyncCoreContext.Instance.Version;

            var handlers = uSyncBackOfficeContext.Instance.Handlers;
            uSyncHandlerCount.Text = handlers.Count.ToString(); ;

            foreach (var handler in handlers)
            { 

                var name = handler.Name ;
                var handlerConfig = settings.Handlers.Where(x => x.Name == name)
                    .FirstOrDefault();

                string enabledText = " (enabled) ";
                bool enabled = true;

                if (handlerConfig != null && !handlerConfig.Enabled)
                {
                    enabledText = " (disabled) ";
                    enabled = false; 
                }

                var item = new ListItem(name + enabledText, name, enabled);
                uSyncHandlers.Items.Add(item);
            }

            usyncFolder.Text = settings.Folder;
        }

        protected void btnFullImport_Click(object sender, EventArgs e)
        {
            PerformImport(true);
        }

        protected void btnFullExport_Click(object sender, EventArgs e)
        {
            // events shoudn't fire when you export, 
            // but we are pausing just incase.
            uSyncEvents.Paused = true;

            var folder = uSyncBackOfficeContext.Instance.Configuration.Settings.Folder;
            if (System.IO.Directory.Exists(folder))
                System.IO.Directory.Delete(folder, true);

            var actions = uSyncBackOfficeContext.Instance.ExportAll(folder);
            if (actions.Any())
            {
                uSyncStatus.DataSource = actions;
                uSyncStatus.DataBind();
            }

            ShowResultHeader("Export", "All items have been exported");
            uSyncEvents.Paused = false;

            uSyncActionLogger.SaveActionLog("Export", actions);
        }

        protected void btnSaveSettings_Click(object sender, EventArgs e)
        {
            var settings = uSyncBackOfficeContext.Instance.Configuration.Settings;

            var mode = "no change";
            if (rbAutoSync.Checked == true)
            {
                settings.ExportOnSave = true;
                settings.Import = true;
                settings.ExportAtStartup = false;

                mode = "AutoSync";

            }
            else if (rbTarget.Checked == true)
            {
                settings.ExportOnSave = false;
                settings.Import = true;
                settings.ExportAtStartup = false;

                mode = "Sync Target";
            }
            else if (rbManual.Checked == true)
            {
                settings.ExportOnSave =false;
                settings.Import = false;
                settings.ExportAtStartup = false;

                mode = "Manual";

            }
            else if (rbSource.Checked == true)
            {
                settings.ExportAtStartup = false;
                settings.Import = false;
                settings.ExportOnSave = true;
                mode = "Source";
            }
            uSyncBackOfficeContext.Instance.Configuration.SaveSettings(settings);

            ShowResultHeader("Settings Updated",
                string.Format("Mode = {0} (requires a restart to take effect)", mode));

            WriteSettings();

        }

        protected void btnSyncImport_Click(object sender, EventArgs e)
        {
            // Backup();
            PerformImport(false);
        }

        protected void btnReport_Click(object sender, EventArgs e)
        {
            var changeMessage = "These are the changes if you ran an import now";
            var actions = uSyncBackOfficeContext.Instance.ImportReport(
                uSyncBackOfficeContext.Instance.Configuration.Settings.MappedFolder());

            if (actions.Any())
            {
                changeMessage = string.Format("if you ran an import now: {0} items would be processed and {1} changes would be made",
                    actions.Count(), actions.Count(x => x.Change > Core.ChangeType.NoChange));

                uSyncStatus.DataSource = actions.Where(x => x.Change > Core.ChangeType.NoChange);
                uSyncStatus.DataBind();
            }

            ShowResultHeader("Change Report", changeMessage);
        }

        /*
        private void Backup()
        {
            uSyncEvents.Paused = true;

            var backupFolder = string.Format("~/app_data/uSync/Backups/{0}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            if (System.IO.Directory.Exists(backupFolder))
                System.IO.Directory.Delete(backupFolder, true);

            uSyncBackOfficeContext.Instance.ExportAll(backupFolder);

            uSyncEvents.Paused = false;


        }
        */

        private void PerformImport(bool force)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            uSyncEvents.Paused = true;
            var actions = uSyncBackOfficeContext.Instance.ImportAll(uSyncBackOfficeContext.Instance.Configuration.Settings.Folder, force);
            uSyncEvents.Paused = false;

            sw.Stop();

            ShowResultHeader("Import processed", string.Format("uSync Import Complete: ({0}ms) processed {1} items and made {2} changes",
                    sw.ElapsedMilliseconds, actions.Count(), actions.Where(x => x.Change > Core.ChangeType.NoChange).Count()));

            if (actions.Any())
            {
                uSyncStatus.DataSource = actions.Where(x => x.Change > Core.ChangeType.NoChange);
                uSyncStatus.DataBind();
            }

            uSyncActionLogger.SaveActionLog("Import", actions);

        }

        private void ShowResultHeader(string title, string message)
        {
            uSyncResultPlaceHolder.Visible = true;
            resultHeader.Text = title;
            resultStatus.Text = message;
        }
    }
}