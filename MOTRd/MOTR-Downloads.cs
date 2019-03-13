using LiteDB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MOTRd
{
    //Her skal man lage en liste over alle tilkoblinger og IDen de har
    //Lista kan lagres på disk og holdes oppdatert 
    struct DownloadStruct
    {
        public string sDownloadID;
        public string sSessionID;
        public string sFullPath;
        public bool bDeleteAfterUse;
        public string sValidTo; 
    }

    //
    // Summary:
    //     Enumerates values returned by several types and taken as a parameter of the Android.App.DownloadManager+Query.SetFilterByStatus
    //     member.
    //
    // Remarks:
    //     Enumerates values returned by the following: Android.App.DownloadManager.StatusFailedAndroid.App.DownloadManager.StatusPausedAndroid.App.DownloadManager.StatusPendingAndroid.App.DownloadManager.StatusRunningAndroid.App.DownloadManager.StatusSuccessfulAndroid.App.DownloadStatus.FailedAndroid.App.DownloadStatus.PausedAndroid.App.DownloadStatus.PendingAndroid.App.DownloadStatus.RunningAndroid.App.DownloadStatus.Successful
    //     and taken as a parameter of the Android.App.DownloadManager+Query.SetFilterByStatus
    //     member.
    public enum DownloadStatus
    {
        INITIALIZED = 0,
        PENDING = 1,
        RUNNING = 2,
        PAUSED = 3,
        COMPLETED = 4,
        CANCELED = 5,
        FAILED = 6,
        NOTSET = 7
    }

    //Database 
    public class DownloadMobileDB
    {
        public int Id { get; set; }
        public int MobileID { get; set; }
        public int DirID { get; set; }
        public string FilePath { get; set; }
        public string DownloadID { get; set; }
        public DownloadStatus Status { get; set; }
        public long FileSize { get; set; }
        public string Longtext { get; set; }
    }

    public class SimpleTest
    {
        public int Id { get; set; }
        public string test { get; set; }
    }

    public class MobileDownloadEventArgs : EventArgs
    {
        public int MobileID { get; set; }
        public int UserID { get; set; }
        public string PushID { get; set; }
    }

    public class MOTR_Downloads
    {
        //This list is filled after 
        private ArrayList aDownloads;
        //private const string sDownloadsFilename = "downloads.ini";

        LiteDatabase m_dbdownload;
        private readonly MOTR_Dirs m_Dirs;

        //Event to handle mobiledownload when connected
        public event EventHandler<MobileDownloadEventArgs> OnMobileDownload;

        //Randomizer generator
        RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
        private const string RANDOM_CHARACTERSUSED = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_-";
        private const int RANDOM_LENGTH = 92;

        public MOTR_Downloads(MOTR_Dirs _Dirs)
        {
            m_Dirs = _Dirs;

            //Array for what??? - old school remove after dbconvert
            aDownloads = new ArrayList();

            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();
            m_dbdownload = new LiteDatabase(sGlobalPath + @"\Downloads.db");

            // this.AddMobileDownload(16, 6, "Mainstream\\super.troopers.2.2018.1080p.bluray.x264-drones.mkv");

            //sDownloadsFilename = "downloads.ini";
            //WindowsService.LogEventInformation("Starting to read sessions...");

            //ReadSession();
            //Console.WriteLine("There is {0} sessions", aSessions.Count);
            //             for(int i=0;i<aSessions.Count;i++)
            //                 Console.WriteLine("Random {0} chars", GetRandomString(RANDOM_LENGTH, RANDOM_CHARACTERSUSED.ToCharArray()));
        }

        //Writes to file when class is destroyed
        ~MOTR_Downloads()
        {
            //WriteSession();
            m_dbdownload.Dispose();
        }

        //Fra: http://stackoverflow.com/questions/14983336/guid-newguid-vs-a-random-string-generator-from-random-next
        private string GetRandomString(int length, params char[] chars)
        {
            string s = "";
            for (int i = 0; i < length; i++)
            {
                byte[] intBytes = new byte[4];
                rand.GetBytes(intBytes);
                uint randomInt = BitConverter.ToUInt32(intBytes, 0);
                s += chars[randomInt % chars.Length];
            }
            return s;
        }

        //Get the array id-number of a session
        private int DownloadArrayNumber(string sDownloadID)
        {
            for (int i = 0; i < aDownloads.Count; i++)
            {
                string sID = ((DownloadStruct)aDownloads[i]).sDownloadID;
                if (sID == sDownloadID)
                    return i;
            }

            //Not found
            return -1;
        }

        //=====================================
        //Public functions here
        public bool DownloadExists(string sDownloadID)
        {
            //Get the ID-in the array
            int nID = DownloadArrayNumber(sDownloadID);
            if (nID == -1)
                return false;
            else
                return true;
        }

        //Creates a download "session". Works either with 
        public string AddDownload(string sSessionID, string sFullPath, bool bDeleteAfterUsage, string sValidTo = "", string sMobileDownloadID = "")
        {
            string sDownloadID = GetRandomString(RANDOM_LENGTH, RANDOM_CHARACTERSUSED.ToCharArray());

            //Vi kan ikke ha den samme session navnet 2 ganger... (kjør loop til den er unik)
            while (DownloadExists(sDownloadID))
                sDownloadID = GetRandomString(RANDOM_LENGTH, RANDOM_CHARACTERSUSED.ToCharArray());

            DownloadStruct structDownload = new DownloadStruct();
            structDownload.sDownloadID = sDownloadID;
            structDownload.sSessionID = sSessionID;
            structDownload.sFullPath = sFullPath;
            structDownload.bDeleteAfterUse = bDeleteAfterUsage;
            structDownload.sValidTo = sValidTo;
            aDownloads.Add(structDownload);
            //WriteSession();

            //Override the list of items with our mobileID
            if (sMobileDownloadID.Length > 0)
                return sMobileDownloadID;

            return sDownloadID;
        }

        //Removes a download from the array
        public bool RemoveDownload(string sDownloadID)
        {
            //Find our session and remove it1
            int nID = DownloadArrayNumber(sDownloadID);
            if (nID == -1)
                return false;

            //Remove it and save
            aDownloads.RemoveAt(nID);
            //WriteSession();

            return true;
        }

        //Returns full path to the file users want to download, if not valid return nothing
        public string GetDownload(string sDownloadID, bool bHead = false)
        {
            //Get ID of the download
            int nID = DownloadArrayNumber(sDownloadID);

            //Not found, check if it is a mobile download
            if (nID == -1)
                return GetMobileDownload(sDownloadID);

            //Store the path for returning
            string sFullPath = ((DownloadStruct)aDownloads[nID]).sFullPath;

            //Check if the link is going to be deleted or not
            if (!bHead)
            {
                if (((DownloadStruct)aDownloads[nID]).bDeleteAfterUse)
                {
                    RemoveDownload(sDownloadID);
                }
                else
                {
                    string sValidTo = ((DownloadStruct)aDownloads[nID]).sValidTo;
                    if (sValidTo.Length == 0)
                    {
                        RemoveDownload(sDownloadID);
                        return "";
                    }
                    else
                    {
                        DateTime oValidTo = Convert.ToDateTime(sValidTo);
                        if (oValidTo < DateTime.Now) //If validto is less than now time, then remove it
                        {
                            RemoveDownload(sDownloadID);
                            return "";
                        }
                    }
                }
            }

            return sFullPath;
        }

        //Download for handling by the mobile
        public bool AddMobileDownload(int iMobileID, int iDirID, string sPath, long lFilesize)
        {
            //Cannot add a zero length
            if (sPath.Length == 0)
                return false;

            //Struct for handling item
            DownloadMobileDB aDownload = new DownloadMobileDB
            {
                FilePath = sPath,
                DirID = iDirID,
                MobileID = iMobileID,
                DownloadID = GetRandomString(RANDOM_LENGTH, RANDOM_CHARACTERSUSED.ToCharArray()),
                Status = DownloadStatus.NOTSET,
                FileSize = lFilesize
            };

            //Check if the item exists already
            LiteCollection<DownloadMobileDB> aDBValues = m_dbdownload.GetCollection<DownloadMobileDB>("mobiledownload");

            var results = aDBValues.FindOne(x => x.FilePath == sPath && x.MobileID == iMobileID && x.DirID == iDirID);
            if (results != null)
            {
                results.Status = DownloadStatus.NOTSET;
                aDBValues.Update(results);
                return false; //Already exist, do not add twice, but reset status
            }
            else //Add new dir to DB
            {
                aDBValues.EnsureIndex(x => x.DownloadID);
                var val = aDBValues.Insert(aDownload);
                return true;
            }
        }

        //Return an array of downloads for the mobile to get
        public string GetMobileDownload(int iMobileID, bool bReturnDownloadID = false)
        {
            LiteCollection<DownloadMobileDB> aDBValues = m_dbdownload.GetCollection<DownloadMobileDB>("mobiledownload");

            // Use Linq to query documents
            var results = aDBValues.FindOne(x => x.MobileID == iMobileID && (x.Status == DownloadStatus.NOTSET || x.Status == DownloadStatus.PENDING) );
            if (results != null) //If dir exists, return displayname
            {
                string sDrive = m_Dirs.GetDriveById(results.DirID) + results.FilePath;

                //Override with the DownloadID
                if (bReturnDownloadID)
                    sDrive = results.DownloadID;

                return sDrive; //Returns the actually path of the fil, this has to be added to downloads afterwards
            }
            return "";
        }

        //Returns full path based on the DownloadID
        public string GetMobileDownload(string DownloadID)
        {
            LiteCollection<DownloadMobileDB> aDBValues = m_dbdownload.GetCollection<DownloadMobileDB>("mobiledownload");

            // Use Linq to query documents
            var results = aDBValues.FindOne(x => x.DownloadID == DownloadID && x.Status != DownloadStatus.COMPLETED);
            if (results != null) //If dir exists, return displayname
            {
                string sDrive = m_Dirs.GetDriveById(results.DirID) + results.FilePath;
                return sDrive; //Returns the actually path of the fil, this has to be added to downloads afterwards
            }
            return "";
        }

        //Set status of a download from callbacks
        public bool SetMobileDownloadStatus(string DownloadID, DownloadStatus downloadStatus, string Longtext)
        {
            if(downloadStatus == DownloadStatus.CANCELED)
                Console.WriteLine("Cancel set on download: " + DownloadID + " - with longtext: " + Longtext);

            LiteCollection<DownloadMobileDB> aDBValues = m_dbdownload.GetCollection<DownloadMobileDB>("mobiledownload");
            //Special handling for "cancel" function
            if(DownloadID.Contains("MOBILEID"))
            {
                //Get the MobileID
                string[] temp = DownloadID.Split('=');
                int MobileID = Convert.ToInt32(temp[1]);

                //Only get "RUNNING" variables and override them
                var resultsmobileid = aDBValues.FindOne(x => x.MobileID == MobileID && x.Status == DownloadStatus.RUNNING);
                if (resultsmobileid != null) //If file exists, set status
                {
                    resultsmobileid.Status = downloadStatus;
                    resultsmobileid.Longtext = Longtext;
                    aDBValues.Update(resultsmobileid);
                    return true; //Status was set return positive
                }
                return false;
            }

            //If DownloadID = real downloadid, not mobile
            // Use Linq to query documents
            var results = aDBValues.FindOne(x => x.DownloadID == DownloadID);
            if (results != null) //If dir exists, return displayname
            {
                results.Status = downloadStatus;
                results.Longtext = Longtext;
                aDBValues.Update(results);
                return true; //Status was set
            }
            return false;
        }

        //Return an array of all downloads with status
        public ArrayList GetMobileDownloadList(int iMobileID)
        {
            ArrayList arrayList = new ArrayList();
            LiteCollection<DownloadMobileDB> aDBValues = m_dbdownload.GetCollection<DownloadMobileDB>("mobiledownload");
            var results = aDBValues.Find(x => x.MobileID == iMobileID);
            foreach (DownloadMobileDB item in results)
                if (item != null)
                {
                    arrayList.Add(item.Id);
                    arrayList.Add(item.DownloadID);
                    arrayList.Add(item.FilePath.Substring(item.FilePath.LastIndexOf('\\') + 1));
                    arrayList.Add(item.FileSize);
                    arrayList.Add(item.Status);
                    arrayList.Add(item.Longtext);
                }
            return arrayList;
        }

        //Return an array of all downloads with status
        public bool RemoveMobileDownload(string sDownloadID)
        {
            LiteCollection<DownloadMobileDB> aDBValues = m_dbdownload.GetCollection<DownloadMobileDB>("mobiledownload");
            var results = aDBValues.FindOne(x => x.DownloadID == sDownloadID);
            if (results != null)
            {
                aDBValues.Delete(results.Id);
                return true;
            }
            return false;
        }

        //Triggers an event to the MOTR-Webserver class that updates the specified queue for procentage
        public void DownloadMobileTrigger(MobileDownloadEventArgs e)
        {
            OnMobileDownload?.Invoke(this, e);
        }
    }
}
