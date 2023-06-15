using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

//Adapted from https://www.fluxbytes.com/csharp/tracert-implementation-in-c/
namespace Tracert
{
    public class TracertEntry
    {
        public int HopID { get; set; }
        public string Address { get; set; }
        public string Hostname { get; set; }
        public long ReplyTime { get; set; }
        public IPStatus ReplyStatus { get; set; }
        public override string ToString()
        {
            NetworkTool.MainWindow mw = (NetworkTool.MainWindow)Application.Current.MainWindow;
            if (ReplyStatus == IPStatus.TimedOut)
            {
                mw.tracertTOCnt += 1;
            }
            else mw.tracertTOCnt = 0; //Reset the timeout count every time we get a valid reply

            return string.Format("{0}  |  {1}  |  {2}",
                HopID,
                string.IsNullOrEmpty(Hostname) ? Address : Hostname + "[" + Address + "]",
                ReplyStatus == IPStatus.TimedOut ? "Request Timed Out." : ReplyTime.ToString() + " ms"
                );
        }
    }
}