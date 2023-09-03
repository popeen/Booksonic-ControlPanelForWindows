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
using System.Net.Http;
using System.Security.Policy;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;

namespace BooksonicControlPanel
{
    public partial class MainWindow : Window
    {
        private System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private WindowState m_storedWindowState = WindowState.Normal;
        private String exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        private String nssmPath, warPath, versionPath;

        private String portNum = "4040";
        private String installLocation = @"C:\booksonic";

        public MainWindow()
        {
            InitializeComponent();

            nssmPath = installLocation + @"\booksonic-nssm.exe";
            warPath = installLocation + @"\booksonic.war";
            versionPath = installLocation + @"\version";

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


            if(isBooksonicRunning()){
                status.Content = "Running";
                startBtn.IsEnabled = false;
                stopBtn.IsEnabled = true;
                openBtn.IsEnabled = true;
                path.IsEnabled = false;
                port.IsEnabled = false;

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

        private bool isServiceInstalled()
        {
            ServiceController ctl = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == "Booksonic");
            if (ctl == null)
                return false;
            else
                return true;
        }
        
        private bool isBooksonicRunning()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string content = client.DownloadString("http://localhost:" + portNum + "/ ");
                    return true;
                }
            }
            catch (WebException)
            {
                return false;
            }
        }

        private void startService()
        {
            ServiceController ctl = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == "Booksonic");
            ctl.Start();
        }

        private void stopService()
        {
            ServiceController ctl = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == "Booksonic");
            ctl.Stop();


        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if ( sender == startBtn)
            {
                Dispatcher.Invoke(new System.Action(() =>
                {
                    installLocation = path.Text;
                    bool shouldStartServer = true;

                    //If the path is invalid dont start
                    if(shouldStartServer && !IsValidPath(installLocation))
                    {
                        MessageBox.Show(
                            "The path you have entered is not a valid path. Please fix it and try again.",
                            "Invalid path",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        shouldStartServer = false;

                    }

                    //If java is not installed we ask used to download it first
                    if (shouldStartServer && !javaInstalled())
                    {
                        installJava("Booksonic requires Java to run.\nDo you want to install the latest JRE from AdoptOpenJDK?");

                        if (!javaInstalled()) { 
                            MessageBox.Show(
                            "Booksonic Control Panel still can't find java on your computer.\n" +
                            "If you did install it restart the control panel.\n" +
                            "If you didn't install it please do so.\n\n" +
                            "If you get this message when starting the control panel from the downloads in your browser, please go to your download folder in windows instead and start it from there.",
                            "Java problem",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                            );
                            shouldStartServer = false;
                        }
                    }

                    //Don't start if the selected port is already in use
                    bool invalidPort = false;
                    int parsedPort;
                    try
                    {
                        parsedPort = int.Parse(port.Text);
                        if (parsedPort > 65353 || parsedPort < 0)
                        {
                            invalidPort = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        invalidPort = true;
                        parsedPort = 4040;
                    }

                    if (shouldStartServer && invalidPort)
                    {
                        MessageBox.Show(
                            "The port you have entered is invalid. Please use a number between 0 and 65535.",
                            "Invalid port",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        shouldStartServer = false;
                    }

                    //Don't start if the selected port is already in use
                    if (shouldStartServer && PortInUse(parsedPort))
                    {
                        MessageBox.Show(
                            "The selected port is already in use on this computer, select another port or close the application using the port",
                            "Port already in use",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        shouldStartServer = false;
                    }

                    
                    //Start the server
                    if (shouldStartServer)
                    {
                        portNum = port.Text;

                        WebClient webClient = new System.Net.WebClient();
                        webClient.Headers.Add("user-agent", "BooksonicControlPanel");
                        String json = webClient.DownloadString("https://api.github.com/repos/popeen/Booksonic-Air/releases");
                        Console.WriteLine(json);
                        JArray jsonArray = JArray.Parse(json);
                        var jsonObjects = jsonArray.OfType<JObject>().ToList();
                        string newestVersion = (string)jsonObjects[0]["tag_name"];
                        string currentVersion;
                        if (!Directory.Exists(installLocation))
                        {
                            Directory.CreateDirectory(installLocation);
                        }
                        if (File.Exists(installLocation + @"\version"))
                        {
                            currentVersion = File.ReadAllText(installLocation + @"\version");
                        }
                        else
                        {
                            currentVersion = "";
                        }
                        string warUrl = (string)jsonObjects[0]["assets"][0]["browser_download_url"];
                        Console.WriteLine("Newest:" + newestVersion + "\nCurrent: " + currentVersion);



                        //If a new version is available ask if we want to download it before starting
                        if (!String.Equals(newestVersion, currentVersion) && !String.Equals(currentVersion, ""))
                        {

                            MessageBoxResult updateAvailableMessageBox = MessageBox.Show(
                                "There is a new update available, do you want to download it before starting?",
                                "Update Available",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information
                            );

                            switch (updateAvailableMessageBox)
                            {
                                case MessageBoxResult.Yes:
                                    if (isBooksonicRunning())
                                    {
                                        stopService();
                                    }
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        status.Content = "Downloading...";
                                        startBtn.IsEnabled = false;
                                    });
                                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new System.Action(() => { }));

                                    downloadBooksonic(warUrl, newestVersion);
                                    break;
                            }

                        }
                        //If this is the first run, inform that booksonic files will be downloaded
                        else if (String.Equals(currentVersion, ""))
                        {

                            MessageBoxResult firstRunDownloadMessageBox = MessageBox.Show(
                                "Before Booksonic can be started the server files need to be downloaded. This will be done automatically and then the server will start",
                                "Downloading Booksonic",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );

                            switch (firstRunDownloadMessageBox)
                            {
                                default:
                                    downloadBooksonic(warUrl, newestVersion);
                                    if (isServiceInstalled() == false)
                                    {
                                        downloadNSSM();
                                        installService();
                                    }
                                    break;
                            }

                        }
                        downloadNSSM();
                        installService();

                        if (isServiceInstalled())
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                status.Content = "Starting service...";
                                startBtn.IsEnabled = false;
                                path.IsEnabled = false;
                                port.IsEnabled = false;
                            });
                            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new System.Action(() => { }));

                            startService();
                            //startBooksonic();

                            while (isBooksonicRunning() == false) { }
                            status.Content = "Running";
                        }
                        else
                        {
                            status.Content = "Installing service...";
                            downloadNSSM();
                            installService();
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                status.Content = "Starting service...";
                                startBtn.IsEnabled = false;
                                path.IsEnabled = false;
                                port.IsEnabled = false;
                            });
                            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new System.Action(() => { }));

                            startService();
                            //startBooksonic();

                            while (isBooksonicRunning() == false) { }
                            status.Content = "Running";
                        }

                        stopBtn.IsEnabled = true;
                        openBtn.IsEnabled = true;
                    }
                }), DispatcherPriority.Background);
            }
            else if (sender == stopBtn)
            {
                Dispatcher.Invoke(new System.Action(() => {
                    status.Content = "Stopping service";
                    stopService();
                    status.Content = "Not Running";
                    startBtn.IsEnabled = true;
                    stopBtn.IsEnabled = false;
                    openBtn.IsEnabled = false;
                    path.IsEnabled = true;
                    port.IsEnabled = true;
                }), DispatcherPriority.ContextIdle);
            }
            else if (sender == devBtn)
            {
                Dispatcher.Invoke(new System.Action(() => {
                    Console.WriteLine("Dev button pressed");
                    installJava();
                }), DispatcherPriority.ContextIdle);
            }
            else if(sender == openBtn)
            {
                System.Diagnostics.Process.Start("http://localhost:" + portNum + "/ ");
            }
        }

        public void downloadNSSM()
        {
            
            Console.WriteLine("Downloading NSSM");
            if (false == File.Exists(nssmPath))
            {
                WebClient webClient = new System.Net.WebClient();
                webClient.Headers.Add("user-agent", "BooksonicControlPanel");
                webClient.DownloadFile("https://booksonic.org/files/nssm.exe", nssmPath);
            }
        }

        public void installService()
        {

            // Remove the scheduled task created by the old installation script
            RemoveScheduledTask("Booksonic start on boot");

            // Install the process using nssm
            startHiddenProcess(nssmPath, $"install Booksonic \"java\" \"-Dairsonic.home=" + installLocation + " -Dserver.port=" + portNum + " -jar " + warPath + "\"");

        }

        public void removeService()
        {

            // Install the process using nssm
            startHiddenProcess(nssmPath, $"remove Booksonic confirm");

        }

        public void startHiddenProcess(String filePath, String arguments)
        {

            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(filePath)
            {
                Arguments = arguments,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false

            };
            process.Start();
            process.WaitForExit();
        }

        public bool RemoveScheduledTask(string taskName)
        {
            using (TaskService taskService = new TaskService())
            {
                try
                {
                    // Get the task by its name
                    Task task = taskService.GetTask(taskName);

                    if (task != null)
                    {
                        // Delete the task
                        taskService.RootFolder.DeleteTask(taskName);
                        return true;
                    }
                    else
                    {
                        // Task not found
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    // Handle any exceptions (e.g., access denied, task not found, etc.)
                    Console.WriteLine($"Error removing task: {ex.Message}");
                    return false;
                }
            }
        }
        public void downloadBooksonic(string warUrl, string version)
        {
            Console.WriteLine("Downloading the latest version of the server");
            if (File.Exists(versionPath))
            {
                File.Delete(versionPath);
            }
            File.WriteAllText(versionPath, version);
            if (File.Exists(warPath))
            {
                File.Delete(warPath);
            }

            WebClient webClient = new System.Net.WebClient();
            webClient.Headers.Add("user-agent", "BooksonicControlPanel");
            webClient.DownloadFile(warUrl, warPath);
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
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "java",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    }
                };
                process.Start();
                return true;
            }
            catch
            {
                return false;
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