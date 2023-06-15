using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using Tracert;
using System.Diagnostics;

namespace NetworkTool
{
    class NetworkLogic
    {
        //Think of Lambda => as a one time use method, we don't call from elsewhere

        private MainWindow mainWindow;
        private static readonly HttpClient client = new HttpClient();
        private bool isBadIP = false;
        private NetworkInterface nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(i =>
                i.NetworkInterfaceType != NetworkInterfaceType.Loopback && i.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
        private long oldBytesOut = 1;
        private long oldBytesIn = 1;


        /**
         * 
         **/
        public NetworkLogic()
        {
            this.mainWindow = (MainWindow)Application.Current.MainWindow;
            //Avoids seeing incorrect initial numbers
            IPv4InterfaceStatistics nicStats = nic.GetIPv4Statistics();
            oldBytesOut = nicStats.BytesSent;
            oldBytesIn = nicStats.BytesReceived;
        }

        /**
         * 
         **/
        public void UpdateRates()
        {
            IPv4InterfaceStatistics nicStats = nic.GetIPv4Statistics();
            int conversion = 128; //Bytes to Kbps
            string rate = " Kbps";
            if (mainWindow.isMbps)
            {
                conversion = 131072; //Bytes to Mbps
                rate = " Mbps";
            }

            //Compare bytes sent/recieved difference to one second ago
            long tempBytesOut = nicStats.BytesSent;
            int curRateOut = (int)(tempBytesOut - oldBytesOut) / conversion;
            oldBytesOut = tempBytesOut;

            long tempBytesIn = nicStats.BytesReceived;
            int curRateIn = (int)(tempBytesIn - oldBytesIn) / conversion;
            oldBytesIn = tempBytesIn;

            // Update the labels
            mainWindow.Dispatcher.Invoke(() => //Allows this thread to update the UI thread
            {
                mainWindow.CurrentOutUsage.Text = curRateOut.ToString() + rate;
                mainWindow.CurrentInUsage.Text = curRateIn.ToString() + rate;
            });
            return;
        }

        /**
         * 
         **/
        public void UpdateARP()
        {
            string address = mainWindow.DefaultGateway.Text.Substring(0, mainWindow.DefaultGateway.Text.LastIndexOf(".")+1);
                //Get the default gateway address' first 24 bits
            
            //Update in background
            Thread t1 = new Thread(() =>
            {
                int timeout = 500;
                //Ping for updates
                Parallel.For(0, 256, i => //Send all pings out synchronously
                {
                    try
                    {
                        Ping sender = new Ping();
                        sender.SendAsync(address + i.ToString(), timeout); //Try whole subnet of default gateway
                        //Program ignores replies, ARP table still gets updated
                    }
                    catch
                    {
                        mainWindow.Dispatcher.Invoke(() => //Allows this thread to update the UI thread
                        {
                            mainWindow.ARPResult.Text = "Unable to ping clients";
                        });
                    }
                });

                string output = GetArp();
                mainWindow.Dispatcher.Invoke(() => //Allows this thread to update the UI thread
                {
                    mainWindow.ARPResult.Text = output;
                });
                mainWindow.isARPUpdating = false;
            });

            t1.Start();
            return;
        }

        /**
         * 
         **/
        internal void RunPing(string address)
        {
            Thread t1 = new Thread(() =>
            {
                string message;
                try
                {
                    Ping sender = new Ping();
                    PingReply reply = sender.Send(address, 1000);
                    if (reply.Status == IPStatus.Success)
                    {
                        message = ("Ping SUCCESS in " + reply.RoundtripTime.ToString() + " ms --> " + address + "\n");
                    }
                    else
                    {
                        message = ("Ping TIMEOUT --> " + address + "\n");
                    }
                }
                catch
                {
                    message = ("Ping TIMEOUT --> " + address + "\n");
                }
                mainWindow.Dispatcher.Invoke(() => //Allows this thread to update the UI thread
                {
                    mainWindow.PingResult.AppendText(message);
                    mainWindow.PingScroller.ScrollToEnd();
                });
                mainWindow.isPingUpdating = false;
            });
            t1.Start();
        }

        /**
         * 
         **/
        internal void RunTracert(string address)
        {
            Thread t1 = new Thread(() =>
            {
                foreach (var entry in Tracert(address, 30, 2500))
                {
                    if (mainWindow.tracertTOCnt >= 3) //Too many timeouts
                    {
                        break;
                    }
                    mainWindow.Dispatcher.Invoke(() => //Allows this thread to update the UI thread
                    {
                        mainWindow.TracertResult.AppendText(entry.ToString() + "\n");
                    });
                    if (isBadIP) break;
                }
                mainWindow.Dispatcher.Invoke(() =>
                {
                    mainWindow.TracertResult.AppendText("Done\n");
                });
                isBadIP = false;
                mainWindow.tracertTOCnt = 0;
                mainWindow.isTracertUpdating = false;
            });
            t1.Start();
        }

        /**
         * Adapted from https://www.fluxbytes.com/csharp/tracert-implementation-in-c/
         **/
        public IEnumerable<TracertEntry> Tracert(string ipAddress, int maxHops, int timeout)
        {
            IPAddress address;
            try
            {
                address = Dns.GetHostAddresses(ipAddress)[0];
            }
            catch
            {
                // Ensure that the argument address is valid.
                if (!IPAddress.TryParse(ipAddress, out address))
                {
                    mainWindow.Dispatcher.Invoke(() => //Allows this thread to update the UI thread
                    {
                        mainWindow.TracertResult.Text = "Not a pingable domain.\n";
                    });
                    isBadIP = true;
                    yield break;
                }
                    
            }
            
            Ping ping = new Ping();
            PingOptions pingOptions = new PingOptions(1, true);
            Stopwatch pingReplyTime = new Stopwatch();
            PingReply reply;

            do
            {
                if (isBadIP) break;
                pingReplyTime.Start();
                try
                {
                    reply = ping.Send(address, timeout, new byte[] { 0 }, pingOptions);
                }
                catch
                {
                    mainWindow.Dispatcher.Invoke(() => //Allows this thread to update the UI thread
                    {
                        mainWindow.TracertResult.Text = "Not a pingable domain.\n";
                    });
                    isBadIP = true;
                    yield break;
                }
                pingReplyTime.Stop();

                string hostname = string.Empty;
                if (reply.Address != null)
                {
                    try
                    {
                        hostname = Dns.GetHostEntry(reply.Address).HostName; //Retrieve the hostname for the replied address
                    }
                    catch (SocketException) { /* No host available for that address. */ }
                }

                // Return out TracertEntry object with all the information about the hop
                yield return new TracertEntry()
                {
                    HopID = pingOptions.Ttl,
                    Address = reply.Address == null ? "N/A" : reply.Address.ToString(),
                    Hostname = hostname,
                    ReplyTime = pingReplyTime.ElapsedMilliseconds,
                    ReplyStatus = reply.Status
                };
                pingOptions.Ttl++;
                pingReplyTime.Reset();
            }
            while (reply.Status != IPStatus.Success && pingOptions.Ttl <= maxHops);
        }

        /**
         * 
         **/
        private string GetArp()
        {
            //Run "arp -a" command and get output --> Credit to www.codeproject.com
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                FileName = "CMD.exe",
                Arguments = "/c arp -a"
            };
            process.StartInfo = startInfo;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output;
        }

