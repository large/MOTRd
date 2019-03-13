using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using LiteDB;

namespace MOTRd
{
    public class UserStructDb
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public string Username { get; set; }
        public byte[] ciphertext { get; set; }
        public byte[] entropy { get; set; }
    }

    //Used in mobileregistration, connected to the user, independent of sessions
    public class Mobileunit
    {
        public int Id { get; set; }
        public DateTime Added { get; set; }
        public string DeviceID { get; set; }
        public int UserID { get; set; } //Same as Id in UserStructDb
        public string Displayname { get; set; }
        public string Model { get; set; } //Everything under this line is optional. User might share this info.
        public string Manufacturer { get; set; }
        public string Devicename { get; set; }
        public string Platform { get; set; }
        public string Version { get; set; }
        public string Idiom { get; set; }
        public string PushID { get; set; }
    }

    public class MOTR_Users
    {
        private string sUserSalt;
        LiteDatabase m_db;

        public MOTR_Users()
        {
            //Opening the database
            string sGlobalPath = MOTR_Settings.GetGlobalApplicationPath();
            m_db = new LiteDatabase(sGlobalPath + @"\Userdata.db");

            sUserSalt = "Also users needs salt :)";
        }

        //Writes to file when class is destroyed
        ~MOTR_Users()
        {
            m_db.Dispose();
        }

        //Returns the number of users available
        public int Count() { return m_db.GetCollection<UserStructDb>("users").Count(); }

        //=====================================
        //Public functions here
        public bool UsernameAndPasswordMatch(string sUser, string sPass)
        {
            LiteCollection<UserStructDb> aDBValues = m_db.GetCollection<UserStructDb>("users");
            UserStructDb results = aDBValues.FindOne(x => x.Username == sUser);
            if (results == null)
                return false;

            byte[] plaintext = ProtectedData.Unprotect(results.ciphertext, results.entropy, DataProtectionScope.LocalMachine);
            string sDecodedPassword = Encoding.ASCII.GetString(plaintext);

            //Password is case sensitive and must match exact...
            if (sDecodedPassword == sPass + sUserSalt)
                return true;
            else
                return false;
        }

        //Returns an array of users
        public ArrayList GetUserArray()
        {
            ArrayList m_list = new ArrayList();
            LiteCollection<UserStructDb> aDBValues = m_db.GetCollection<UserStructDb>("users");
            foreach (UserStructDb item in aDBValues.FindAll())
            {
                m_list.Add(item.Id.ToString());
                m_list.Add(item.Username);
            }
            return m_list;
        }

        //Changes the password of a user
        public bool ChangePassword(int nID, string sPass)
        {
            if (sPass.Length == 0)
                return false;

            LiteCollection<UserStructDb> aDBValues = m_db.GetCollection<UserStructDb>("users");
            UserStructDb results = aDBValues.FindOne(x => x.Id == nID);
            if (results == null)
                return false;

            //Create new password
            byte[] plaintext = Encoding.ASCII.GetBytes(sPass + sUserSalt);

            // Generate additional entropy (will be used as the Initialization vector)
            byte[] entropy = new byte[15];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                rng.GetBytes(entropy);

            byte[] ciphertext = ProtectedData.Protect(plaintext, entropy, DataProtectionScope.LocalMachine);

            results.entropy = entropy;
            results.ciphertext = ciphertext;
            aDBValues.Update(results);

            return true;
        }

        //Removes a user based on the ID
        public bool RemoveUser(int nID)
        {
            LiteCollection<UserStructDb> aDBValues = m_db.GetCollection<UserStructDb>("users");
            UserStructDb results = aDBValues.FindOne(x => x.Id == nID);
            if (results == null)
                return false;

            //If there is only 1 user left, then ignore the delete
            if (aDBValues.Count() == 1)
                return false;

            //Remove the data at given ID
            aDBValues.Delete(results.Id);
            return true;
        }

        //Store the userdatabase
        public bool AddUserName(string sUser, string sPass)
        {
            //Do not add zero length items
            if (sUser.Length == 0 || sPass.Length == 0)
                return false;

            //Encrypt password
            byte[] plaintext = Encoding.ASCII.GetBytes(sPass + sUserSalt);
            byte[] entropy = new byte[15];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(entropy);
            }
            byte[] ciphertext = ProtectedData.Protect(plaintext, entropy, DataProtectionScope.LocalMachine);

            UserStructDb aUser = new UserStructDb
            {
                Username = sUser,
                ciphertext = ciphertext,
                entropy = entropy,
                DisplayName = sUser
            };

            LiteCollection<UserStructDb> aDBValues = m_db.GetCollection<UserStructDb>("users");

            // Use Linq to query documents
            var results = aDBValues.FindOne(x => x.Username == sUser);
            if (results != null) //If username exists, return
                return false; //Already exist
            else //Add new user
            {
                aDBValues.EnsureIndex(x => x.Username);
                aDBValues.Insert(aUser);
            }

            return true;
        }

        //Return the ID based on the username
        public int GetUserID(string Username)
        {
            LiteCollection<UserStructDb> aDBValues = m_db.GetCollection<UserStructDb>("users");
            var results = aDBValues.FindOne(x => x.Username == Username);
            if (results != null) //If username exists, return id
                return results.Id;
            return -1; //Not found, no good
        }

        //Returns the username based on the ID
        public string GetUsername(int UserID)
        {
            LiteCollection<UserStructDb> aDBValues = m_db.GetCollection<UserStructDb>("users");
            var results = aDBValues.FindOne(x => x.Id == UserID);
            if (results != null) //If id exists, return username
                return results.Username;
            return ""; //Not found, no good
        }

        //////////////////////////////////////////////////////////////////////////
        // Mobile registration and handling
        //////////////////////////////////////////////////////////////////////////

        //Checks if a mobile with that device exists
        public bool IsMobileRegistered(string DeviceID, int UserID)
        {
            LiteCollection<Mobileunit> aDBValues = m_db.GetCollection<Mobileunit>("mobile");
            var results = aDBValues.FindOne(x => x.DeviceID == DeviceID && x.UserID == UserID);
            if (results != null) //If id exists, return true
                return true;
            return false;
        }
        public string MobileDisplayname(string DeviceID, int UserID)
        {
            LiteCollection<Mobileunit> aDBValues = m_db.GetCollection<Mobileunit>("mobile");
            var results = aDBValues.FindOne(x => x.DeviceID == DeviceID && x.UserID == UserID);
            if (results != null) //If id exists, return true
                return results.Displayname;
            return "";
        }
        public int GetMobileID(string DeviceID, int UserID)
        {
            LiteCollection<Mobileunit> aDBValues = m_db.GetCollection<Mobileunit>("mobile");
            var results = aDBValues.FindOne(x => x.DeviceID == DeviceID && x.UserID == UserID);
            if (results != null) //If id exists, return true
                return results.Id;
            return -1;
        }
        //Register a device on a user
        public bool MobileRegister(Mobileunit mobileunit)
        {
            //No love if these variables are no good
            if (mobileunit.DeviceID.Length == 0 ||
                mobileunit.Displayname.Length == 0 ||
                GetUsername(mobileunit.UserID).Length == 0)
                return false;

            //Add database access
            LiteCollection<Mobileunit> aDBValues = m_db.GetCollection<Mobileunit>("mobile");

            //Set the time for adding
            mobileunit.Added = DateTime.Now;

            //Check if it exists already
            //Add new mobile to register if not
            if (IsMobileRegistered(mobileunit.DeviceID, mobileunit.UserID))
            {
                var results = aDBValues.FindOne(x => x.DeviceID == mobileunit.DeviceID);
                mobileunit.Id = results.Id;
                aDBValues.Update(mobileunit);
            }
            else
                aDBValues.Insert(mobileunit);

            return true;
        }
        public bool MobileRegister(string AppId, int UserID, string Displayname)
        {
            Mobileunit mobileunit = new Mobileunit
            {
                DeviceID = AppId,
                UserID = UserID,
                Displayname = Displayname,
            };
            return MobileRegister(mobileunit);
        }

        //Return a list of mobiles for a user
        public ArrayList MobileList(int UserID)
        {
            ArrayList arrayList = new ArrayList();
            LiteCollection<Mobileunit> aDBValues = m_db.GetCollection<Mobileunit>("mobile");
            var results = aDBValues.Find(x => x.UserID == UserID);
            foreach (Mobileunit item in results)
                if (item != null)
                {
                    arrayList.Add(item.Id);
                    arrayList.Add(item.Displayname);
                }
            return arrayList;
        }

        //Set the PushID to the profile (registered after, either FCM or Azure or whatever...)
        public bool SetPushID(string DeviceID, int UserID, string PushID)
        {
            LiteCollection<Mobileunit> aDBValues = m_db.GetCollection<Mobileunit>("mobile");
            var results = aDBValues.FindOne(x => x.DeviceID == DeviceID);
            if (results != null) //If id exists, set the PushID
            {
                results.PushID = PushID;
                aDBValues.Update(results);
                return true;
            }
            return false;
        }

        //Return the PushID based on DeviceID and User
        public string GetPushID(int Id, int UserID)
        {
            LiteCollection<Mobileunit> aDBValues = m_db.GetCollection<Mobileunit>("mobile");
            var results = aDBValues.FindOne(x => x.Id == Id);
            if (results != null) //If id exists, set the PushID
                return results.PushID;
            return "";
        }
    }
}
