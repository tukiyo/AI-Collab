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
            // 環境変数
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", 
                "--disable-features=InsecureFormWarnings,MixedFormsInterstitial,SafeBrowsing --allow-running-insecure-content");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            string initialUrl = IniFileHandler.GetUrlFromIni();
            Application.Run(new MainForm(initialUrl));
        }
    }

    public class MainForm : Form
    {
        private WebView2 webView;
        private string currentUrl;
        private FileSystemWatcher exitWatcher;

        public MainForm(string url)
        {
            this.currentUrl = url;
            this.Text = "読み込み中..."; // 初期表示
            this.Width = 1024;
            this.Height = 768;

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

            webView.CoreWebView2.ServerCertificateErrorDetected += CoreWebView2_ServerCertificateErrorDetected;
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            // HTMLのタイトルが変更された時に実行されるイベント
            webView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
            
            webView.CoreWebView2.Navigate(this.currentUrl);
        }

        // ページのタイトルをウィンドウのタイトルバーに反映する
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
            MainForm newWindow = new MainForm(e.Uri);
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

        public static string GetUrlFromIni()
        {
            string defaultUrl = "https://www.google.com/";
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program.ini");

            if (!File.Exists(iniPath)) return defaultUrl;

            StringBuilder sb = new StringBuilder(1024);
            uint result = GetPrivateProfileString("Settings", "Url", defaultUrl, sb, (uint)sb.Capacity, iniPath);
            return result > 0 ? sb.ToString() : defaultUrl;
        }
    }
}