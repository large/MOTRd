using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UNCFunctions;

namespace MOTRd
{
    struct DrivesAndNetwork
    {
        public string sName; //Displayname, stripped with info
        public string sType; //Type of network
        public bool bIsDrive; //true = it is drive, false = network server
    }
    
    struct DirectoryBrowse
    {
        public string sName;
        public string sPath;
    }

    //Gives a list of all the network items available in network
    class NetworkInfo
    {
        //declare the Netapi32 : NetServerEnum method import
        [DllImport("Netapi32", CharSet = CharSet.Auto,
        SetLastError = true),
        SuppressUnmanagedCodeSecurityAttribute]

        // The NetServerEnum API function lists all servers of the 
        // specified type that are visible in a domain.
        public static extern int NetServerEnum(
            string ServerNane, // must be null
            int dwLevel,
            ref IntPtr pBuf,
            int dwPrefMaxLen,
            out int dwEntriesRead,
            out int dwTotalEntries,
            int dwServerType,
            string domain, // null for login domain
            out int dwResumeHandle
            );

        //declare the Netapi32 : NetApiBufferFree method import
        [DllImport("Netapi32", SetLastError = true),
        SuppressUnmanagedCodeSecurityAttribute]

        // Netapi32.dll : The NetApiBufferFree function frees 
        // the memory that the NetApiBufferAllocate function allocates.         
        public static extern int NetApiBufferFree(IntPtr pBuf);

        //create a _SERVER_INFO_100 STRUCTURE
        [StructLayout(LayoutKind.Sequential)]
        public struct _SERVER_INFO_100
        {
            internal int sv100_platform_id;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string sv100_name;
        }

