using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WebView2App
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", 
                "--disable-features=InsecureFormWarnings,MixedFormsInterstitial,SafeBrowsing " +
                "--allow-running-insecure-content " +
                "--ssl-version-min=tls1 " + 
                "--cipher-suite-blacklist=0x0000");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            string initialUrl = IniFileHandler.GetUrlFromIni();
            string appendUserAgent = IniFileHandler.GetUserAgentFromIni();
            // INIからサイズを取得
            int width = IniFileHandler.GetIntValue("Settings", "Width", 1024);
            int height = IniFileHandler.GetIntValue("Settings", "Height", 768);

            Application.Run(new MainForm(initialUrl, appendUserAgent, width, height));
        }
    }

    public class MainForm : Form
    {
        private WebView2 webView;
        private string currentUrl;
        private string appendUserAgent;
        private FileSystemWatcher exitWatcher;

        public MainForm(string url, string appendUserAgent, int width, int height)
        {
            this.currentUrl = url;
            this.appendUserAgent = appendUserAgent;
            this.Text = "読み込み中...";
            
            // 引数で渡されたサイズを設定
            this.Width = width;
            this.Height = height;

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            this.Controls.Add(webView);

            this.Load += MainForm_Load;
            SetupExitWatcher();
        }

        private void SetupExitWatcher()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            exitWatcher = new FileSystemWatcher(baseDir, "exit.signal");
            exitWatcher.Created += (s, e) => {
                this.Invoke((MethodInvoker)delegate {
                    Application.Exit();
                });
            };
            exitWatcher.EnableRaisingEvents = true;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await webView.EnsureCoreWebView2Async(null);

            if (!string.IsNullOrEmpty(this.appendUserAgent))
            {
                string defaultUA = webView.CoreWebView2.Settings.UserAgent;
                webView.CoreWebView2.Settings.UserAgent = string.Format("{0} {1}", defaultUA, this.appendUserAgent);
            }

            webView.CoreWebView2.ServerCertificateErrorDetected += CoreWebView2_ServerCertificateErrorDetected;
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
            
            webView.CoreWebView2.Navigate(this.currentUrl);
        }

        private void CoreWebView2_DocumentTitleChanged(object sender, object e)
        {
            this.Text = webView.CoreWebView2.DocumentTitle;
        }

        private void CoreWebView2_ServerCertificateErrorDetected(object sender, CoreWebView2ServerCertificateErrorDetectedEventArgs e)
        {
            e.Action = CoreWebView2ServerCertificateErrorAction.AlwaysAllow;
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            // 子ウィンドウにも設定を引き継ぐ（サイズは親と同じに設定）
            MainForm newWindow = new MainForm(e.Uri, this.appendUserAgent, this.Width, this.Height);
            newWindow.Show();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && exitWatcher != null)
            {
                exitWatcher.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public static class IniFileHandler
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern uint GetPrivateProfileString(
            string lpAppName, string lpKeyName, string lpDefault,
            StringBuilder lpReturnedString, uint nSize, string lpFileName);

        private static string GetIniPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program.ini");
        }

        public static string GetUrlFromIni()
        {
            return GetValue("Settings", "Url", "https://www.google.com/");
        }

        public static string GetUserAgentFromIni()
        {
            // 元のiniのキー名「UserAgent」に合わせています
            return GetValue("Settings", "UserAgent", "");
        }

        // 数値を取得するためのヘルパー
        public static int GetIntValue(string section, string key, int defaultValue)
        {
            string val = GetValue(section, key, "");
            int result;
            if (int.TryParse(val, out result))
            {
                return result;
            }
            return defaultValue;
        }

        private static string GetValue(string section, string key, string defaultValue)
        {
            string iniPath = GetIniPath();
            if (!File.Exists(iniPath)) return defaultValue;

            StringBuilder sb = new StringBuilder(1024);
            uint result = GetPrivateProfileString(section, key, defaultValue, sb, (uint)sb.Capacity, iniPath);
            return result > 0 ? sb.ToString().Trim() : defaultValue;
        }
    }
}