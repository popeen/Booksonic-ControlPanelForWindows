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

namespace BooksonicControlPanel
{
    public partial class MainWindow : Window
    {
        private System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
        private String portNum = "4040";
        private String installLocation = @"C:\booksonic";

        public MainWindow()
        {
            InitializeComponent();
        }


        protected override void OnClosing(CancelEventArgs e)
        {


            //If the server is running ask if we really want to close
            if (processIsRunning(pProcess))
            {

                MessageBoxResult shutdownWarningMessageBox = MessageBox.Show(
                    "If you close this window Booksonic will be shut down. Do you want to continue?",
                    "Closing Booksonic",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                switch (shutdownWarningMessageBox)
                {
                    case MessageBoxResult.Yes:
                        stopBooksonic();
                        break;

                    default:
                        e.Cancel = true;
                        break;
                }
            }

            base.OnClosing(e);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if ( sender == startBtn)
            {
                Dispatcher.Invoke(new Action(() =>
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
                                    break;
                            }

                        }
                        startBooksonic();

                        status.Content = "Running";
                        startBtn.IsEnabled = false;
                        stopBtn.IsEnabled = true;
                    }
                }), DispatcherPriority.Background);
            }
            else if (sender == stopBtn)
            {
                Dispatcher.Invoke(new Action(() => {
                    stopBooksonic();
                    status.Content = "Not Running";
                    startBtn.IsEnabled = true;
                    stopBtn.IsEnabled = false;
                }), DispatcherPriority.ContextIdle);
            }
            else if (sender == devBtn)
            {
                Dispatcher.Invoke(new Action(() => {
                    Console.WriteLine("Dev button pressed");
                    installJava();
                }), DispatcherPriority.ContextIdle);
            }

        }

        public void startBooksonic()
        {
            pProcess.StartInfo.FileName = @"java";
            pProcess.StartInfo.Arguments = @"-Dairsonic.home=" + installLocation + @" -Dserver.port=" + portNum + " -jar booksonic.war";
            pProcess.StartInfo.UseShellExecute = false;
            pProcess.StartInfo.CreateNoWindow = true;
            pProcess.StartInfo.WorkingDirectory = installLocation + @"\";
            pProcess.Start();
        }

        public void stopBooksonic()
        {
            //TODO, this should be done more gracefully
            if (processIsRunning(pProcess))
            {
                pProcess.Kill();
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