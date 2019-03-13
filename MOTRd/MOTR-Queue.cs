using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Management;

namespace MOTRd
{
    enum QueueStatus
    {
        RUNNING,
        NOT_RUNNING,
        FINISHEDANDFAIL,
        FINISHED
    }

    struct QueueStruct
    {
        public long nQueueID;
        public string sSource; //FileName
        public string sDestination; //FileName
        public string sHandbrakeProfile; //Profile for convertion
        public string sDisplayName; //What the queue is called in the list
        public string sDisplayDirectory; //Which directory
        public string sDisplayPath; //Physical path under the drive
        public string sPath;
        public QueueStatus nStatus; //Status code of the running, 0 = not running, 1 = running, 2 = finished, 3 = finished and fail
        public int iProcentage;
        public string sETA;
        public DateTime dateRunning; //Time and date for when the item was set to run
        public ArrayList aProcessOutput; //Temporary, not stored in file?
    }
    struct QueueStructLight : IComparable
    {
        public long nQueueID;
        public string sDisplayName;
        public string sDisplayDirectory;
        public QueueStatus nStatus;
        public string sDisplayStatus;
        public string sHandbrakeProfile;
        public int iProcentage;
        public string sETA;

        public int CompareTo(object obj)
        {
            QueueStructLight that = (QueueStructLight)obj;

            if (this.nStatus < that.nStatus)
                return -1;
            if (this.nStatus > that.nStatus)
                return 1;

            return 0;
        }
    }

    struct ProcessRunning
    {
        public Process objProcess; //Process running on this struct
        public long nQueueID; //The ID of the queue this process belongs
    }

    public class QueueIDEventArg : EventArgs
    {
        public long nQueueID { get; set; }
        public int iProcentage { get; set; }
        public string sETA { get; set; }
    }

    public class MOTR_Queue : IDisposable
    {
        //This number is increasing every time a new item is added
        private long lQueueNumber;
        private ArrayList aQueue;
        static object _lockObject = new object();
        private ArrayList aProcessRunning;
        private bool disposed = false;
        static MOTR_Queue pMaster = null;
        public MOTR_Convertprofiles convertprofiles;

        public event EventHandler OnQueueUpdate;
        public event EventHandler<QueueIDEventArg> OnQueueProcentage;
        //public event EventHandler<QueueIDEventArg> OnQueueExecute;

        public MOTR_Queue()
        {
            lQueueNumber = 0; //Could be anything, an unique identifier for a job running
            aQueue = new ArrayList();
            aProcessRunning = new ArrayList();
            pMaster = this;

            //Convert profiles variable for returning what we support
            convertprofiles = new MOTR_Convertprofiles();
            string test = convertprofiles.GetProfiles();
            string test2 = convertprofiles.GetDescription(5);
            string test3 = convertprofiles.GetDescription("Normal");
            ArrayList test5 = convertprofiles.GetProfilesArray();
        }

        ~MOTR_Queue()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Manual release of managed resources.
                    ClearProcessListAndClean();
                }

                // Release unmanaged resources.
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        //Add an item to the queue
        public void Add(string sDisplayName, string sDisplayDirectory, string sDisplayPath, string _Path, string _Source, string _Destination, string _Profile, bool bAtTop = false)
        {
            QueueStruct sQueue = new QueueStruct();
            sQueue.nQueueID = lQueueNumber;
            sQueue.sDisplayName = sDisplayName;
            sQueue.sDisplayDirectory = sDisplayDirectory;
            sQueue.sDisplayPath = sDisplayPath;
            sQueue.sPath = _Path;
            sQueue.sSource = _Source;
            sQueue.sDestination = _Destination;
            sQueue.sHandbrakeProfile = _Profile;
            sQueue.iProcentage = -1;
            sQueue.sETA = "(Waiting)";
            sQueue.nStatus = QueueStatus.NOT_RUNNING;

            ArrayList aList = new ArrayList(); ;
            sQueue.aProcessOutput = aList;

            //Man må legge sammen .sPath + .sSource eller .sDestination for full path. sDisplayPath er bare visningsnavn (fullpath - drive)...

            //Now add the item to the list, bottom or top :)
            if (!bAtTop)
                aQueue.Add(sQueue);
            else
                aQueue.Insert(0, sQueue); //Add first, nStatus sort them out :)

            //Number always increase! :)
            lQueueNumber++;

            //Now check the queue
            CheckNextItemInQueue();
        }