        public static List<string> GetNetworkComputerNames()
        {
            List<string> networkComputerNames = new List<string>();
            const int MAX_PREFERRED_LENGTH = -1;
            int SV_TYPE_WORKSTATION = 1;
            int SV_TYPE_SERVER = 2;
            IntPtr buffer = IntPtr.Zero;
            IntPtr tmpBuffer = IntPtr.Zero;
            int entriesRead = 0;
            int totalEntries = 0;
            int resHandle = 0;
            int sizeofINFO = Marshal.SizeOf(typeof(_SERVER_INFO_100));

            Console.WriteLine("All parameters is set...");

            try
            {
                int ret = NetServerEnum(null, 100, ref buffer,
                    MAX_PREFERRED_LENGTH,
                    out entriesRead,
                    out totalEntries, SV_TYPE_WORKSTATION |
                    SV_TYPE_SERVER, null, out
                    resHandle);
                //if the returned with a NERR_Success 
                //(C++ term), =0 for C#
                if (ret == 0)
                {
                    Console.WriteLine("Something went ok, entries: " + totalEntries);

                    //loop through all SV_TYPE_WORKSTATION and SV_TYPE_SERVER PC's
                    for (int i = 0; i < totalEntries; i++)
                    {
                        tmpBuffer = new IntPtr((int)buffer +
                                   (i * sizeofINFO));

                        Console.WriteLine("Buffer created: " + i);

                        //Have now got a pointer to the list of SV_TYPE_WORKSTATION and SV_TYPE_SERVER PC's
                        _SERVER_INFO_100 svrInfo = (_SERVER_INFO_100)
                            Marshal.PtrToStructure(tmpBuffer,
                                    typeof(_SERVER_INFO_100));


                        Console.WriteLine("Found: " + svrInfo.sv100_name);

                        //add the Computer name to the List
                        networkComputerNames.Add(svrInfo.sv100_name);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetNetworkComputerNames: " + ex.Message);
            }
            finally
            {
                //The NetApiBufferFree function frees the allocated memory
                NetApiBufferFree(buffer);
            }

            return networkComputerNames;
        }
    }

    public class MOTR_Admin
    {
        private string sSalt;
        private string sSessionID;
        private DateTime dateSessionSet;
        private int iSessionTimeout;
        private string sCurrentPath; //Current path
        private string sDriveSelected; //Which drive is selected
        private UNCAccess pUNC = new UNCAccess();
 
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        [Serializable]
        public struct EncryptedData
        {
            public byte[] ciphertext;
            public byte[] entropy;
        }

        private EncryptedData m_AdminPassword;

        #region ErrorHandling
        //Errorhandling for sending info
        public bool HasError { get; private set; }
        private string m_sErrorString;
        private void ClearError() { HasError = false; }
        public void SetErrorString(string sError)
        {
            HasError = true;
            m_sErrorString = sError;
        }
        public string GetErrorString()
        {
            HasError = false;
            return m_sErrorString;
        }
        #endregion

        public MOTR_Admin()
        {
            sSalt = "Please do not remove this line...";
            sSessionID = ""; //This the session that has admin rights atm...
            iSessionTimeout = 3600; //1 hour timeout

            //Set timeout to invalid at startup
            dateSessionSet = DateTime.Now.Subtract(TimeSpan.FromSeconds(iSessionTimeout - 1));

            ReadAdminPassword();
        }

        ~MOTR_Admin()
        {
            pUNC.NetUseDelete();
        }

        private bool ReadAdminPassword()
        {
            string baseFolder = MOTR_Settings.GetGlobalApplicationPath("config");

            if (File.Exists(baseFolder + "admin.ini") == false)
            {
                SetErrorString(@"Critical error! No adminpassword is set, please check the %programdata%\MOTRd\ for a admin.ini file\nAlternative delete all files in %programdata%\MOTRd\ to run inital setup again");
                return false;
            }

            using (var file = File.OpenRead(baseFolder + "admin.ini"))
            {
                var reader = new BinaryFormatter();
                m_AdminPassword = (EncryptedData)reader.Deserialize(file); // Reads the entire list.
                file.Close();
            }

            return true;
        }

        public void CreateAdminPassword(string sPassword)
        {
            string baseFolder = MOTR_Settings.GetGlobalApplicationPath("config");
            
            //We do not overwrite...
            if (File.Exists(baseFolder + "admin.ini"))
            {
                SetErrorString("Adminpassword already exists, cannot overwrite");
                return;
            }

            //Get the structed filled
            m_AdminPassword = GetCipher(sPassword);

            using (var file = File.OpenWrite(baseFolder + "admin.ini"))
            {
                var writer = new BinaryFormatter();
                writer.Serialize(file, m_AdminPassword); // Writes the struct
                file.Close();
            }
        }

        private EncryptedData GetCipher(string sInput)
        {
            // Data to protect. Convert a string to a byte[] using Encoding.UTF8.GetBytes().
            byte[] plaintext = Encoding.ASCII.GetBytes("stats"+sInput+sSalt);

            // Generate additional entropy (will be used as the Initialization vector)
            byte[] entropy = new byte[80];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(entropy);
            }

            byte[] ciphertext = ProtectedData.Protect(plaintext, entropy, DataProtectionScope.LocalMachine);

            EncryptedData pData = new EncryptedData();
            pData.ciphertext = ciphertext;
            pData.entropy = entropy;

            return pData;
            //return Encoding.ASCII.GetString(entropy)+","+ Encoding.ASCII.GetString(ciphertext);
        }

        public bool CheckAdminPassword(string sPassword, string sSession)
        {
            byte[] plaintext = ProtectedData.Unprotect(m_AdminPassword.ciphertext, m_AdminPassword.entropy, DataProtectionScope.LocalMachine);
            string sDecodedPassword = Encoding.ASCII.GetString(plaintext);

            //Check if the timeout has been reached
            if ((DateTime.Now - dateSessionSet).TotalSeconds >= iSessionTimeout)
                sSessionID = "";
            if(sSession.Length == 0)
            {
                SetErrorString("Session can not be empty while checking admin password");
                return false;
            }

            if ("stats" + sPassword + sSalt == sDecodedPassword)
            {
                dateSessionSet = DateTime.Now;
                sSessionID = sSession;
                ClearError();
                return true;
            }
            else
            {
                SetErrorString("Wrong admin password");
                return false;
            }
        }

        public bool IsSessionLoggedIn(string sSession)
        {
            //Check if the timeout has been reached
            if ((DateTime.Now - dateSessionSet).TotalSeconds >= iSessionTimeout)
                sSessionID = "";
            if (sSession.Length == 0)
                return false;

            if (sSessionID == sSession)
                return true;
            else
                return false;
        }

        //Returns the drives and network servers available (for directory browse)
        public ArrayList GetDrivesAndNetwork()
        {
            ArrayList aDrives = new ArrayList();

            //First store the local drives
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                DrivesAndNetwork strDrive = new DrivesAndNetwork();
                strDrive.sName = d.Name;
                strDrive.sType = d.DriveType.ToString();
                strDrive.bIsDrive = true;
                aDrives.Add(strDrive);
            }

            //Now list all the network servers
            List<string> networkComputers = NetworkInfo.GetNetworkComputerNames();
            foreach (string computerName in networkComputers)
            {
                //Console.WriteLine(computerName);
                DrivesAndNetwork strDrive = new DrivesAndNetwork();
                strDrive.sName = computerName;
                strDrive.sType = "Network server";
                strDrive.bIsDrive = false;
                aDrives.Add(strDrive);
            }

            return aDrives;
        }

