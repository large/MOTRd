using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;

namespace MOTRd
{
    [RunInstaller(true)]
    public partial class WindowsServiceInstaller : Installer
    {
        /// <summary>
        /// Public Constructor for WindowsServiceInstaller.
        /// - Put all of your Initialization code here.
        /// </summary>
        public WindowsServiceInstaller()
        {
            ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller();
            ServiceInstaller serviceInstaller = new ServiceInstaller();

            //# Service Account Information
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;

            //# Service Information
            serviceInstaller.DisplayName = Properties.Settings.Default.Servicedisplayname;
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            // This must be identical to the WindowsService.ServiceBase name
            // set in the constructor of WindowsService.cs
            serviceInstaller.ServiceName = Properties.Settings.Default.Servicename;
            serviceInstaller.Description = Properties.Settings.Default.Servicedescription;

            this.Installers.Add(serviceProcessInstaller);
            this.Installers.Add(serviceInstaller);
        }

        public override void Install(IDictionary stateSaver)
        {
            var config = ConfigurationManager.OpenExeConfiguration(Assembly.GetAssembly(typeof(WindowsServiceInstaller)).Location);
            var settings = config.AppSettings.Settings;

            string http = Convert.ToString(GetParam("http"));
            if(http.Length > 0)
            {
                if (settings["http"] == null)
                    settings.Add("http", http);
                else
                    settings["http"].Value = http;
            }

            string https = Convert.ToString(GetParam("https"));
            if (https.Length > 0)
            {
                if (settings["https"] == null)
                    settings.Add("https", https);
                else
                    settings["https"].Value = https;
            }

            //Save changed config
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);


            base.Install(stateSaver);
        }

        public override void Uninstall(IDictionary savedState)
        {
            base.Uninstall(savedState);
        }

        //Returns a parameter base on the key requested 
        private object GetParam(string p)
        {
            try
            {
                if (this.Context != null)
                {
                    if (this.Context.Parameters != null)
                    {
                        string lParamValue = this.Context.Parameters[p];
                        if (lParamValue != null)
                            return lParamValue;
                    }
                }
            }
            catch (Exception ex)
            {
                return ex;
            }
            return string.Empty;
        }
    }
}