        //Check if the queue has a running process, if not start the first
        private bool CheckNextItemInQueue()
        {
            //Idle item is set to -1 until it is executed
            long nFirstIdleItem = -1;
            for (int i = 0; i < aQueue.Count; i++)
            {
                QueueStruct sStruct = (QueueStruct)aQueue[i];

                //If we found a running, then exit
                if (sStruct.nStatus == QueueStatus.RUNNING)
                    return false;

                //Set the idle item number
                if (sStruct.nStatus == QueueStatus.NOT_RUNNING && nFirstIdleItem == -1)
                    nFirstIdleItem = sStruct.nQueueID;
            }

            //If there is an idle item, then execute it right away
            if (nFirstIdleItem != -1)
            {
                this.ExecuteQueue(nFirstIdleItem);
                return true;
            }
            return false;
        }

        //Remove a process based on the queueid
        private void KillProcessByQueueID(long nQueueID)
        {
            for (int i = 0; i < aProcessRunning.Count; i++)
            {
                ProcessRunning structProcess = (ProcessRunning)aProcessRunning[i];
                if (structProcess.nQueueID == nQueueID)
                {
                    structProcess.objProcess.OutputDataReceived -= CaptureOutput;
                    structProcess.objProcess.ErrorDataReceived -= CaptureError;
                    structProcess.objProcess.Exited -= Process_Exited;

                    Debug.WriteLine("Killing pid: " + structProcess.objProcess.Id);
                    structProcess.objProcess.Kill();
                    structProcess.objProcess.Close();
                    aProcessRunning.RemoveAt(i);
                    return;
                }
            }
        }


        //Called before exiting the class
        public void ClearProcessListAndClean()
        {
            for (int i = 0; i < aProcessRunning.Count; i++)
            {
                ProcessRunning structProcess = (ProcessRunning)aProcessRunning[i];
                if (!structProcess.objProcess.HasExited)
                {
                    structProcess.objProcess.OutputDataReceived -= CaptureOutput;
                    structProcess.objProcess.ErrorDataReceived -= CaptureError;
                    structProcess.objProcess.Exited -= Process_Exited;

                    Debug.WriteLine("Killing pid: " + structProcess.objProcess.Id);
                    structProcess.objProcess.Kill();
                }
                structProcess.objProcess.Close();
            }

            //Now we are finished
            aProcessRunning.Clear();
        }


        //Returns the queue for display in front
        public ArrayList GetQueue()
        {
            ArrayList aTempList = new ArrayList();

            //Loop through
            for (int i = 0; i < aQueue.Count; i++)
            {
                QueueStructLight aLight = new QueueStructLight();
                aLight.nQueueID = ((QueueStruct)aQueue[i]).nQueueID;
                aLight.nStatus = ((QueueStruct)aQueue[i]).nStatus;
                switch(aLight.nStatus)
                {
                    case QueueStatus.FINISHED:
                        aLight.sDisplayStatus = "Finished";
                        break;
                    case QueueStatus.FINISHEDANDFAIL:
                        aLight.sDisplayStatus = "Finished and failed";
                        break;
                    case QueueStatus.NOT_RUNNING:
                        aLight.sDisplayStatus = "Not running";
                        break;
                    case QueueStatus.RUNNING:
                        aLight.sDisplayStatus = "Running";
                        break;
                }
                aLight.sDisplayDirectory = ((QueueStruct)aQueue[i]).sDisplayDirectory;
                aLight.sDisplayName = ((QueueStruct)aQueue[i]).sDisplayName;
                aLight.sHandbrakeProfile = ((QueueStruct)aQueue[i]).sHandbrakeProfile;
                aLight.iProcentage = ((QueueStruct)aQueue[i]).iProcentage;
                aLight.sETA = ((QueueStruct)aQueue[i]).sETA;
                aTempList.Add(aLight);
            }

            //Now sort the bastard
            aTempList.Sort();

            return aTempList;
        }

