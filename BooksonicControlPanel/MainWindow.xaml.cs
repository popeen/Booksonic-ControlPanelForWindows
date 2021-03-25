using System;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows.Media;
using System.ServiceProcess;
using System.ComponentModel;
using System.Security.Principal;

namespace BooksonicControlPanel
{
    public partial class MainWindow : Window
    {
        private System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private WindowState m_storedWindowState = WindowState.Normal;
        private String exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        private readonly BackgroundWorker worker = new BackgroundWorker();

        private String portNum = "4040";
        private String installLocation = @"C:\booksonic";


        public MainWindow()
        {
            InitializeComponent();

            if (IsAdministrator() == false)
            {
                System.Windows.Forms.MessageBox.Show("The control panel needs to run as administrator so it can handle the Booksonic service. \n\nPlease rightlick on the exe and then run as administrator", "Admin rights needed", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                System.Windows.Application.Current.Shutdown();
            }


            notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                BalloonTipText = "Booksonic has been minimised. Click the tray icon to show.",
                BalloonTipTitle = "Booksonic Control Panel",
                Text = "Booksonic Control Panel",
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath)
            };
            notifyIcon.Click += new EventHandler(trayIconClick);

            worker.DoWork += worker_DoWork;
            worker.RunWorkerAsync();
        }
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            ServiceController sc = new ServiceController("Booksonic");
            String statusStr;
            Boolean startBtnEnabled, stopBtnEnabled;
            while (true)
            {
                System.Threading.Thread.Sleep(2000);
                sc.Refresh();
                switch (sc.Status)
                {
                    case ServiceControllerStatus.Running:
                        statusStr = "Running";
                        startBtnEnabled = false;
                        stopBtnEnabled = true;
                        break;
                    case ServiceControllerStatus.Stopped:
                        statusStr = "Stopped";
                        startBtnEnabled = true;
                        stopBtnEnabled = false;
                        break;
                    case ServiceControllerStatus.Paused:
                        statusStr = "Paused";
                        startBtnEnabled = true;
                        stopBtnEnabled = true;
                        break;
                    case ServiceControllerStatus.StopPending:
                        statusStr = "Stopping";
                        startBtnEnabled = false;
                        stopBtnEnabled = false;
                        break;
                    case ServiceControllerStatus.StartPending:
                        statusStr = "Starting";
                        startBtnEnabled = false;
                        stopBtnEnabled = false;
                        break;
                    default:
                        statusStr = "Status Changing";
                        startBtnEnabled = false;
                        stopBtnEnabled = false;
                        break;
                }
                Dispatcher.Invoke(() =>
                {
                    status.Content = statusStr;
                    startBtn.IsEnabled = startBtnEnabled;
                    stopBtn.IsEnabled = stopBtnEnabled;
                });
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            notifyIcon.Dispose();
            notifyIcon = null;
            base.OnClosing(e);
        }

        void OnStateChanged(object sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (notifyIcon != null)
                {
                    notifyIcon.ShowBalloonTip(2000);

                }
            }
            else
            {
                m_storedWindowState = WindowState;
            }
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = !IsVisible;
            }
        }

        void trayIconClick(object sender, EventArgs e)
        {
            Show();
            WindowState = m_storedWindowState;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if ( sender == startBtn)
            {
                (new ServiceController("Booksonic")).Start();
            }
            else if (sender == stopBtn)
            {
                (new ServiceController("Booksonic")).Stop();
            }
            else if (sender == devBtn)
            {
                Dispatcher.Invoke(new Action(() => {
                    Console.WriteLine("Dev button pressed");
                    installJava();
                }), DispatcherPriority.ContextIdle);
            }

        }

        public void downloadBooksonic(string warUrl, string version)
        {
            Console.WriteLine("Downloading the latest version of the server");
            if (File.Exists(installLocation + @"\version"))
            {
                File.Delete(installLocation + @"\version");
            }
            File.WriteAllText(installLocation + @"\version", version);
            if (File.Exists(installLocation + @"\booksonic.war"))
            {
                File.Delete(installLocation + @"\booksonic.war");
            }

            WebClient webClient = new System.Net.WebClient();
            webClient.Headers.Add("user-agent", "BooksonicControlPanel");
            webClient.DownloadFile(warUrl, installLocation + @"\booksonic.war");
        }

        public bool processIsRunning(Process process)
        {
            try
            {
                return !process.HasExited && process.Threads.Count > 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public bool javaInstalled()
        {
            try
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = @"java";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static private string javaVersionInstalled()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "java.exe";
                psi.Arguments = " -version";
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;

                Process pr = Process.Start(psi);
                string strOutput = pr.StandardError.ReadLine().Split(' ')[2].Replace("\"", "");

                return strOutput;
            }
            catch (Exception ex)
            {
                return "error";
            }
        }

        public static bool PortInUse(int port)
        {
            bool inUse = false;

            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();


            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    inUse = true;
                    break;
                }
            }


            return inUse;
        }

        public bool installJava(String msg = "Do you want to download and install the latest Java JRE version from AdoptOpenJDK?")
        {

            Console.WriteLine("Install java started");
            if (javaInstalled())
            {
                Console.WriteLine("Java already installed");
                return true;
            }
            else
            {

                WebClient webClient = new System.Net.WebClient();
                webClient.Headers.Add("user-agent", "BooksonicControlPanel");
                String json = webClient.DownloadString("https://api.adoptopenjdk.net/v2/info/releases/openjdk11?openjdk_impl=hotspot&os=windows&arch=x64&release=latest&type=jre");
                Console.WriteLine(json);
                JObject jsonObject = JObject.Parse(json);
                string javaInstaller = (string)jsonObject["binaries"][0]["installer_link"];
                string javaName = (string)jsonObject["binaries"][0]["installer_name"];
                Console.WriteLine(javaInstaller);


                MessageBoxResult downloadJavaMessageBox = MessageBox.Show(
                    msg,
                    "Download Java",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );

                switch (downloadJavaMessageBox)
                {
                    case MessageBoxResult.Yes:
                        MessageBox.Show(
                            "Click OK to start the download, once it has been downloaded the installation will start",
                            "Download Java",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                        System.IO.Directory.CreateDirectory(installLocation);
                        webClient.DownloadFile(javaInstaller, installLocation + @"\" + javaName);
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = installLocation + @"\" + javaName;
                        psi.CreateNoWindow = false;
                        Process pr = Process.Start(psi);
                        pr.WaitForExit();

                        return true;
                        break;
                    default:
                        return false;
                        break;
                }

            }

        }

        private bool IsValidPath(string path, bool allowRelativePaths = false)
        {
            bool isValid = true;

            try
            {
                string fullPath = Path.GetFullPath(path);

                if (allowRelativePaths)
                {
                    isValid = Path.IsPathRooted(path);
                }
                else
                {
                    string root = Path.GetPathRoot(path);
                    isValid = string.IsNullOrEmpty(root.Trim(new char[] { '\\', '/' })) == false;
                }
            }
            catch (Exception ex)
            {
                isValid = false;
            }

            return isValid;
        }

    }

}