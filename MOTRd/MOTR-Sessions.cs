using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Security.Cryptography;
using System.IO;
using System.Text.RegularExpressions;
using LiteDB;
using Effortless.Net.Encryption;

namespace MOTRd
{
    //Her skal man lage en liste over alle tilkoblinger og IDen de har
    //Lista kan lagres på disk og holdes oppdatert 
    public class SessionStruct
    {
        public int Id { get; set; }
        public DateTime sLastUsed { get; set; }
        public string sID { get; set; }
        public bool bLoggedIn { get; set; }
        public int UserID { get; set; }
    }

    //TemporarySession holds information that is not stored. sID is used as ref
    struct TemporarySession
    {
        public string sID;
        public ArrayList aTemporaryVariables; //Ikke lagret data som er knyttet til sesjonen så lenge den er aktiv
        public ArrayList aFileList; //Used for returning JSON through websocket
    }

    //Temp values that are filled in the aTemporaryVariables array
    struct TemporarySessionStruct
    {
        public string sItem;
        public string sValue;
    }

    //Items in the aFileList contain the current users filelist
    struct TemporaryFileList
    {
        public int nID;
        public string sDisplayName; //Displayname, stripped with info
        public bool bIsFolder;
        public string sRealname; //Real file/directory name
        public string sFileSize; //Empty string for directories, pretty text for actual size
    }

    //A temporary list that is presented for en user (without sRealname)
    struct TemporaryFileListClean //Same as TemporarySessionValues, but without realname
    {
        public int nID;
        public string sDisplayName; //Displayname, stripped with info
        public bool bIsFolder;
        public string sFileSize; //Empty string for directories, pretty text for actual size
    }

    //Sessionless storage is based on userid & dirid and not session, so it 
    public class SessionlessStorage
    {
        public int Id { get; set; }
        public int UserID { get; set; }
        public int DirID { get; set; }
        public string Path { get; set; } //Skal ikke inneholde hovedmappa (gjør det mulig å bytte mappe et sted for alle)
        public string Command { get; set; }
        public string Value { get; set; }
        public DateTime Added { get; set; }
    }

    public class MOTR_Sessions
    {
        //This list is filled after 
        private ArrayList aTemporarySessions;

        //Randomizer generator
        RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();
        private const string RANDOM_CHARACTERSUSED = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890";
        private const int RANDOM_LENGTH = 60;

        //SessionLess storage object to handle multiple threads
        static object _lockDbObject = new object();
        static object _lockDbObjectMovies = new object(); //Database locking for movieinfo

        //Password and salt for encrypt/decrypt of whatever
        private string sSessionInitVector = "   DetVarEngangEnLitenHestSom!\" "; //Alltid keysize / 8, dvs 16 bokstaver == 32 bytes i array
        private Bytes.KeySize nSessionPasswordKeySize = Bytes.KeySize.Size256;
        private string sSessionPassword = "Dette er passordet jaøæå";
        private int nIterations = 10000;
        private string sSessionSalt = "This is MySalt if you ever wonder about that... øåæ";

        //Usermanagement
        private readonly MOTR_Users m_Users;

        LiteDatabase m_db;

        public MOTR_Sessions(MOTR_Users Users)
        {
            //Storing the users variable, used to check the ID etc..
            m_Users = Users;
            aTemporarySessions = new ArrayList();
            //WindowsService.LogEventInformation("Starting to read sessions...");

            //Opening the database
            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();
            m_db = new LiteDatabase(sGlobalPath + @"\Sessions.db");

            //Check for expired sessions
            LiteCollection<SessionStruct> aDBValues = m_db.GetCollection<SessionStruct>("session");
            foreach (SessionStruct item in aDBValues.FindAll())
                if (SessionExpired(item.sLastUsed))
                    RemoveSession(item.sID);
        }