        //Get the array id-number of a session
        private int ItemByQueueID(long nQueueID)
        {
            //Not valid
            if (nQueueID == -1)
                return -1;

            for (int i = 0; i < aQueue.Count; i++)
            {
                long nQID = ((QueueStruct)aQueue[i]).nQueueID;
                if (nQID == nQueueID)
                    return i;
            }

            //Not found
            return -1;
        }

        //Return an item of the queue (selected by user)
        public ArrayList GetQueueByID(long nQueueID)
        {
            //ArrayList aTempList = new ArrayList();

            int nID = ItemByQueueID(nQueueID);
            if (nID == -1)
                return new ArrayList();

            ArrayList aTempArray = new ArrayList();
            QueueStruct aStruct = (QueueStruct)aQueue[nID];
            aTempArray.Add(aStruct.sDisplayName);           //0
            aTempArray.Add(aStruct.sDisplayDirectory);      //1
            aTempArray.Add(aStruct.sDisplayPath);           //2
            aTempArray.Add(aStruct.sSource);                //3
            aTempArray.Add(aStruct.sDestination);           //4
            aTempArray.Add(aStruct.sHandbrakeProfile);      //5
            aTempArray.Add(aStruct.nStatus);                //6
            aTempArray.Add(aStruct.iProcentage);            //7
            aTempArray.Add(aStruct.sETA);                   //8
            aTempArray.Add(aStruct.nQueueID);               //9
            aTempArray.Add(aStruct.aProcessOutput);         //10
            return aTempArray;
        }


        /// <summary>
        /// Function that catch all the process output and report back to the system
        /// </summary>
        static void CaptureOutput(object sender, DataReceivedEventArgs e)
        {
            lock(_lockObject)
            {
                Process objProcess = (Process)sender;
                if (e.Data != null)
                {
                    ShowOutput(objProcess.Id + " - " + e.Data, ConsoleColor.Green);
                    pMaster.AddOutputToProcess(objProcess, e.Data);
                }
            }
        }

        static void CaptureError(object sender, DataReceivedEventArgs e)
        {
            lock (_lockObject)
            {
                Process objProcess = (Process)sender;
                if (e.Data != null)
                {
                    ShowOutput(objProcess.Id + " - ERROR: " + e.Data, ConsoleColor.Red);
                    pMaster.AddOutputToProcess(objProcess, e.Data);
                }
            }
        }

        private void AddOutputToProcess(Process objProcess, string sData)
        {
            //Check the data for Regexp stats (procentage and time left)
            //Regex x = new Regex(@"[,] (.*) [%] .*[ETA ](.*)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            Regex x = new Regex(@"[,] (?<procentage>.*?)\.\d\d [%] .*[ETA] (?<eta>.*?)\)", RegexOptions.IgnoreCase);
            MatchCollection mc = x.Matches(sData);

            //If nothing was found, try the rar-way
            if(mc.Count == 0)
            {
                Regex x2 = new Regex(@"\b(?<procentage>\d+)(?:\%)$(?<eta>)", RegexOptions.IgnoreCase);
                mc = x2.Matches(sData);
            }

            for (int i=0;i<aProcessRunning.Count;i++)
            {
                ProcessRunning structProcess = (ProcessRunning)aProcessRunning[i];
                if(structProcess.objProcess == objProcess)
                {
                    for(int o=0;o<aQueue.Count;o++)
                    {
                        QueueStruct structQueue = (QueueStruct)aQueue[o];
                        if (structQueue.nQueueID == structProcess.nQueueID)
                        {
                            if(mc.Count==0) //Handbrake output not found, just add data
                                structQueue.aProcessOutput.Add(sData);
                            else //Now we got handbreak data, parse and extract, then update if nessesary
                            {
                                int iOldProcentage = structQueue.iProcentage;
                                structQueue.iProcentage = Convert.ToInt32(mc[0].Groups["procentage"].Value);
                                structQueue.sETA = mc[0].Groups["eta"].Value;
                                aQueue.RemoveAt(o);
                                aQueue.Insert(o, structQueue);

                                //If the procentage has change, then trigger en update
                                if (iOldProcentage < structQueue.iProcentage)
                                {
                                    QueueIDEventArg eventArg = new QueueIDEventArg();
                                    eventArg.iProcentage = structQueue.iProcentage;
                                    eventArg.nQueueID = structQueue.nQueueID;
                                    eventArg.sETA = structQueue.sETA;
                                    this.QueueProcentageUpdate(eventArg);
                                }
                            }
                            break;
                        }
                    }
                    break;
                }
            }
        }

