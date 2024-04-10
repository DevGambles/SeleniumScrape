using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SeleniumScrape
{
    public partial class OrderBot : Form
    {
        //http://api.ipify.org?format=json
        //https://www.vaurioajoneuvo.fi/kayttajalle/kirjaudu-sisaan/
        //url = "https://www.vaurioajoneuvo.fi/tuote/volvo-5d-v50-stw-2-0-mw4342-264-boi-682/";
        class ScrapeParam
        {
            public string url;
            public int frequency;
        }
        Thread mainThread;
        IWebDriver mDriver;
        Thread threadMonitor;
        bool main_loop = true;
        bool monitor_loop = true;
        public OrderBot()
        {
            InitializeComponent();
            InitializeTor();
        }
        private void InitializeTor()
        {
            killTor();
            runHideTor();
        }

        private static void runHideTor()
        {
            Process cmd = new Process();
            cmd.StartInfo.FileName = @"Tor\Tor\tor.exe";
            cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.Start();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            mainThread = new Thread(mainloop);
            mainThread.Start();
        }
        private void mainloop()
        {
            string[] config = GetConfig();
            //SystemSounds.Question.Play();
            string url = txtUrl.Text;
            int frequency = int.Parse(txtFrequency.Text);
            int delay = int.Parse(txt_proxychange.Text);
            main_loop = true;
            monitor_loop = true;
            //open the page from url
            while (main_loop)
            {
                StartupChromeBrowser();
                GoToUrl(url);
                Login(config);
                GetData(url, frequency);
                Thread.Sleep(delay * 60000);
                mDriver.Quit();
                RefreshTorIdentity();
            }
        }
        private static string[] GetConfig()
        {
            string[] config = new string[2];
            int i = 0;
            foreach (string line in File.ReadLines("./config.txt"))
            {
                config[i] = line;
                i++;
            }
            return config;
        }

        private void GoToUrl(string url)
        {
            mDriver.Navigate().GoToUrl(url);

            mDriver.Manage().Window.Maximize();
        }

        private void Login(string[] config)
        {
            try
            {
                IWebElement div = mDriver.FindElement(By.Id("header-actions-desktop"));

                IWebElement button = div.FindElement(By.ClassName("js-login"));
                button.Click();

                Thread.Sleep(1000);

                IWebElement inputUsername = mDriver.FindElement(By.Id("username"));
                IWebElement inputPassword = mDriver.FindElement(By.Id("password"));
                inputUsername.SendKeys(config[0]);
                Thread.Sleep(1000);
                inputPassword.SendKeys(config[1]);
                Thread.Sleep(1000);

                IWebElement btnLogin = mDriver.FindElement(By.ClassName("mb-3"));
                btnLogin.Click();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        private void GetData(string url, int frequency)
        {
            Thread.Sleep(2000);

            ScrapeParam param = new ScrapeParam
            {
                url = url,
                frequency = frequency
            };

            threadMonitor = new Thread(new ParameterizedThreadStart(ThreadMonitor));
            threadMonitor.Start(param);
        }

        private void StartupChromeBrowser()
        {
            ChromeOptions chromeOptions = new ChromeOptions();
            List<string> ls = new List<string>();
            ls.Add("enable-automation");
            chromeOptions.AddExcludedArguments(ls);
            chromeOptions.AddArgument("--disable-blink-features=AutomationControlled");
            chromeOptions.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36");
            chromeOptions.AddArgument("--remote-debugging-port=9222");
            //chromeOptions.AddArguments("--proxy-server=socks5://localhost:9050");
            mDriver = new ChromeDriver(chromeOptions);
        }

        private void txtFrequency_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
            {
                e.Handled = true;
            }

            // only allow one decimal point
            if ((e.KeyChar == '.') && ((sender as TextBox).Text.IndexOf('.') > -1))
            {
                e.Handled = true;
            }
        }

        public void ThreadMonitor(object info)
        {
            ScrapeParam param = (ScrapeParam)info;
            while (monitor_loop)
            {
                try
                {
                    IWebElement button = mDriver.FindElement(By.ClassName("button-buy"));
                    string text = button.GetAttribute("outerHTML"); ;

                    if (!text.Contains("disabled"))
                    {
                        button.Click();
                        Console.Beep(500, 2000);
                        break;
                    }

                    Thread.Sleep(param.frequency * 1000);
                    mDriver.Navigate().Refresh();
                    //if (changeProxyTime)
                }
                catch { }
            }
        }

        private void But_close_Click(object sender, EventArgs e)
        {
            main_loop = false;
            monitor_loop = false;
            mainThread.Abort();
            threadMonitor.Abort();
            mDriver.Quit();
        }

        public void RefreshTorIdentity()
        {
            Socket server = null;
            try
            {
                IPEndPoint ip = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9051);
                server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server.Connect(ip);
                server.Send(Encoding.ASCII.GetBytes("AUTHENTICATE \"123456\"" + Environment.NewLine));
                byte[] data = new byte[1024];
                int receivedDataLength = server.Receive(data);
                string stringData = Encoding.ASCII.GetString(data, 0, receivedDataLength);
                server.Send(Encoding.ASCII.GetBytes("SIGNAL NEWNYM" + Environment.NewLine));
                data = new byte[1024];
                receivedDataLength = server.Receive(data);
                stringData = Encoding.ASCII.GetString(data, 0, receivedDataLength);
                if (!stringData.Contains("250"))
                {
                    Console.WriteLine("Unable to signal new user to server.");
                    server.Shutdown(SocketShutdown.Both);
                    server.Close();
                }
            }
            finally
            {
                server.Close();
            }
        }

        private void But_changeProxy_Click(object sender, EventArgs e)
        {
            RefreshTorIdentity();
        }

        private void OrderBot_FormClosing(object sender, FormClosingEventArgs e)
        {
            killTor();
        }

        private static void killTor()
        {
            Process[] previous = Process.GetProcessesByName("tor");

            if (previous != null && previous.Length > 0)
            {
                foreach (Process process in previous)
                    process.Kill();
            }
        }
    }
}
