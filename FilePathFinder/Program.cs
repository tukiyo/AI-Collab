using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Generic;

namespace FileListSearcher
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SearchForm());
        }
    }

    public class SearchForm : Form
    {
        private string baseDir;
        private string historyPath;
        private Encoding sjisEnc;
        private Encoding utf8Enc;

        // 現在選択されているファイルリストのパス情報
        private string currentRawPath = "";
        private string currentCompressedPath = "";
        private string lastSelectedListName = "";

        // GUIコンポーネント
        private Label lblTitle;
        private Label lblTargetList;
        private ComboBox cmbTargetList;
        private Label lblGuide;
        private ComboBox cmbKeyword;
        private Button btnSearch;
        private Button btnMakeList;
        private Label lblStatus;

        public SearchForm()
        {
            // パスと文字コードの初期設定
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            historyPath = Path.Combine(baseDir, "history.txt");
            sjisEnc = Encoding.GetEncoding("shift_jis");
            utf8Enc = new UTF8Encoding(false);

            // 画面のデザイン設定
            this.Text = "ファイル名検索＆生成ツール";
            this.Size = new Size(540, 290);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // タイトル
            lblTitle = new Label();
            lblTitle.Text = "🔍 ファイル名検索";
            lblTitle.Font = new Font("Meiryo", 14, FontStyle.Bold);
            lblTitle.Location = new Point(20, 15);
            lblTitle.Size = new Size(480, 30);

            // ファイルリスト切り替えラベル
            lblTargetList = new Label();
            lblTargetList.Text = "検索対象のファイルリストを選択:";
            lblTargetList.Font = new Font("Meiryo", 9, FontStyle.Bold);
            lblTargetList.Location = new Point(22, 55);
            lblTargetList.Size = new Size(200, 20);

            // ファイルリスト選択用ドロップダウン
            cmbTargetList = new ComboBox();
            cmbTargetList.Font = new Font("Meiryo", 10);
            cmbTargetList.Location = new Point(25, 78);
            cmbTargetList.Size = new Size(325, 28);
            cmbTargetList.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTargetList.SelectedIndexChanged += new EventHandler(CmbTargetList_SelectedIndexChanged);

            // ファイルリスト生成ボタン
            btnMakeList = new Button();
            btnMakeList.Text = "新しいリストを作る";
            btnMakeList.Font = new Font("Meiryo", 9, FontStyle.Bold);
            btnMakeList.Location = new Point(360, 77);
            btnMakeList.Size = new Size(140, 29);
            btnMakeList.Click += new EventHandler(BtnMakeList_Click);

            // 説明文
            lblGuide = new Label();
            lblGuide.Text = "探したいキーワードを入力して【Enter】を押してください。\nスペース区切りでAND検索になります。";
            lblGuide.Font = new Font("Meiryo", 8.5f);
            lblGuide.Location = new Point(22, 115);
            lblGuide.Size = new Size(480, 35);

            // 入力履歴付きコンボボックス
            cmbKeyword = new ComboBox();
            cmbKeyword.Font = new Font("Meiryo", 11);
            cmbKeyword.Location = new Point(25, 155);
            cmbKeyword.Size = new Size(425, 30);
            cmbKeyword.BackColor = Color.FromArgb(235, 243, 250);
            cmbKeyword.KeyDown += new KeyEventHandler(CmbKeyword_KeyDown);

            // 検索ボタン
            btnSearch = new Button();
            btnSearch.Text = "検索";
            btnSearch.Font = new Font("Meiryo", 9, FontStyle.Bold);
            btnSearch.Location = new Point(455, 154);
            btnSearch.Size = new Size(45, 29);
            btnSearch.Click += new EventHandler(BtnSearch_Click);

            // 状態表示ラベル
            lblStatus = new Label();
            lblStatus.Text = "準備完了";
            lblStatus.Font = new Font("Meiryo", 8.5f);
            lblStatus.Location = new Point(25, 215);
            lblStatus.Size = new Size(475, 20);

            // コンポーネントを画面に追加
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblTargetList);
            this.Controls.Add(cmbTargetList);
            this.Controls.Add(btnMakeList);
            this.Controls.Add(lblGuide);
            this.Controls.Add(cmbKeyword);
            this.Controls.Add(btnSearch);
            this.Controls.Add(lblStatus);

            this.Load += new EventHandler(SearchForm_Load);
        }

        private void SearchForm_Load(object sender, EventArgs e)
        {
            LoadHistory();
            RefreshListDropdown(lastSelectedListName);
            
            this.ActiveControl = cmbKeyword;
            cmbKeyword.Focus();
        }

        private void RefreshListDropdown(string selectName)
        {
            if (string.IsNullOrEmpty(selectName) && cmbTargetList.SelectedItem != null)
            {
                selectName = cmbTargetList.SelectedItem.ToString();
            }

            cmbTargetList.Items.Clear();
            
            string rawFile = Path.Combine(baseDir, "filelist.txt");
            if (File.Exists(rawFile))
            {
                string compPath = Path.Combine(baseDir, "filelist.gz");
                if (!File.Exists(compPath))
                {
                    lblStatus.Text = "自動圧縮中: filelist.txt ...";
                    SetControlsEnabled(false);
                    Application.DoEvents();
                    try
                    {
                        CompressFile(rawFile, compPath, sjisEnc);
                        File.Delete(rawFile);
                    }
                    catch { }
                }
                else
                {
                    try { File.Delete(rawFile); } catch { }
                }
            }

            string[] compressedFiles = Directory.GetFiles(baseDir, "*.gz");
            foreach (string gzFile in compressedFiles)
            {
                string displayName = Path.GetFileNameWithoutExtension(gzFile);
                if (displayName.Equals("history", StringComparison.OrdinalIgnoreCase)) continue;

                cmbTargetList.Items.Add(displayName);
            }

            SetControlsEnabled(true);

            if (cmbTargetList.Items.Count > 0)
            {
                int index = cmbTargetList.FindStringExact(selectName);
                if (index >= 0) cmbTargetList.SelectedIndex = index;
                else cmbTargetList.SelectedIndex = 0;
                
                btnSearch.Enabled = true;
            }
            else
            {
                lblStatus.Text = "リストがありません。右上の「新しいリストを作る」から作成してください。";
                btnSearch.Enabled = false;
            }
        }

        private void CmbTargetList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbTargetList.SelectedItem == null) return;

            string selectedName = cmbTargetList.SelectedItem.ToString();
            currentRawPath = Path.Combine(baseDir, "filelist.txt");
            currentCompressedPath = Path.Combine(baseDir, selectedName + ".gz");

            lblStatus.Text = "「" + selectedName + "」を選択中。";

            if (lastSelectedListName != selectedName)
            {
                lastSelectedListName = selectedName;
                SaveHistory(cmbKeyword.Text.Trim(), false);
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnSearch.Enabled = enabled && (cmbTargetList.Items.Count > 0);
            btnMakeList.Enabled = enabled;
            cmbKeyword.Enabled = enabled;
            cmbTargetList.Enabled = enabled;
        }

        private void CmbKeyword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && btnSearch.Enabled)
            {
                e.SuppressKeyPress = true;
                btnSearch.PerformClick();
            }
        }

        private void BtnMakeList_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "スキャンしたいフォルダーを選択してください。";
                fbd.ShowNewFolderButton = false;

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    lblStatus.Text = "フォルダーをスキャン中...";
                    SetControlsEnabled(false);
                    this.Cursor = Cursors.WaitCursor;
                    Application.DoEvents();

                    string targetRaw = Path.Combine(baseDir, "filelist.txt");
                    string targetComp = Path.Combine(baseDir, "filelist.gz");

                    try
                    {
                        if (File.Exists(targetRaw)) File.Delete(targetRaw);
                        if (File.Exists(targetComp)) File.Delete(targetComp);

                        long fileCount = 0;
                        using (StreamWriter sw = new StreamWriter(targetRaw, false, sjisEnc))
                        {
                            foreach (string file in SafeEnumerateFiles(fbd.SelectedPath))
                            {
                                sw.WriteLine(file);
                                fileCount++;

                                if (fileCount % 5000 == 0)
                                {
                                    lblStatus.Text = "スキャン中... 現在 " + fileCount + " 件発見";
                                    Application.DoEvents();
                                }
                            }
                        }

                        lblStatus.Text = "一覧作成完了（" + fileCount + " 件）。続いて圧縮ファイルを生成中...";
                        Application.DoEvents();
                        
                        CompressFile(targetRaw, targetComp, sjisEnc);

                        if (File.Exists(targetRaw))
                        {
                            File.Delete(targetRaw);
                        }

                        lblStatus.Text = "作成完了：filelist.gzの名前を変更して管理してください（例: Cドライブ.gz）";
                        
                        RefreshListDropdown("filelist");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("リスト作成中にエラーが発生しました:\n" + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "作成エラー";
                    }
                    finally
                    {
                        this.Cursor = Cursors.Default;
                        SetControlsEnabled(true);
                        cmbKeyword.Focus();
                    }
                }
            }
        }

        private IEnumerable<string> SafeEnumerateFiles(string path)
        {
            string[] files = null;
            try
            {
                files = Directory.GetFiles(path, "*");
            }
            catch { }

            if (files != null)
            {
                foreach (string file in files)
                {
                    yield return file;
                }
            }

            string[] dirs = null;
            try
            {
                dirs = Directory.GetDirectories(path);
            }
            catch { }

            if (dirs != null)
            {
                foreach (string dir in dirs)
                {
                    foreach (string file in SafeEnumerateFiles(dir))
                    {
                        yield return file;
                    }
                }
            }
        }

        private void BtnSearch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentCompressedPath) || !File.Exists(currentCompressedPath))
            {
                MessageBox.Show("有効な検索対象リストが選ばれていません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                cmbKeyword.Focus();
                return;
            }

            string keyword = cmbKeyword.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("検索キーワードを入力してください。", "お知らせ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                cmbKeyword.Focus();
                return;
            }

            SaveHistory(keyword, true);

            char[] splitChars = new char[] { ' ', '　' };
            string[] keywords = keyword.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);

            string safeKeyword = keyword;
            foreach (char c in Path.GetInvalidFileNameChars()) safeKeyword = safeKeyword.Replace(c, '_');

            string listName = cmbTargetList.SelectedItem.ToString();
            string htmlFileName = "result_" + listName + "_" + safeKeyword + ".html";
            string htmlPath = Path.Combine(baseDir, htmlFileName);

            lblStatus.Text = "「" + listName + "」から検索中...";
            SetControlsEnabled(false);
            this.Cursor = Cursors.WaitCursor;
            Application.DoEvents();

            long matchCount = 0;

            try
            {
                using (FileStream fs = new FileStream(currentCompressedPath, FileMode.Open, FileAccess.Read))
                using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                using (StreamReader reader = new StreamReader(gz, sjisEnc))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        bool isMatch = true;
                        foreach (string kw in keywords)
                        {
                            if (line.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                isMatch = false;
                                break;
                            }
                        }
                        if (isMatch) matchCount++;
                    }
                }

                if (matchCount == 0)
                {
                    lblStatus.Text = "検索完了：ヒットしたファイルはありません。";
                    MessageBox.Show("指定されたキーワードに一致するファイルは見つかりませんでした。結果ファイルは出力されません。", "検索結果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    using (FileStream fs2 = new FileStream(currentCompressedPath, FileMode.Open, FileAccess.Read))
                    using (GZipStream gz2 = new GZipStream(fs2, CompressionMode.Decompress))
                    using (StreamReader reader2 = new StreamReader(gz2, sjisEnc))
                    using (StreamWriter writer = new StreamWriter(htmlPath, false, utf8Enc))
                    {
                        Action<string> W = delegate(string text) { writer.WriteLine(text); };

                        W("<!DOCTYPE html>");
                        W("<html lang=\"ja\">");
                        W("<head>");
                        W("    <meta charset=\"UTF-8\">");
                        W("    <title>検索結果: " + keyword + "</title>");
                        W("    <style>");
                        W("        body { font-family: 'Segoe UI', Meiryo, sans-serif; margin: 15px; background-color: #f0f4f9; color: #1f1f1f; transition: background-color 0.3s, color 0.3s; }");
                        W("        h1 { font-size: 16px; border-left: 5px solid #1a73e8; padding-left: 10px; color: #1f1f1f; margin-bottom: 3px; margin-top: 0; }");
                        W("        .summary { margin-bottom: 10px; color: #5f6368; font-size: 12px; }");
                        W("        .control-panel { display: flex; align-items: center; gap: 20px; margin-bottom: 10px; }");
                        W("        .filter-box { margin-bottom: 0; }");
                        W("        .filter-box input { width: 100%; max-width: 400px; padding: 6px 10px; font-size: 13px; border: 1px solid #747775; border-radius: 6px; background-color: #ffffff; color: #1f1f1f; outline: none; }");
                        W("        .filter-box input:focus { border-color: #1a73e8; box-shadow: 0 0 0 2px rgba(26,115,232,0.2); }");
                        W("        .switch-box { font-size: 13px; color: #444746; display: flex; align-items: center; cursor: pointer; user-select: none; }");
                        W("        .switch-box input { margin-right: 6px; cursor: pointer; }");

                        W("        ol { background: #ffffff; padding: 10px 10px 10px 40px; border: 1px solid #c4c7c5; border-radius: 8px; max-height: calc(100vh - 160px); overflow-y: auto; margin-top: 5px; margin-bottom: 10px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }");
                        W("        ol::-webkit-scrollbar { width: 8px; height: 8px; }");
                        W("        ol::-webkit-scrollbar-track { background: #f0f4f9; border-radius: 4px; }");
                        W("        ol::-webkit-scrollbar-thumb { background: #c4c7c5; border-radius: 4px; }");
                        W("        ol::-webkit-scrollbar-thumb:hover { background: #a8aab0; }");
                        W("        ol { scrollbar-width: thin; scrollbar-color: #c4c7c5 #f0f4f9; }");

                        W("        li { display: flex; align-items: center; justify-content: space-between; padding: 2px 6px; font-size: 12px; font-family: Consolas, monospace; word-break: break-all; color: #3c4043; cursor: pointer; transition: background-color 0.1s; border-radius: 3px; list-style-position: outside; }");
                        W("        li:nth-child(even) { background-color: #f8f9fa; }");
                        W("        li:hover { background-color: #e8f0fe !important; color: #1a73e8; }");
                        W("        .file-info { display: flex; align-items: center; flex-grow: 1; margin-right: 15px; white-space: normal; }");
                        W("        .file-icon { display: inline-block; width: 20px; text-align: center; margin-right: 4px; user-select: none; flex-shrink: 0; }");
                        W("        .path-dir { color: #8e918f; font-weight: normal; }");
                        
                        W("        .hide-dir .path-dir { display: none; }");
                        // 【追加】フォルダボタン(copy-btn)にホバーしたときだけ、非表示設定でもフォルダ名を表示させる指定
                        W("        .hide-dir li:has(.copy-btn:hover) .path-dir { display: inline; }");

                        W("        .path-name { color: #1f1f1f; font-weight: normal; }");
                        W("        li:hover .path-name { color: #1a73e8; }");

                        W("        .btn-group { display: flex; gap: 4px; flex-shrink: 0; }");
                        W("        .copy-btn { padding: 1px 6px; font-size: 10px; font-family: Meiryo, sans-serif; border: 1px solid #c4c7c5; border-radius: 3px; background-color: #ffffff; color: #444746; cursor: pointer; user-select: none; visibility: hidden; opacity: 0; transition: visibility 0s, opacity 0.1s linear; }");
                        W("        li:hover .copy-btn { visibility: visible; opacity: 1; }");
                        W("        .copy-btn:hover { background-color: #1a73e8; color: #ffffff; border-color: #1a73e8; }");
                        
                        W("        .toast { position: fixed; bottom: 24px; left: 50%; transform: translateX(-50%); background-color: #1f1f1f; color: #fff; padding: 12px 24px; border-radius: 24px; font-size: 14px; opacity: 0; transition: opacity 0.2s; pointer-events: none; z-index: 9999; box-shadow: 0 4px 12px rgba(0,0,0,0.15); }");
                        W("        .toast.show { opacity: 1; }");
                        
                        W("        @media (prefers-color-scheme: dark) {");
                        W("            body { background-color: #0b0d14; color: #e3e3e3; }");
                        W("            h1 { color: #e3e3e3; border-image: linear-gradient(to bottom, #7a96fc, #9bc5ff) 1 100%; border-left: 5px solid; }");
                        W("            .summary { color: #8e918f; }");
                        W("            .switch-box { color: #c4c7c5; }");
                        W("            .filter-box input { background-color: #1e202a; color: #e3e3e3; border-color: #444746; }");
                        W("            .filter-box input:focus { border-color: #a8c7fa; box-shadow: 0 0 0 2px rgba(168,199,250,0.2); }");
                        W("            ol { background: #131520; border-color: #444746; box-shadow: 0 4px 16px rgba(0,0,0,0.3); }");
                        W("            ol::-webkit-scrollbar-track { background: #131520; }");
                        W("            ol::-webkit-scrollbar-thumb { background: #444746; }");
                        W("            ol::-webkit-scrollbar-thumb:hover { background: #5f6368; }");
                        W("            ol { scrollbar-color: #444746 #131520; }");
                        W("            li { color: #c4c7c5; }");
                        W("            li:nth-child(even) { background-color: #1a1c28; }");
                        W("            li:hover { background-color: #28324a !important; color: #a8c7fa; }");
                        W("            .path-dir { color: #686a70; }");
                        W("            .path-name { color: #f5f5f5; }");
                        W("            li:hover .path-name { color: #a8c7fa; }");
                        W("            .copy-btn { background-color: #1e202a; color: #c4c7c5; border-color: #444746; }");
                        W("            .copy-btn:hover { background-color: #a8c7fa; color: #131520; border-color: #a8c7fa; }");
                        W("            .toast { background-color: #e3e3e3; color: #131520; font-weight: bold; }");
                        W("        }");
                        W("    </style>");
                        W("    <script>");
                        W("        function filterRows() {");
                        W("            var input = document.getElementById('htmlFilter');");
                        W("            var filterText = input.value.trim().toLowerCase();");
                        W("            var keywords = filterText.split(/[ 　]+/).filter(Boolean);");
                        W("            var listItems = document.querySelectorAll('ol li');");
                        W("            var visibleCount = 0;");
                        W("            listItems.forEach(function(li) {");
                        W("                var pathText = li.querySelector('.file-path').textContent;");
                        W("                var text = pathText.toLowerCase();");
                        W("                var isMatch = true;");
                        W("                for (var i = 0; i < keywords.length; i++) {");
                        W("                    if (text.indexOf(keywords[i]) === -1) {");
                        W("                        isMatch = false;");
                        W("                        break;");
                        W("                    }");
                        W("                }");
                        W("                if (isMatch) {");
                        W("                    li.style.display = 'flex';");
                        W("                    visibleCount++;");
                        W("                } else {");
                        W("                    li.style.display = 'none';");
                        W("                }");
                        W("            });");
                        W("            document.getElementById('hitCount').textContent = visibleCount;");
                        W("        }");
                        
                        W("        function toggleDirDisplay(checkbox) {");
                        W("            var ol = document.getElementById('resultList');");
                        W("            if (checkbox.checked) {");
                        W("                ol.classList.remove('hide-dir');");
                        W("            } else {");
                        W("                ol.classList.add('hide-dir');");
                        W("            }");
                        W("        }");

                        W("        function execCopy(text, msg) {");
                        W("            navigator.clipboard.writeText(text).then(function() {");
                        W("                var toast = document.getElementById('toast');");
                        W("                toast.textContent = msg;");
                        W("                toast.classList.add('show');");
                        W("                setTimeout(function() {");
                        W("                    toast.classList.remove('show');");
                        W("                }, 1500);");
                        W("            }).catch(function(err) {");
                        W("                console.error('コピーに失敗しました: ', err);");
                        W("            });");
                        W("        }");
                        W("        function copyFullPath(liElement) {");
                        W("            var text = liElement.querySelector('.file-path').textContent;");
                        W("            execCopy(text, '📄 フルパスをコピーしました');");
                        W("        }");
                        W("        function copyFolderPath(e, btn) {");
                        W("            e.stopPropagation();");
                        W("            var li = btn.closest('li');");
                        W("            var text = li.querySelector('.file-path').textContent;");
                        W("            var lastSlash = text.lastIndexOf(\"\\\\\");");
                        W("            if (lastSlash !== -1) {");
                        W("                text = text.substring(0, lastSlash + 1);");
                        W("            }");
                        W("            execCopy(text, '📁 フォルダパスをコピーしました');");
                        W("        }");
                        W("        window.addEventListener('keydown', function(e) {");
                        W("            var input = document.getElementById('htmlFilter');");
                        W("            if (document.activeElement === input || e.ctrlKey || e.altKey || e.metaKey) return;");
                        W("            if (e.key === ' ' || e.key === 'Spacebar') return;");
                        W("            if (e.key.length === 1) {");
                        W("                input.focus();");
                        W("            }");
                        W("        });");
                        W("    </script>");
                        W("</head>");
                        W("<body>");
                        W("    <h1>検索対象リスト: 「" + listName + "」 ➔ キーワード: 「" + keyword + "」</h1>");
                        W("    <div class=\"summary\">検索日時: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | 合計ヒット件数: <span id=\"hitCount\">" + matchCount + "</span> / " + matchCount + " 件 (行クリックでフルパス / フォルダボタンで親パスをコピー)</div>");
                        
                        W("    <div class=\"control-panel\">");
                        W("        <div class=\"filter-box\">");
                        W("            <input type=\"text\" id=\"htmlFilter\" oninput=\"filterRows()\" placeholder=\"文字入力で自動絞り込み（スペース区切りでAND）\">");
                        W("        </div>");
                        W("        <label class=\"switch-box\">");
                        W("            <input type=\"checkbox\" id=\"dirToggle\" checked onchange=\"toggleDirDisplay(this)\">フォルダ名を表示する");
                        W("        </label>");
                        W("    </div>");
                        
                        W("    <ol id=\"resultList\">");

                        string line2;
                        while ((line2 = reader2.ReadLine()) != null)
                        {
                            bool isMatch = true;
                            foreach (string kw in keywords)
                            {
                                if (line2.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    isMatch = false;
                                    break;
                                }
                            }

                            if (isMatch)
                            {
                                string escapedLine = System.Security.SecurityElement.Escape(line2);
                                string lowerLine = line2.ToLower();
                                string emoji = "📄";
                                
                                if (lowerLine.EndsWith(".pdf")) emoji = "📕";
                                else if (lowerLine.EndsWith(".xlsx") || lowerLine.EndsWith(".xls")) emoji = "📗";
                                else if (lowerLine.EndsWith(".docx") || lowerLine.EndsWith(".doc")) emoji = "📘";
                                else if (lowerLine.EndsWith(".zip") || lowerLine.EndsWith(".gz") || lowerLine.EndsWith(".rar") || lowerLine.EndsWith(".7z")) emoji = "📦";
                                else if (lowerLine.EndsWith(".txt") || lowerLine.EndsWith(".log") || lowerLine.EndsWith(".ini")) emoji = "📝";
                                else if (lowerLine.EndsWith(".exe") || lowerLine.EndsWith(".bat") || lowerLine.EndsWith(".msi")) emoji = "⚙️";
                                else if (lowerLine.EndsWith(".png") || lowerLine.EndsWith(".jpg") || lowerLine.EndsWith(".jpeg") || lowerLine.EndsWith(".gif") || lowerLine.EndsWith(".bmp")) emoji = "🖼️";

                                string dirPart = "";
                                string namePart = escapedLine;
                                int lastSlashIdx = escapedLine.LastIndexOf("\\");
                                if (lastSlashIdx != -1)
                                {
                                    dirPart = escapedLine.Substring(0, lastSlashIdx + 1);
                                    namePart = escapedLine.Substring(lastSlashIdx + 1);
                                }

                                W("        <li onclick=\"copyFullPath(this)\">");
                                W("            <span class=\"file-path\" style=\"display:none;\">" + escapedLine + "</span>");
                                W("            <div class=\"file-info\">");
                                W("                <span class=\"file-icon\">" + emoji + "</span>");
                                W("                <span><span class=\"path-dir\">" + dirPart + "</span><span class=\"path-name\">" + namePart + "</span></span>");
                                W("            </div>");
                                W("            <div class=\"btn-group\">");
                                W("                <button class=\"copy-btn\" onclick=\"copyFolderPath(event, this)\">📁 フォルダ</button>");
                                W("            </div>");
                                W("        </li>");
                            }
                        }

                        W("    </ol>");
                        W("    <div id=\"toast\" class=\"toast\"></div>");
                        W("</body>");
                        W("</html>");
                    }

                    lblStatus.Text = "完了：" + matchCount + " 件ヒット。";
                    Process.Start(htmlPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("検索中にエラーが発生しました:\n" + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "検索エラー";
            }
            finally
            {
                SetControlsEnabled(true);
                this.Cursor = DefaultCursor;
                cmbKeyword.Focus();
            }
        }

        private void LoadHistory()
        {
            if (File.Exists(historyPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(historyPath, Encoding.UTF8);
                    
                    if (lines.Length > 0)
                    {
                        lastSelectedListName = lines[0].Trim();
                    }

                    cmbKeyword.Items.Clear();
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(lines[i].Trim()))
                        {
                            cmbKeyword.Items.Add(lines[i].Trim());
                        }
                    }
                }
                catch { }
            }
        }

        private void SaveHistory(string newKeyword, bool updateKeywordList)
        {
            try
            {
                List<string> historyList = new List<string>();
                historyList.Add(lastSelectedListName);

                List<string> keywordItems = new List<string>();
                if (updateKeywordList && !string.IsNullOrEmpty(newKeyword))
                {
                    keywordItems.Add(newKeyword);
                }

                foreach (object item in cmbKeyword.Items)
                {
                    string strItem = item.ToString();
                    if (strItem != newKeyword && keywordItems.Count < 20)
                    {
                        keywordItems.Add(strItem);
                    }
                }

                if (updateKeywordList)
                {
                    cmbKeyword.Items.Clear();
                    foreach (string h in keywordItems) cmbKeyword.Items.Add(h);
                    cmbKeyword.Text = newKeyword;
                }

                historyList.AddRange(keywordItems);
                File.WriteAllLines(historyPath, historyList.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        private void CompressFile(string sourcePath, string destPath, Encoding encoding)
        {
            using (StreamReader reader = new StreamReader(sourcePath, encoding))
            using (FileStream destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            using (GZipStream gzipStream = new GZipStream(destStream, CompressionMode.Compress))
            using (StreamWriter writer = new StreamWriter(gzipStream, encoding))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    writer.WriteLine(line);
                }
            }
        }
    }
}