        static void ShowOutput(string data, ConsoleColor color)
        {
            if (data != null)
            {
                ConsoleColor oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                //Console.WriteLine("Received: {0}", data);
                Console.ForegroundColor = oldColor;
            }
        }


        // Handle Exited event and display process information.
        private void Process_Exited(object sender, System.EventArgs e)
        {
            Process objProcess = (Process)sender;
            //Console.WriteLine(objProcess.Id +": Exit time:    {0} - " +
            //    "Exit code:    {1}", objProcess.ExitTime, objProcess.ExitCode);
            objProcess.Refresh();
            objProcess.WaitForExit();
            this.SetQueueExitInformation(objProcess);
            this.RemoveProcessFromQueue(objProcess);
            objProcess.Dispose(); //Drep denne jævelen
            
        }

        //Update the queue based on process
        private void SetQueueExitInformation(Process objProcess)
        {
            for (int i = 0; i < aProcessRunning.Count; i++)
            {
                ProcessRunning structProcess = (ProcessRunning)aProcessRunning[i];
                if (structProcess.objProcess == objProcess)
                {
                    for (int o = 0; o < aQueue.Count; o++)
                    {
                        QueueStruct structQueue = (QueueStruct)aQueue[o];
                        if (structQueue.nQueueID == structProcess.nQueueID)
                        {
                            structQueue.nStatus = QueueStatus.FINISHED;
                            string sETA = "Finished " + DateTime.Now.ToString() + PrettyDurationFormat(DateTime.Now - structQueue.dateRunning);

                            if (objProcess.ExitCode != 0)
                            {
                                structQueue.nStatus = QueueStatus.FINISHEDANDFAIL;
                                sETA = "Finished, with errors " + DateTime.Now.ToString() + PrettyDurationFormat(DateTime.Now - structQueue.dateRunning); ;
                            }
                            structQueue.sETA = sETA;
                            aQueue.RemoveAt(o);
                            aQueue.Insert(o, structQueue);

                            //Update queue data everywhere
                            QueueIDEventArg eventArg = new QueueIDEventArg();
                            eventArg.iProcentage = 100;
                            eventArg.nQueueID = structQueue.nQueueID;
                            eventArg.sETA = sETA;
                            this.QueueProcentageUpdate(eventArg);

                            //Last check the next item in queue, if executed update the queue
                            CheckNextItemInQueue();

                            //Update the new queue
                            this.QueueUpdate(EventArgs.Empty);
                        }
                    }
                }
            }
        }

        private string PrettyDurationFormat(TimeSpan span)
        {
            if (span == TimeSpan.Zero) return "0 minutes";

            var sb = new StringBuilder();
            if (span.Days > 0)
                sb.AppendFormat("{0} day{1} ", span.Days, span.Days > 1 ? "s" : String.Empty);
            if (span.Hours > 0)
                sb.AppendFormat("{0} hour{1} ", span.Hours, span.Hours > 1 ? "s" : String.Empty);
            if (span.Minutes > 0)
                sb.AppendFormat("{0} minute{1}", span.Minutes, span.Minutes > 1 ? "s" : String.Empty);

            //Task to seconds, just ignore the duration
            if (sb.ToString().Length == 0)
                return "";

            return " (" + sb.ToString() + ")";
        }

