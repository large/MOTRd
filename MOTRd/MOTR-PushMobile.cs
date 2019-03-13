using System;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Firebase.NET;
using Firebase.NET.Contracts;
using Firebase.NET.Messages;
using Firebase.NET.Notifications;

namespace MOTRd
{
    //Class to handle payload
    public class Payload : System.Collections.Generic.Dictionary<string, string>, Firebase.NET.Contracts.IPayload {}

    class MOTR_PushMobile
    {
        public MOTR_PushMobile()
        {
        }

        ~MOTR_PushMobile()
        {
        }

        public static bool Update(string oldToken, string newToken)
        {
            //update oldToken with newToken in your database
            Console.WriteLine("Update PushID: " + oldToken + " with " + newToken);
            return true;
        }

        /// <summary>
        /// This method deletes registration token that is invalid and you should not try
        /// to push notifications any longer to it.
        /// </summary>
        public static bool Delete(string oldToken)
        {
            Console.WriteLine("DeletePushID: " + oldToken);
            //delete oldToken from your database
            return true;
        }

        public bool SendPush(string PushID)
        {
            //Set array of IDs, just one here :)
            string[] id = { PushID };

            var requestMessage = new RequestMessage
            {
                Body =
                {
                    RegistrationIds = id,
                    /*Notification = new CrossPlatformNotification
                    {
                        Title = "Important Notification",
                        Body = "This is a notification send from Firebase.NET"
                    },*/
                    Data = new Payload()
                    {
                            { "mobiledownload", "Available" },
                    }
                }
            };

            //your implemented functions that will be send to Firebase.NET as parameters
            Func<string, string, bool> updateFunc = new Func<string, string, bool>(Update);
            Func<string, bool> deleteFunc = new Func<string, bool>(Delete);

            string mysecrettoken = "AAAATEVUNGI:APA91bFP8O_MD7hpxpUeS8yNtAupv9FxXBvtipd9SeQGURKRJFHUod6VC4Hu8Bif4_3RJbht1EKvuOePGCxOtLa9UAlI6yup99vz9mOWcNWUXqmF2YxUt1H62AusDoM1Q2rhWcwHPbaH";

            PushNotificationService pushService = new PushNotificationService(mysecrettoken, updateFunc, deleteFunc);
            pushService.MAX_RETRIES = 5;
            ResponseMessage responseMessage = (ResponseMessage)pushService.PushMessage(requestMessage).Result;

            string error = "";
            if (responseMessage.Body != null)
            {
                for (int i = 0; i < responseMessage.Body.Results.Length; i++)
                {
                    if (responseMessage.Body.Results[i].RequestRetryStatus != PushMessageStatus.NULL)
                        Console.WriteLine(((PushMessageStatus)responseMessage.Body.Results[i].RequestRetryStatus).ToString());
                    error = responseMessage.Body.Results[i].Error != null ? ((ResponseMessageError)responseMessage.Body.Results[i].Error).ToString() : "";
                    if (error.Length == 0)
                        error = "None";
                    Console.WriteLine("Error: {0};  MessageId: {1};  RegistrationId: {2}", error, responseMessage.Body.Results[i].MessageId, responseMessage.Body.Results[i].RegistrationId);
                }
            }

            //Easy handle?
            if (error.Length != 0)
                return false;
            return true;
        }
    }


    /*    class MOTR_PushOnesignal
        {
            private OneSignalClient m_PushClient;

            public MOTR_PushOnesignal()
            {
                m_PushClient = new OneSignalClient("ZTRhM2Y5N2ItZmRkYS00YjIyLTk4NDgtNTQ2MWY0MTVmN2Yx");
                //this.CreatePushMessage("This is the text in c#");
            }

            ~MOTR_PushOnesignal()
            {
            }

            public void CreatePushMessage(string Text)
            {
                var options = new NotificationCreateOptions();

                Guid appIDGUID = Guid.Empty;
                try
                {
                    string ownerId = "e75c54b5-2ed5-42ec-8588-51eb169f0d8d";
                    appIDGUID = new Guid(ownerId);
                }
                catch
                {
                    // implement catch 
                }

                options.AppId = appIDGUID;
                options.IncludedSegments = new List<string> { "All" };
                options.Contents.Add(LanguageCodes.English, Text);

                //Send the shit
                NotificationCreateResult m_res = m_PushClient.Notifications.Create(options);
                return;
            }
        }*/
}