        public ArrayList GetNetworkShareFoldersList(string serverName)
        {
            ArrayList shares = new ArrayList();

            //Split each and go back one
            string[] parts = serverName.Split('\\');

            //Could not happend if stated as \\server\path\
            if (parts.Length < 3)
            {
                SetErrorString(@"GetNetworkShareFoldersList did not get a \\server\path as expected");
                return shares;
            }

            string server = @"\\" + parts[2] + @"\";
            ShareCollection shi = ShareCollection.LocalShares;

            if (server != null && server.Trim().Length > 0)
            {
                shi = ShareCollection.GetShares(server);
                if (shi != null)
                {
                    foreach (Share si in shi)
                    {
                        //We only want the disks
                        if (si.ShareType == ShareType.Disk || si.ShareType == ShareType.Special)
                        {
                            DirectoryBrowse aDir = new DirectoryBrowse();
                            aDir.sPath = si.Root.FullName + @"\";
                            aDir.sName = si.NetName;
                            shares.Add(aDir);
                        }
                    }
                }
                else
                {
                    SetErrorString(@"Unable to enumerate the shares on " + server + " - Make sure the machine exists and that you have permission to access it");
                    return new ArrayList();
                }
            }
            return shares;
        }

        /*        public ArrayList GetNetworkShareFoldersList(string serverName)
                {
                    //List<string> shares = new List<string>();
                    ArrayList shares = new ArrayList();

                    try
                    {
                        // do not use ConnectionOptions to get shares from local machine
                        ConnectionOptions connectionOptions = new ConnectionOptions();
                        connectionOptions.Username = @"\filer";
                        connectionOptions.Password = "filer";
                        connectionOptions.Impersonation = ImpersonationLevel.Impersonate;

                        ManagementScope scope = new ManagementScope(serverName + "root\\CIMV2", connectionOptions);
                        scope.Connect();

                        ManagementObjectSearcher worker = new ManagementObjectSearcher(scope, new ObjectQuery("select Name from win32_share"));

                        foreach (ManagementObject share in worker.Get())
                        {
                            DirectoryBrowse aDir = new DirectoryBrowse();
                            aDir.sPath = serverName + share["Name"].ToString() + "\\";
                            aDir.sName = share["Name"].ToString();
                            shares.Add(aDir);
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Getnetworkshare: " + ex.Message);
                    }

                    return shares;
                }*/


