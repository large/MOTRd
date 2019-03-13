using LiteDB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UNCFunctions;

namespace MOTRd
{
    public class MOTR_Dirs
    {
        public class BasePathsDb
        {
            public int Id { get; set; }
            public string DisplayName { get; set; }
            public string BasePath { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }

        struct BasePathsLight
        {
            public int nID;
            public string sDisplayName;
        }

        struct BasePathsUNC
        {
            public int Id;
            public UNCAccess pUNC;
        }

        private ArrayList aUNCConnections;
        LiteDatabase m_db;

        public MOTR_Dirs()
        {
            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();
            m_db = new LiteDatabase(sGlobalPath + @"\Directories.db");

            aUNCConnections = new ArrayList();
            ReadDirs();
        }

        ~MOTR_Dirs()
        {
            //Close the UNC connections
            for(int i=0;i< aUNCConnections.Count;i++)
                    ((BasePathsUNC)aUNCConnections[i]).pUNC.NetUseDelete();

            m_db.Dispose();
        }

        public void ReadDirs()
        {
            ArrayList m_list = new ArrayList();
            LiteCollection<BasePathsDb> aDBValues = m_db.GetCollection<BasePathsDb>("dirs");
            foreach (BasePathsDb item in aDBValues.FindAll())
            {
                //Connect & track the UNC for this path 
                if (item.BasePath.IndexOf(@"\\") != -1)
                    ConnectToUNC(item);
            }
        }

        private void ConnectToUNC(BasePathsDb baseDb)
        {
            //Path is \\<server>@<username>:<password>\rest\of\the\path

            //             Regex x = new Regex(@"\\\\(?<server>.*?)@(?<user>.*?):(?<pass>.*?)\\(?<path>.*?)$", RegexOptions.IgnoreCase);
            //             MatchCollection mc = x.Matches(aBasePath.sBasePath);
            // 
            //             if (mc.Count != 0)
            //             {
            //                 string sServer = mc[0].Groups["server"].Value;
            //                 string sUser = mc[0].Groups["user"].Value;
            //                 string sPassword = mc[0].Groups["pass"].Value;
            //                 string sPath = mc[0].Groups["path"].Value;
            //                 aBasePath.sBasePath = @"\\" + sServer + @"\" + sPath;


            //Create a UNC connector
            UNCAccess pUNC = new UNCAccess(); //to be added in array based on ID

            //Fire up a task to connect
            var bgw = new BackgroundWorker();
            bgw.DoWork += (_, __) =>
            {
                if (!pUNC.login(baseDb.BasePath, baseDb.Username, "", baseDb.Password))
                    Console.WriteLine("Could not connect to: " + baseDb.BasePath);
            };
            bgw.RunWorkerCompleted += (_, __) =>
            {
                Console.WriteLine("Finished connecting to UNC: " + baseDb.BasePath);
            };
            bgw.RunWorkerAsync();

            //Add the entry to the list of UNCs
            BasePathsUNC sUNC = new BasePathsUNC
            {
                pUNC = pUNC,
                Id = baseDb.Id
            };
            aUNCConnections.Add(sUNC);
        }

        public ArrayList GetDirectoryArray()
        {
            //LINQ to select only nID and sDisplay to be a part of this
            //            var query = from BasePaths dir in aPaths
            //                        select new { dir.nID, dir.sDisplayName };
            ArrayList m_list = new ArrayList();
            LiteCollection<BasePathsDb> aDBValues = m_db.GetCollection<BasePathsDb>("dirs");
            foreach (BasePathsDb item in aDBValues.FindAll())
            {
                BasePathsLight sLight = new BasePathsLight
                {
                    nID = item.Id,
                    sDisplayName = item.DisplayName
                };
                m_list.Add(sLight);
            }
            return m_list;
        }

        public string GetDriveById(int nID)
        {
            LiteCollection<BasePathsDb> aDBValues = m_db.GetCollection<BasePathsDb>("dirs");

            // Use Linq to query documents
            var results = aDBValues.FindOne(x => x.Id == nID);
            if (results != null) //If dir exists, return displayname
            {
                string sDrive = results.BasePath;
                if (sDrive.Substring(sDrive.Length - 1) != "\\")
                    sDrive += '\\';
                return sDrive; //Return ID
            }
            return "";
        }

        public string GetNameByPath(string sPath)
        {
            LiteCollection<BasePathsDb> aDBValues = m_db.GetCollection<BasePathsDb>("dirs");

            // Use Linq to query documents
            var results = aDBValues.FindOne(x => x.BasePath == sPath);
            if (results != null) //If dir exists, return displayname
                return results.DisplayName; //Return ID
            return "";
        }

        public int GetIDByPath(string sPath)
        {
            LiteCollection<BasePathsDb> aDBValues = m_db.GetCollection<BasePathsDb>("dirs");

            // Use Linq to query documents
            var results = aDBValues.FindOne(x => x.BasePath == sPath);
            if (results != null) //If dir exists, return displayname
                return results.Id; //Return ID
            return -1;
        }

        public ArrayList GetDirsArray()
        {
            ArrayList m_list = new ArrayList();
            LiteCollection<BasePathsDb> aDBValues = m_db.GetCollection<BasePathsDb>("dirs");
            foreach (BasePathsDb item in aDBValues.FindAll())
                m_list.Add(item);
            return m_list;
        }

        //Adds a directory (through add)
        public bool AddDirectory(string sDisplayName, string sPath, string sUNCUsername, string sUNCPassword)
        {
            //Cannot add a zero length
            if (sDisplayName.Length == 0 ||
                sPath.Length == 0)
                return false;

            //Ensure a backslash at the end
            if (sPath.Substring(sPath.Length - 1) != "\\")
                sPath += '\\';

            BasePathsDb aDir = new BasePathsDb
            {
                BasePath = sPath,
                DisplayName = sDisplayName,
                Username = sUNCUsername,
                Password = sUNCPassword
            };

            LiteCollection<BasePathsDb> aDBValues = m_db.GetCollection<BasePathsDb>("dirs");

            // Use Linq to query documents
            var results = aDBValues.FindOne(x => x.BasePath == sPath);
            if (results != null) //If path exists, return
                return false; //Already exist
            else //Add new dir
            {
                //Now add to DB
                aDBValues.EnsureIndex(x => x.DisplayName);
                aDBValues.Insert(aDir);

                //Connect & track the UNC for this path 
                if (aDir.BasePath.IndexOf(@"\\") != -1)
                    ConnectToUNC(aDir);
            }
            return true;
        }

        //Removes a directory based on the id in list
        public bool RemoveDirectory(int nID)
        {
            LiteCollection<BasePathsDb> aDBValues = m_db.GetCollection<BasePathsDb>("dirs");
            BasePathsDb results = aDBValues.FindOne(x => x.Id == nID);
            if (results == null)
                return false;

            //Check if id has a UNC
            for (int i = 0; i < aUNCConnections.Count; i++)
            {
                if (((BasePathsUNC)aUNCConnections[i]).Id == results.Id)
                {
                    ((BasePathsUNC)aUNCConnections[i]).pUNC.NetUseDelete();
                    aUNCConnections.RemoveAt(i);
                    break;
                }
            }
            //Remove the data at given ID
            aDBValues.Delete(results.Id);
            return true;
        }
    }
}
