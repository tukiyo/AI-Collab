using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace WebView2App
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private WebView2 webView;

        public MainForm()
        {
            this.Text = "WebView2 via csc.exe";
            this.Width = 1024;
            this.Height = 768;

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            this.Controls.Add(webView);

            this.Load += MainForm_Load;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            // WebView2の初期化
            await webView.EnsureCoreWebView2Async(null);
            
            string url = "https://www.google.com/";
            webView.CoreWebView2.Navigate(url);
            
            this.Text = string.Format("Loaded: {0}", url);
        }
    }
}