        //Writes to file when class is destroyed
        ~MOTR_Sessions()
        {
            m_db.Dispose();
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

        //=====================================
        //Public functions here
        public bool SessionExists(string sSession)
        {
            LiteCollection<SessionStruct> aDBValues = m_db.GetCollection<SessionStruct>("session");
            SessionStruct results = aDBValues.FindOne(x => x.sID == sSession);
            if (results != null)
                return true;
            return false;
        }

        public bool SessionLoggedIn(string sSession, string sUser)
        {
            LiteCollection<SessionStruct> aDBValues = m_db.GetCollection<SessionStruct>("session");
            SessionStruct results = aDBValues.FindOne(x => x.sID == sSession);
            if (results == null)
                return false;
            else
            {
                //Check if the session is expired, remove session if so
                if (SessionExpired(results.sLastUsed))
                {
                    RemoveSession(sSession);
                    return false;
                }

                //Special case for Websites, since the AuthID is not a cookie, we cannot receive it...
                if (sUser == "[WEBBASEDAUTH, NOT USER]")
                    return results.bLoggedIn;

                //Here we compare the username against the session, false if no good...
                string sUsername = GetTemporaryVariable(sSession, "Username");
                //if (((SessionStruct)aSessions[nID]).sDisplayname == sUser) //Temp hack
                if(sUsername == sUser)
                    return results.bLoggedIn;
                else
                    return false;
            }
        }

        public string AddSession()
        {
            string sSession = GetRandomString(RANDOM_LENGTH, RANDOM_CHARACTERSUSED.ToCharArray());

            //Vi kan ikke ha den samme session navnet 2 ganger... (kjør loop til den er unik)
            while (SessionExists(sSession))
                sSession = GetRandomString(RANDOM_LENGTH, RANDOM_CHARACTERSUSED.ToCharArray());

            SessionStruct structSession = new SessionStruct();
            structSession.sLastUsed = DateTime.Now;
            structSession.sID = sSession;
            structSession.bLoggedIn = false;
            structSession.UserID = -1;

            //Store in database
            LiteCollection<SessionStruct> aDBValues = m_db.GetCollection<SessionStruct>("session");
            aDBValues.EnsureIndex(x => x.sID);
            aDBValues.Insert(structSession);

            //Now add a TempID for verify the HTTP cookie for it
            GenerateTempIDInSession(sSession);
            return sSession;
        }

        //Generates a random ID to cross check during first load in the HTTP part (sets the SessionID as cookie httponly afterwards)
        public string GenerateTempIDInSession(string sSession)
        {
            if (sSession.Length <= 0)
                return "";

            //Generate a TempID to set the 
            string sTempID = GetRandomString(RANDOM_LENGTH, RANDOM_CHARACTERSUSED.ToCharArray());
            SetTemporaryVariable(sSession, "TempID", sTempID);
            return sTempID;
        }

        //Return session based on TempID (temporary variable set by javascript)
        public string GetSessionByTempID(string sTempID)
        {
            if (sTempID.Length <= 0)
                return "";

            //Loop through all the sessions
            LiteCollection<SessionStruct> aDBValues = m_db.GetCollection<SessionStruct>("session");
            foreach (SessionStruct item in aDBValues.FindAll())
            {
                string sIDTemp = GetTemporaryVariable(item.sID, "TempID");
                if (sIDTemp == sTempID)
                    return item.sID;
            }

            return "";
        }

        //Return the TempID by session
        public string GetTempIDBySession(string sSession)
        {
            return GetTemporaryVariable(sSession, "TempID");
        }

        //Removes a session
        public bool RemoveSession(string sSession)
        {
            //Find our session and remove it1
            LiteCollection<SessionStruct> aDBValues = m_db.GetCollection<SessionStruct>("session");
            SessionStruct results = aDBValues.FindOne(x => x.sID == sSession);
            if (results == null)
                return false;

            aDBValues.Delete(results.Id);

            return true;
        }

        public void SetLogin(string sSession, bool bLoginState, string sUsername)
        {
            LiteCollection<SessionStruct> aDBValues = m_db.GetCollection<SessionStruct>("session");
            SessionStruct results = aDBValues.FindOne(x => x.sID == sSession);
            if (results == null)
                return;

            //Check if the session is expired, remove session if so
            if(SessionExpired(results.sLastUsed))
            {
                RemoveSession(sSession);
                return;
            }

            //Set the state of the session
            results.sLastUsed = DateTime.Now;
            results.bLoggedIn = bLoginState;
            results.UserID = m_Users.GetUserID(sUsername);

            aDBValues.Update(results);
        }

        //Checks if a date is older than x number of days
        private bool SessionExpired(DateTime sessionDate)
        {
            //Check if the LastUse date is older than x days
            if ((DateTime.Now - sessionDate).TotalDays > 7)
                return true;
            else
                return false;
        }

        //Return the displayname of a session
        public string GetDisplayName(string sSession)
        {
            return GetTemporaryVariable(sSession, "Username");
        }

        //Sets a temporary variable to the array <item>=<value>
        public void SetTemporaryVariable(string sSession, string sItem, string sValue)
        {
            //Loop through the variables
            ArrayList aVariables = GetTemporaryArray(sSession);
            for (int i = 0; i < aVariables.Count; i++)
            {
                TemporarySessionStruct aTemp = ((TemporarySessionStruct)aVariables[i]);
                if (aTemp.sItem == sItem)
                {
                    aTemp.sValue = sValue;
                    aVariables.RemoveAt(i);
                    aVariables.Insert(i, aTemp);
                    return;
                }
            }

            //Here we just add a new temp-struct
            TemporarySessionStruct aTempAdd = new TemporarySessionStruct();
            aTempAdd.sItem = sItem;
            aTempAdd.sValue = sValue;
            aVariables.Add(aTempAdd);
        }

        //Returns the sValue of the item we have found
        public string GetTemporaryVariable(string sSession, string sItem)
        {
            //Loop through the variables
            ArrayList aVariables = GetTemporaryArray(sSession);
            for (int i = 0; i < aVariables.Count; i++)
            {
                TemporarySessionStruct aTemp = ((TemporarySessionStruct)aVariables[i]);
                if (aTemp.sItem == sItem)
                    return aTemp.sValue;
            }

            return "";
        }

        //Returns the two array attached to a session
        public ArrayList GetTemporaryArray(string sSession, bool FileList = false)
        {
            for (int i = 0; i < aTemporarySessions.Count; i++)
            {
                TemporarySession aTempSession = ((TemporarySession)aTemporarySessions[i]);
                if (aTempSession.sID == sSession)
                {
                    if (!FileList)
                        return aTempSession.aTemporaryVariables;
                    else
                        return aTempSession.aFileList;
                }
            }

            //Create an temporary session variable since it did not exist
            TemporarySession temporarySession = new TemporarySession
            {
                sID = sSession,
                aFileList = new ArrayList(),
                aTemporaryVariables = new ArrayList()
            };
            aTemporarySessions.Add(temporarySession);

            if (!FileList)
                return temporarySession.aTemporaryVariables;
            else
                return temporarySession.aFileList;
        }

        public ArrayList GenerateFilelist(string sSession, string sPath, string sParent="")
        {
            ArrayList aFileList = new ArrayList();

            //Not logged in, then no go...
            if (!SessionLoggedIn(sSession, GetTemporaryVariable(sSession, "Username")))
                return aFileList;

            if (Directory.Exists(sPath))
            {
                //Get the sort-string; NAME, MODIFY, SIZE
                string sSorting = GetTemporaryVariable(sSession, "Sorting");
                if (sSorting.Length == 0)
                    sSorting = "NAME";

                DirectoryInfo[] dirArray = null;
                FileInfo[] filesArray = null;

                if (sSorting == "MODIFY")
                {
                    dirArray = new DirectoryInfo(sPath)
                            .GetDirectories("*")
                            .OrderByDescending(f => f.LastWriteTime)
                            .ToArray();
                    filesArray = new DirectoryInfo(sPath)
                            .GetFiles("*")
                            .OrderByDescending(f => f.LastWriteTime)
                            .ToArray();
                } else if(sSorting == "SIZE")
                {
                    dirArray = new DirectoryInfo(sPath)
                            .GetDirectories("*")
                            .OrderByDescending(f => f.Name)
                            .ToArray();
                    filesArray = new DirectoryInfo(sPath)
                            .GetFiles("*")
                            .OrderByDescending(f => f.Length)
                            .ToArray();
                } else //NAME
                {
                    dirArray = new DirectoryInfo(sPath)
                            .GetDirectories("*")
                            //.OrderByDescending(f => f.LastWriteTime)
                            .ToArray();
                    filesArray = new DirectoryInfo(sPath)
                            .GetFiles("*")
                            //.OrderByDescending(f => f.LastWriteTime)
                            .ToArray();
                }

                //First add the ".." for the top
                if (sParent.Length > 0)
                {
                    TemporaryFileList aDirDots = new TemporaryFileList();
                    aDirDots.bIsFolder = true;
                    aDirDots.nID = aFileList.Count;
                    aDirDots.sRealname = sParent;
                    aDirDots.sDisplayName = "(parent directory)";
                    aDirDots.sFileSize = "..";
                    aFileList.Add(aDirDots);
                }

                //Check if the user wants cleaned dirs or not
                string sValue = GetTemporaryVariable(sSession, "Clean");
                if (sValue.Count() == 0)
                    sValue = "False";
                bool bCleanFilename = Convert.ToBoolean(sValue);

                //Always show directory first when we are not sorting by size
                if (sSorting != "SIZE")
                {
                    foreach (DirectoryInfo sDir in dirArray)
                    {
                        TemporaryFileList aDir = new TemporaryFileList();
                        aDir.bIsFolder = true;
                        aDir.nID = aFileList.Count;
                        aDir.sRealname = sDir.FullName;
                        aDir.sFileSize = "";
                        if (bCleanFilename)
                        {
                            aDir.sDisplayName = CleanDirectoryAndFilename(sDir.Name);
                            if (!IgnoreFileOrDirectory(sDir.Name))
                                aFileList.Add(aDir);
                        }
                        else
                        {
                            aDir.sDisplayName = sDir.Name.Substring(sDir.Name.LastIndexOf('\\') + 1);
                            aFileList.Add(aDir);
                        }
                    }
                }

                foreach (FileInfo sFile in filesArray)
                {
                    TemporaryFileList aFile = new TemporaryFileList();
                    aFile.bIsFolder = false;
                    aFile.nID = aFileList.Count;
                    aFile.sRealname = sFile.Name;
                    aFile.sFileSize = this.HumanFileSize(sFile.Length);
                    if (bCleanFilename)
                    {
                        aFile.sDisplayName = CleanDirectoryAndFilename(sFile.Name, false);
                        if (!IgnoreFileOrDirectory(sFile.Name))
                            aFileList.Add(aFile);
                    }
                    else
                    {
                        aFile.sDisplayName = sFile.Name.Substring(sFile.Name.LastIndexOf('\\') + 1);
                        aFileList.Add(aFile);
                    }
                }

                //Equal as above, but directories are after files
                if (sSorting == "SIZE")
                {
                    foreach (DirectoryInfo sDir in dirArray)
                    {
                        TemporaryFileList aDir = new TemporaryFileList();
                        aDir.bIsFolder = true;
                        aDir.nID = aFileList.Count;
                        aDir.sRealname = sDir.FullName;
                        aDir.sFileSize = "";
                        if (bCleanFilename)
                        {
                            aDir.sDisplayName = CleanDirectoryAndFilename(sDir.Name);
                            if (!IgnoreFileOrDirectory(sDir.Name))
                                aFileList.Add(aDir);
                        }
                        else
                        {
                            aDir.sDisplayName = sDir.Name.Substring(sDir.Name.LastIndexOf('\\') + 1);
                            aFileList.Add(aDir);
                        }
                    }
                }

            }

            //Find the filelist
            for (int i = 0; i < aTemporarySessions.Count; i++)
            {
                TemporarySession aTempSession = ((TemporarySession)aTemporarySessions[i]);
                if (aTempSession.sID == sSession)
                {
                    aTempSession.aFileList = aFileList;
                    aTemporarySessions.RemoveAt(i);
                    aTemporarySessions.Insert(i, aTempSession);
                }
            }

            //Now create a temp array to return to user without sRealName (real directory name)
            ArrayList aTempFilelist = new ArrayList();
            for(int i=0;i<aFileList.Count;i++)
            {
                TemporaryFileListClean filelistTemp = new TemporaryFileListClean();
                filelistTemp.bIsFolder = ((TemporaryFileList)aFileList[i]).bIsFolder;
                filelistTemp.nID = ((TemporaryFileList)aFileList[i]).nID;
                filelistTemp.sDisplayName = ((TemporaryFileList)aFileList[i]).sDisplayName;
                filelistTemp.sFileSize = ((TemporaryFileList)aFileList[i]).sFileSize;
                aTempFilelist.Add(filelistTemp);
            }

            return aTempFilelist;
        }

        private bool IgnoreFileOrDirectory(string sFileName)
        {
            Regex x = new Regex(@".r\d\d$", RegexOptions.IgnoreCase);
            MatchCollection m = x.Matches(sFileName);

            if (m.Count > 0)
                return true;

            return false;
        }

        public string CleanDirectoryAndFilename(string sPath, bool bFolder=true)
        {
            string sDir = sPath.Substring(sPath.LastIndexOf('\\')+1);

            //-MOTR[ override
            if (sDir.ToUpper().Contains("-MOTR["))
                return sDir;

            //Tatt fra Kodi
            Regex x = new Regex(@"[ _\,\.\(\)\[\]\-](ac3|dts|custom|dc|remastered|divx|divx5|dsr|dsrip|dutch|dvd|dvd5|dvd9|dvdrip|dvdscr|dvdscreener|screener|dvdivx|cam|fragment|fs|hdtv|hdrip|hdtvrip|internal|limited|multisubs|ntsc|ogg|ogm|pal|pdtv|proper|repack|rerip|retail|r3|r5|bd5|se|svcd|swedish|german|read.nfo|nfofix|unrated|extended|ws|telesync|ts|telecine|tc|brrip|bdrip|480p|480i|576p|576i|720p|720i|1080p|1080i|3d|hrhd|hrhdtv|hddvd|bluray|x264|h264|xvid|xvidvd|xxx|www.www|cd[1-9]|\[.*\])([ _\,\.\(\)\[\]\-]|$)", RegexOptions.IgnoreCase);
            MatchCollection m = x.Matches(sDir);

            //1 or more hits :)
            if (m.Count > 0)
            {
                //Sparer filetternavnet, hvis det ikke er folder
                string sExt = "";
                if (!bFolder)
                {
                    int nPos = sDir.LastIndexOf('.');
                    if(nPos > 0)
                        sExt = sDir.Substring(nPos);
                }

                //Første hittet finner man og fjerner alt bak...
                sDir = sDir.Substring(0, sDir.IndexOf(m[0].Value));
                sDir = sDir.Replace('.', ' ');
                if(!bFolder)
                    sDir += sExt;
            }
            return sDir;
        }


        public string GetPathByID(string sSession, int iID)
        {
            ArrayList aFileList = GetTemporaryArray(sSession, true);

            //Check the index
            if (iID < 0 || iID >= aFileList.Count)
                return "";

            string sPath = ((TemporaryFileList)aFileList[iID]).sRealname;

            return sPath;
        }

        //Returns a fileinfo array to send
        public ArrayList GetFileInformation(string sPath)
        {
            ArrayList aFileInfo = new ArrayList();

            if (!File.Exists(sPath))
                return aFileInfo;

            FileInfo oFileObject = new FileInfo(sPath);

            string sFile = sPath.Substring(sPath.LastIndexOf('\\') + 1);
            aFileInfo.Add(sFile);
            string sFileDisplay = CleanDirectoryAndFilename(sPath, false);
            aFileInfo.Add(sFileDisplay);

            aFileInfo.Add(this.HumanFileSize(oFileObject.Length));
            aFileInfo.Add(oFileObject.Length.ToString());
            aFileInfo.Add(oFileObject.Extension);
            aFileInfo.Add(oFileObject.CreationTime.ToString());
            aFileInfo.Add(oFileObject.LastWriteTime.ToString());

            return aFileInfo;
        }

        private string HumanFileSize(double len)
        {
            //Make Human filesize for presentation
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            return String.Format("{0:0.#} {1}", len, sizes[order]);
        }

        //Sessionless storage 
        public string GetSessionLessVariable(string sSession, int nFileID, string sCommand)
        {
            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();

            //Get the path for the FileID
            string sDrive = this.GetTemporaryVariable(sSession, "Drive");
            string sPath = this.GetTemporaryVariable(sSession, "DrivePosition");
            string sFile = this.GetPathByID(sSession, nFileID);

            int DirID = Convert.ToInt32(GetTemporaryVariable(sSession, "DriveID"));
            int UserID = Convert.ToInt32(GetTemporaryVariable(sSession, "UserID"));

            //No length, no go...
            if (sPath.Length == 0 || sFile.Length == 0)
                return "";

            //Include full filename, minus the drive length
            sPath += sFile;
            sPath = sPath.Substring(sDrive.Length);

            lock (_lockDbObject)
            {
                LiteDatabase m_db = new LiteDatabase(sGlobalPath + @"\SessionLessStorage.db");
                LiteCollection<SessionlessStorage> aDBValues = m_db.GetCollection<SessionlessStorage>("sessionlessstorage");

                var results = aDBValues.FindOne(x => x.UserID == UserID && x.DirID == DirID && x.Path == sPath && x.Command == sCommand);
                m_db.Dispose();
                if (results != null)
                {
                    return results.Value;
                }
                else
                {
                    return "";
                }
            }
        }

        public bool StoreSessionLessVariable(string sSession, int nFileID, string sCommand, string sValue)
        {
            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();

            //Store the username
            int DirID = Convert.ToInt32(GetTemporaryVariable(sSession, "DriveID"));
            int UserID = Convert.ToInt32(GetTemporaryVariable(sSession, "UserID"));

            //Get the path for the FileID
            string sDrive = this.GetTemporaryVariable(sSession, "Drive");
            string sPath = this.GetTemporaryVariable(sSession, "DrivePosition");
            string sFile = this.GetPathByID(sSession, nFileID);

            //No length, no go...
            if (sPath.Length == 0 || sFile.Length == 0)
                return false;

            //Include full filename
            sPath += sFile;
            sPath = sPath.Substring(sDrive.Length);
            SessionlessStorage aSessionlessStorage = new SessionlessStorage
            {
                DirID = DirID,
                UserID = UserID,
                Path = sPath,
                Command = sCommand,
                Value = sValue,
                Added = DateTime.Now
            };

            lock (_lockDbObject)
            {
                LiteDatabase m_db = new LiteDatabase(sGlobalPath + @"\SessionLessStorage.db");
                LiteCollection<SessionlessStorage> aDBValues = m_db.GetCollection<SessionlessStorage>("sessionlessstorage");

                // Use Linq to query documents
                var results = aDBValues.FindOne(x => x.UserID == UserID && x.DirID == DirID && x.Path == sPath && x.Command == sCommand);
                if (results != null)
                {
                    if (sValue.Length > 0)
                    {
                        results.Value = sValue;
                        aDBValues.Update(results);
                    }
                    else
                    {
                        aDBValues.Delete(results.Id); //Remove by id
                    }
                }
                else //Add new
                {
                    aDBValues.EnsureIndex(x => x.UserID);
                    aDBValues.Insert(aSessionlessStorage);
                }

                m_db.Dispose();
            }

            return true;
        }

        //Encrypts a string, makes it sendable through websocket
        public string Encrypt(string sString)
        {
            try
            {
                return Strings.Encrypt(sString, sSessionPassword, sSessionSalt, sSessionInitVector, nSessionPasswordKeySize, nIterations);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not encrypt string: " + e.ToString());
                return "";
            }
        }

        //Decrypts a string
        public string Decrypt(string sString)
        {
            try
            {
                return Strings.Decrypt(sString, sSessionPassword, sSessionSalt, sSessionInitVector, nSessionPasswordKeySize, nIterations);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not deencrypt string: " + e.ToString());
                return "";
            }
        }

        //Get movieinformation from database
        public MovieInformation GetMovieInformation(string sSession, int nFileID)
        {
            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();

            //Get the path for the FileID
            string sPath = this.GetTemporaryVariable(sSession, "DrivePosition");
            string sFile = this.GetPathByID(sSession, nFileID);

            //No length, no go...
            if (sPath.Length == 0 || sFile.Length == 0)
                return null;

            //Include full filename
            sPath += sFile;

            lock (_lockDbObjectMovies)
            {
                LiteDatabase m_db = new LiteDatabase(sGlobalPath + @"\MovieInformation.db");
                LiteCollection<MovieInformation> aDBValues = m_db.GetCollection<MovieInformation>("movieinfo");

                MovieInformation results = aDBValues.FindOne(x => x.Path == sPath);
                m_db.Dispose();
                if (results != null)
                {
                    return results;
                }
                else
                {
                    return null;
                }
            }
        }

        //Store movieinformation in a database
        public bool StoreMovieInformation(string sSession, int nFileID, MovieInformation movieInformation)
        {
            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();

            //Get the path for the FileID
            string sPath = this.GetTemporaryVariable(sSession, "DrivePosition");
            string sFile = this.GetPathByID(sSession, nFileID);

            //No length, no go...
            if (sPath.Length == 0 || sFile.Length == 0)
                return false;

            //Include full filename
            sPath += sFile;

            //Store it in the class
            movieInformation.Path = sPath;

            lock (_lockDbObjectMovies)
            {
                LiteDatabase m_db = new LiteDatabase(sGlobalPath + @"\MovieInformation.db");
                LiteCollection<MovieInformation> aDBValues = m_db.GetCollection<MovieInformation>("movieinfo");

                // Use Linq to query documents
                var results = aDBValues.FindOne(x => x.Path == sPath);
                if (results != null)
                {
                    int ResultID = results._id;
                    results = movieInformation;
                    results._id = ResultID;
                    aDBValues.Update(results);
                }
                else //Add new
                {
                    aDBValues.EnsureIndex(x => x.Path);
                    aDBValues.Insert(movieInformation);
                }

                m_db.Dispose();
            }

            return true;
        }
    }
}