        private bool RemoveProcessFromQueue(Process hProcess)
        {
            for (int i = 0; i < aProcessRunning.Count; i++)
            {
                ProcessRunning structProcess = (ProcessRunning)aProcessRunning[i];
                if (hProcess == structProcess.objProcess)
                {
                    aProcessRunning.RemoveAt(i);
                    Console.WriteLine("Processes left: " + aProcessRunning.Count);
                    return true;
                }
            }
            return false;
        }


        private bool StartBackgroundProcess(string sPath, string sArgs, string sWorkingDir, long nQueueID)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(sPath, sArgs);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.WorkingDirectory = sWorkingDir;

            try
            {
                Process process = new Process();
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += CaptureOutput;
                //process.OutputDataReceived += (sender, e) => CaptureOutput(sender, e, this);
                process.ErrorDataReceived += CaptureError;
                //process.ErrorDataReceived += (sender, e) => CaptureError(sender, e, this);
                process.Exited += new EventHandler(Process_Exited);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                ProcessRunning structProcess = new ProcessRunning();
                structProcess.nQueueID = nQueueID;
                structProcess.objProcess = process;
                aProcessRunning.Add(structProcess);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error background process: " + ex.Message);
                return false;
            }
        }

        //Function that fires away a convert with handbreak
        private bool ExecuteQueue(long nQueueID)
        {
            int nItem = ItemByQueueID(nQueueID);
            if (nItem == -1)
                return false;
            QueueStruct aStruct = (QueueStruct)aQueue[nItem];

            //We don't run twice...
            if (aStruct.nStatus == QueueStatus.RUNNING)
                return false;

            //Setting the parameters
            string baseFolder = MOTR_Settings.GetGlobalApplicationPath("tools");
            string sExec = MOTR_Settings.GetExecuteToolPath("handbreak");
            string sParams = "--optimize --preset \"" + aStruct.sHandbrakeProfile + "\" --input \"" + aStruct.sPath + aStruct.sSource + "\" --output \"" + aStruct.sPath + aStruct.sDestination + "\"";
            aStruct.dateRunning = DateTime.Now;
            aStruct.nStatus = QueueStatus.RUNNING;

            //extension = extension.Substring(extension.Length - 4);
            string sExtension = aStruct.sSource.Substring(aStruct.sSource.Length - 4);
            if (sExtension.ToUpper() == ".RAR")
            {
                sExec = MOTR_Settings.GetExecuteToolPath("unrar");
                sParams = "x -y -p- " + aStruct.sPath + aStruct.sSource;
            }

            //Console.WriteLine("Exec: " + sExec + " " + sParams);

            //Now start the background process, if it fails change status of the queue
            if(!StartBackgroundProcess(sExec, sParams, aStruct.sPath, aStruct.nQueueID))
            {
                aStruct.nStatus = QueueStatus.FINISHEDANDFAIL;
                aStruct.sETA = "Background could not be executed, please update tools";
            }

            //Update the current item
            aQueue.RemoveAt(nItem);
            aQueue.Insert(nItem, aStruct);

            return true;
        }

        //Queuemanagement
        public void Run(long nQueueID)
        {
            int nItem = ItemByQueueID(nQueueID);
            if (nItem == -1)
                return;

            //Get struct and check status, return if running
            QueueStruct aStruct = (QueueStruct)aQueue[nItem];
            if (aStruct.nStatus != QueueStatus.RUNNING)
                this.ExecuteQueue(nQueueID);
        }

        //QueueHandling ++++ 
        public void QueueManangement(long nQueueID, string sCommand)
        {
            //Remove all the items that are "finished"
            if(sCommand.ToUpper() == "CLEAR-FINISHED")
            {
                for (int i=aQueue.Count-1; i>=0; i--)
                {
                    QueueStruct aStructTemp = (QueueStruct)aQueue[i];
                    if (aStructTemp.nStatus == QueueStatus.FINISHED ||
                        aStructTemp.nStatus == QueueStatus.FINISHEDANDFAIL)
                        aQueue.RemoveAt(i);
                }
                return;
            }

            //Stop all the running processes (set them into the queue again)
            if (sCommand.ToUpper() == "STOP-ALL-RUNNING")
            {
                ClearProcessListAndClean();
                for(int i=0;i<aQueue.Count;i++)
                {
                    QueueStruct aStructTemp = (QueueStruct)aQueue[i];
                    if(aStructTemp.nStatus == QueueStatus.RUNNING)
                    {
                        aStructTemp.nStatus = QueueStatus.NOT_RUNNING;
                        aStructTemp.iProcentage = 0;
                        aStructTemp.sETA = "";
                        aQueue.RemoveAt(i);
                        aQueue.Insert(i, aStructTemp);
                    }
                }
                return;
            }

            if (sCommand.ToUpper() == "REMOVE-ALL")
            {
                ClearProcessListAndClean();
                aQueue.Clear();
                return;
            }

            //Since we need the item to move objecs, we bail out now...
            int nItem = ItemByQueueID(nQueueID);
            if (nItem == -1)
                return;

            //Get struct and check status, return if running
            QueueStruct aStruct = (QueueStruct)aQueue[nItem];


            //Used in the queuemanagement-running, needs to stop one process and then handle 
            if (sCommand.ToUpper() == "REMOVE-RUNNING" ||
                sCommand.ToUpper() == "STOP-RUNNING") 
            {
                KillProcessByQueueID(nQueueID);
                CheckNextItemInQueue();

                //Just remove it, if the item 
                if (sCommand.ToUpper() == "REMOVE-RUNNING")
                    aQueue.RemoveAt(nItem);
                else
                {
                    QueueStruct aStructCheck = (QueueStruct)aQueue[nItem];
                    aStructCheck.nStatus = QueueStatus.NOT_RUNNING;
                    aStructCheck.iProcentage = -1;
                    aStructCheck.sETA = "(Waiting)";
                    aQueue.RemoveAt(nItem);
                    aQueue.Insert(nItem, aStructCheck);
                }
                return;
            }


            //Rest of the commands are only for non running queue items...
            if (aStruct.nStatus != QueueStatus.NOT_RUNNING)
                return;

            //Now handle the move of the item
            if (sCommand.ToUpper() == "REMOVE")
            {
                aQueue.RemoveAt(nItem);
                return;
            }
            if (sCommand.ToUpper() == "MOVE-TOP")
            {
                aQueue.RemoveAt(nItem);
                aQueue.Insert(0, aStruct);
                return;
            }
            if (sCommand.ToUpper() == "MOVE-BOTTOM")
            {
                aQueue.RemoveAt(nItem);
                aQueue.Insert(aQueue.Count, aStruct);
                return;
            }
            if (sCommand.ToUpper() == "MOVE-UP")
            {
                //Check if we are at the top
                if (nItem == 0)
                    return;

                //Loop back to see if there are others that are not running, insert in front of
                for(int i=nItem-1;i>=0;i--)
                {
                    QueueStruct aStructCheck = (QueueStruct)aQueue[i];
                    if(aStructCheck.nStatus == QueueStatus.NOT_RUNNING)
                    {
                        aQueue.RemoveAt(nItem);
                        aQueue.Insert(i, aStruct);
                        return;
                    }
                }
            }
            if (sCommand.ToUpper() == "MOVE-DOWN")
            {
                //Check if we are at the top
                if (nItem == aQueue.Count-1)
                    return;

                //Loop back to see if there are others that are not running, insert in front of
                for (int i = nItem+1 ; i < aQueue.Count; i++)
                {
                    QueueStruct aStructCheck = (QueueStruct)aQueue[i];
                    if (aStructCheck.nStatus == QueueStatus.NOT_RUNNING)
                    {
                        aQueue.RemoveAt(nItem);
                        aQueue.Insert(i, aStruct);
                        return;
                    }
                }
            }


        }
        //Triggers an event to the MOTR-Webserver class that updates the specified queue for procentage
        public void QueueProcentageUpdate(QueueIDEventArg e)
        {
            OnQueueProcentage?.Invoke(this, e);
        }

        public void QueueUpdate(EventArgs e)
        {
            OnQueueUpdate?.Invoke(this, e);
        }
    }
}