        //Return an array based on the path
        public ArrayList GetDirectoryList()
        {
            ArrayList aFileList = new ArrayList();

            //Check if we have a \\xxx\ path selected
            if(sCurrentPath == sDriveSelected)
                if(sDriveSelected.IndexOf(@"\\") != -1)
                    return GetNetworkShareFoldersList(sCurrentPath);

            if (Directory.Exists(sCurrentPath))
            {
                DirectoryInfo[] dirArray = null;

                try
                {
                    dirArray = new DirectoryInfo(sCurrentPath)
                    .GetDirectories("*")
                    .ToArray();
                }
                catch (Exception ex) //Usally access denied, then return nothing...
                {
                    SetErrorString(@"GetDirectoryList exception: " + ex.Message);
                    //Console.WriteLine("Exception: " + ex.Message);
                    return new ArrayList();
                }

                //Check if we are going to add ".." to the top
                if (sCurrentPath != sDriveSelected)
                {
                    DirectoryBrowse aDots = new DirectoryBrowse();
                    aDots.sPath = "..";
                    aDots.sName = "..";
                    aFileList.Add(aDots);
                }

                foreach (DirectoryInfo sDir in dirArray)
                {
                    DirectoryBrowse aDir = new DirectoryBrowse();
                    aDir.sPath = sDir.FullName;
                    aDir.sName = sDir.Name.Substring(sDir.Name.LastIndexOf('\\') + 1);
                    aFileList.Add(aDir);
                }
            }

            return aFileList;
        }

        //Sets the drive selected and the current path
        public void SetBasePath(string sPath)
        {
            //Chech if we have a # in the name, if so, extract the username and passwords
            string sUNCUsername = "";
            string SUNCPassword = "";
            string sUNCDomain = "";
            if (sPath.IndexOf('#') != -1)
            {
                string[] sItems = sPath.Split('#');
                sPath = sItems[0];
                sUNCUsername = sItems[1]; //Later on add the domain feature
                SUNCPassword = sItems[2];
            }

            //If the path does not exists a \, then it is a network connection
            if (sPath.IndexOf('\\') == -1)
            {
                sPath = @"\\" + sPath + @"\";
                pUNC.NetUseDelete();
            }

            //Now connect to the networkdrive
            if (sPath.IndexOf(@"\\") != -1)
            {
                bool bLoggedIn = false;
                if (sUNCUsername.Length > 0)
                    bLoggedIn = pUNC.login(sPath + @"IPC$\", sUNCUsername, sUNCDomain, SUNCPassword);
                else
                    bLoggedIn = pUNC.login(sPath, sUNCUsername, sUNCDomain, SUNCPassword);

                if (!bLoggedIn)
                    SetErrorString(@"Could not log into " + sPath + " using username " + sUNCUsername);
            }

            sDriveSelected = sPath;
            sCurrentPath = sPath;
        }

        //Sets the path of where we are
        public void SetCurrentPath(string sDirectory)
        {
            //Go one directory up...
            if(sDirectory == "..")
            {
                //Remove the slash at the end if needed
                if (sCurrentPath.Substring(sCurrentPath.Length - 1) == "\\")
                    sCurrentPath = sCurrentPath.Substring(0, sCurrentPath.Length - 1);

                //Store if it is a UNC path
                bool isUNC = false;
                if (sCurrentPath.IndexOf(@"\\") != -1)
                    isUNC = true;

                //If it is UNC then handle it a little different                
                if(isUNC)
                {
                    //Split each and go back one
                    string[] parts = sCurrentPath.Split('\\');

                    //Starts with this
                    sCurrentPath = @"\\";
                    for (int i = 2; i < parts.Length - 1; i++)
                        sCurrentPath += parts[i] + @"\";
                }
                else //Normal path
                    sCurrentPath = Directory.GetParent(sCurrentPath).FullName;

                //Add the slash at the end if needed
                if (sCurrentPath.Substring(sCurrentPath.Length - 1) != "\\")
                    sCurrentPath += "\\";

                return;
            } //If ".."

            //It cannot contain \. in the filename
            if (sDirectory.IndexOf(@"\.") != -1 || sDirectory.IndexOf(@".\") != -1 ||
                sDirectory.IndexOf(@"/.") != -1 || sDirectory.IndexOf(@"./") != -1)
            {
                SetErrorString(@"Not allowed with . or combination of those in paths");
                return;
            }

            //Set the directory
            if (Directory.Exists(sCurrentPath + sDirectory))
            {
                ClearError();
                sCurrentPath += sDirectory + "\\";
            }
            else
                SetErrorString("Path " + sCurrentPath + sDirectory + " does not exists or user credentials is wrong");

        }
    }
}