        /**
         * 
         **/
        public async void FillIdentifiersAsync()
        {
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                //Get Local IP
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var lip in host.AddressList)
                    {
                        if (lip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            mainWindow.LocalIP.Text = lip.ToString();
                        }
                    }
                }
                catch
                {
                    mainWindow.LocalIP.Text = "Unavailable";
                }
                
                try
                {
                    //Get Default Gateway --> Credit to https://stackoverflow.com/a/13635038/4585894
                    IPAddress dgip = NetworkInterface
                        .GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus == OperationalStatus.Up)
                        .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                        .Select(g => g?.Address)
                        .Where(a => a != null)
                        .FirstOrDefault();
                    mainWindow.DefaultGateway.Text = dgip.ToString();
                }
                catch
                {
                    mainWindow.DefaultGateway.Text = "Unavailable";
                }
            }
            else
            {
                mainWindow.LocalIP.Text = "Unavailable";
                mainWindow.DefaultGateway.Text = "Unavailable";
            }

            //External IP using API
            try
            {
                var responseString = await client.GetStringAsync("https://api.ipify.org/");
                mainWindow.ExternalIP.Text = responseString;
            }
            catch
            {
                mainWindow.ExternalIP.Text = "Unavailable";
            }

            //Get Physical (MAC) Address
            try
            {
                string macAddr = nic.GetPhysicalAddress().ToString();
                //Format string with dashes
                string temp = "";
                for (int i = 0; i < macAddr.Length; i+=2)
                {
                    temp += macAddr.Substring(i, 2);
                    if (i+3 < macAddr.Length)
                    {
                        temp += "-";
                    }
                }
                mainWindow.MACAddress.Text = temp;
            }
            catch
            {
                mainWindow.MACAddress.Text = "Unavailable";
            }
        }
    }
}