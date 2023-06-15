using System;
using System.Timers;
using System.Windows;

namespace NetworkTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //For NetworkLogic class
        private NetworkLogic netLog;
        public bool isARPUpdating = false;
        public bool isPingUpdating = false;
        public bool isTracertUpdating = false;
        public bool isMbps = false;
        public int tracertTOCnt = 0;
        private Timer timer;

        public MainWindow()
        {
            InitializeComponent();

            //Initialize and fill networking fields
            netLog = new NetworkLogic();
            netLog.FillIdentifiersAsync(); //Fills in ExternalIP, LocalIP, MACAddress, DefaultGateway
            this.ARPResult.Text = "Updating now...";
            this.isARPUpdating = true;
            netLog.UpdateARP(); //Fills in Local Network Map with ARP results
            PingResult.IsReadOnly = true;
            TracertResult.IsReadOnly = true;

            //Continuously run in/out network rate updates
            timer = new Timer();
            timer.Interval = 1000;
            timer.Elapsed += new ElapsedEventHandler(timerTick); //Do update every second
            timer.Start();
        }

        private void timerTick(object sender, EventArgs e) { netLog.UpdateRates(); }

        private void Mbps_Checked(object sender, RoutedEventArgs e) => isMbps = true;

        private void Kbps_Checked(object sender, RoutedEventArgs e) => isMbps = false;

        private void PingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isPingUpdating) //If already updating, ignore click. Like a mutex
            {
                this.isPingUpdating = true;
                netLog.RunPing(this.EnterDomain.Text);
            }
        }

        private void TracertButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isTracertUpdating) //If already updating, ignore click. Like a mutex
            {
                isTracertUpdating = true;
                TracertResult.Text = "";
                netLog.RunTracert(this.EnterDomain.Text);
            }
        }

        private void UpdateARPButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isARPUpdating) //If already updating, ignore click. Like a mutex
            {
                isARPUpdating = true;
                ARPResult.Text = "Updating now...";
                netLog.UpdateARP();
            }
        }
    }
}
