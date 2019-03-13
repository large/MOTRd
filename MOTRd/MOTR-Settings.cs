using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Net;
using System.IO;
using System.IO.Compression;

namespace MOTRd
{
    public class MOTR_Settings
    {

        public static void ShowAllSettings()
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                if (appSettings.Count == 0)
                    Console.WriteLine("AppSettings is empty.");
                else
                    foreach (var key in appSettings.AllKeys)
                        Console.WriteLine("Key: {0} Value: {1}", key, appSettings[key]);
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
            }
        }

        public static string GetString(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings[key] ?? "";
            }
            catch (ConfigurationErrorsException)
            {
                return "";
            }
        }

        public static bool SetString(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                    settings.Add(key, value);
                else
                    settings[key].Value = value;

                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                return true;
            }
            catch (ConfigurationErrorsException)
            {
                return false;
            }
        }

        public static int GetNumber(string key)
        {
            string sValue = GetString(key);
            if (sValue.Length == 0)
                return 0;
            return Convert.ToInt32(sValue);
        }

        public static bool SetNumber(string key, int value)
        {
            string sValue = value.ToString();
            return SetString(key, sValue);
        }

        public static string GetWebsiteToolVersion(string sTool)
        {
            WebClient m_WebClient = new WebClient();
            try
            {
                string sResult = m_WebClient.DownloadString("http://moviesonthe.run/tools/" + sTool + ".txt");
                m_WebClient.Dispose();
                return sResult;
            }
            catch(Exception ex)
            {
                return ex.ToString();
            }
        }

        public static string GetCurrentToolVersion(string sTool)
        {
            string baseFolder = MOTR_Settings.GetGlobalApplicationPath("tools");
            if (Directory.Exists(baseFolder))
            {
                if (File.Exists(baseFolder + sTool + ".txt"))
                    return File.ReadAllText(baseFolder + sTool + ".txt");
            }
            return "";
        }

        public static bool UpdateTool(string sTool, string sVersion)
        {
            WebClient m_WebClient = new WebClient();
            string baseFolder = MOTR_Settings.GetGlobalApplicationPath("tools");

            //Create directory if it does not exists
            try
            {
                if (!Directory.Exists(baseFolder))
                    Directory.CreateDirectory(baseFolder);
                if(!Directory.Exists(baseFolder + sTool))
                    Directory.CreateDirectory(baseFolder + sTool);

                //Now create a directory with current version
                string sToolDirectory = baseFolder + sTool + @"\";
                if (!Directory.Exists(sToolDirectory + sVersion))
                    Directory.CreateDirectory(sToolDirectory + sVersion);

                //Now download the file from the webserver
                m_WebClient.DownloadFile("http://moviesonthe.run/tools/" + sTool + "/" + sVersion + "/" + sTool + ".zip",
                                                sToolDirectory + sVersion + @"\" + sTool + ".zip");

                //Extract the file, ready to use
                //ZipFile.ExtractToDirectory(sToolDirectory + sVersion + @"\" + sTool + ".zip", sToolDirectory + sVersion);
                ZipArchive archive = ZipFile.OpenRead(sToolDirectory + sVersion + @"\" + sTool + ".zip");
                foreach (ZipArchiveEntry file in archive.Entries)
                    if(file.Name.Length > 0)
                        file.ExtractToFile(sToolDirectory + sVersion + @"\" + file.FullName, true);

                //Create a file with the current version
                File.WriteAllText(baseFolder + sTool + ".txt", sVersion);
                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        //Returns the path where to store configs and downloaded content
        static public string GetGlobalApplicationPath(string sSubDirectory = "", bool bCreateDirectory = true)
        {
            string sPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string sSubPath = Properties.Settings.Default.Servicename;
            sPath += @"\" + sSubPath;

            //Add the subdirectory, typical like "config", "tools" etc...
            if(sSubDirectory.Length > 0)
            {
                sPath += @"\" + sSubDirectory + @"\";
                if (!Directory.Exists(sPath) && bCreateDirectory)
                    Directory.CreateDirectory(sPath);
            }

            return sPath;
        }

        //Returns the path to the tools used by MOTRd
        static public string GetExecuteToolPath(string sTool)
        {
            string sToolVersion = GetCurrentToolVersion(sTool);
            if (sToolVersion.Length == 0)
                return "";

            string baseFolder = MOTR_Settings.GetGlobalApplicationPath("tools") ;
            try
            {
                string[] aExeFiles = Directory.GetFiles(baseFolder + sTool + @"\" + sToolVersion, "*.exe", SearchOption.TopDirectoryOnly);
                if (aExeFiles.Length > 0)
                    return aExeFiles[0];
                else
                    return "";
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error getting execute tool path, with error: " + ex.Message.ToString());
                return "";
            }
        }
    }
}
