using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOTRd
{
    [Serializable]
    public class WebSocketCommandClass
    {
        public WebSocketCommandClass()
        {
            //sessionid = "";
            command = "";
            parameter = "";
        }
        //public string sessionid { get; set; }
        public string command { get; set; }
        public string parameter { get; set; }
    }

    [Serializable]
    public class WebsocketSendClass
    {
        public WebsocketSendClass()
        {
            command = "";
            count = 0;
            aArray = null;
        }
        public string command { get; set; }
        public int count { get; set; }
        public ArrayList aArray { get; set; }
    }
}
