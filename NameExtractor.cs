using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

public class ScanOptions {
    public string[] Roots;
    public bool Extension = true, Recursive, Hidden, Relative;
    public string Kind = "files";
}
public class ScanResult {
    public List<string> Names = new List<string>();
    public List<string> Warnings = new List<string>();
}
public static class Extractor {
    public const string Prefix = "__名称提取_";
    public static string AppDir = AppDomain.CurrentDomain.BaseDirectory;
    public static bool IsTool(string path) {
        string full = Path.GetFullPath(path);
        if (String.Equals(full, System.Reflection.Assembly.GetExecutingAssembly().Location, StringComparison.OrdinalIgnoreCase)) return true;
        return String.Equals(Path.GetDirectoryName(full), AppDir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            && (Path.GetFileName(full) == "提取文件名-不带后缀.bat" || Path.GetFileName(full) == "提取文件名-带后缀.bat");
    }
    public static ScanResult Scan(ScanOptions options) {
        var result = new ScanResult();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string rootText in options.Roots) {
            string root = Path.GetFullPath(rootText);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("文件夹不存在：" + root);
            var pending = new Stack<string>(); pending.Push(root);
            while (pending.Count > 0) {
                string dir = pending.Pop();
                FileSystemInfo[] entries;
                try { entries = new DirectoryInfo(dir).GetFileSystemInfos(); }
                catch (Exception ex) {
                    if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)) throw;
                    result.Warnings.Add(dir + "：" + ex.Message); continue;
                }
                Array.Sort(entries, delegate(FileSystemInfo a, FileSystemInfo b) { return StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name); });
                foreach (FileSystemInfo entry in entries) {
                    try {
                        bool folder = (entry.Attributes & FileAttributes.Directory) != 0;
                        if (!options.Hidden && (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                        if (folder && options.Recursive) {
                            if ((entry.Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(entry.FullName);
                            else result.Warnings.Add("未递归进入链接文件夹：" + entry.FullName);
                        }
                        if ((options.Kind == "files" && folder) || (options.Kind == "folders" && !folder)) continue;
                        if (!folder && (IsTool(entry.FullName) || (entry.Name.StartsWith(Prefix, StringComparison.Ordinal) && entry.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)))) continue;
                        if (!seen.Add(entry.FullName)) continue;
                        string name = entry.Name;
                        if (!folder && !options.Extension) {
                            // Keep extensionless dotfiles such as .gitignore intact.
                            int dot = name.LastIndexOf('.');
                            if (dot > 0) name = name.Substring(0, dot);
                        }
                        if (options.Relative) {
                            string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                            string relativeDir = entry.FullName.Substring(prefix.Length);
                            int slash = relativeDir.LastIndexOf(Path.DirectorySeparatorChar);
                            if (slash >= 0) name = relativeDir.Substring(0, slash + 1) + name;
                            if (options.Roots.Length > 1) name = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + name;
                        }
                        result.Names.Add(name);
                    } catch (Exception ex) {
                        if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)) throw;
                        result.Warnings.Add(entry.FullName + "：" + ex.Message);
                    }
                }
            }
        }
        result.Names.Sort(StringComparer.CurrentCultureIgnoreCase);
        return result;
    }
    public static void Write(string path, IEnumerable<string> names, bool overwrite) {
        using (var stream = new FileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(true))) {
            writer.NewLine = "\r\n";
            foreach (string name in names) writer.WriteLine(name);
        }
    }
}

public class MainForm : Form {
    ListBox roots = new ListBox();
    RadioButton stem = new RadioButton(), full = new RadioButton();
    ComboBox kind = new ComboBox();
    CheckBox recursive = new CheckBox(), hidden = new CheckBox(), relative = new CheckBox();
    TextBox preview = new TextBox();
    Label status = new Label();
    Button scan, save, copy, warnings;
    ScanResult result;
    bool busy;
    public MainForm(string[] paths) {
        Text = "名称提取工具"; Font = new Font("Microsoft YaHei UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(840, 720); MinimumSize = new Size(760, 680);
        StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(245,247,251);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 8 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        Controls.Add(layout);
        var title = new Label { Text = "批量提取名称\n选择文件夹 → 选择模式 → 提取并导出", Dock = DockStyle.Fill, Font = new Font(Font.FontFamily, 13), ForeColor = Color.FromArgb(34,56,83) };
        layout.Controls.Add(title);
        var folderPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,128));
        roots.Dock = DockStyle.Fill; roots.HorizontalScrollbar = true; roots.SelectionMode = SelectionMode.MultiExtended;
        roots.AllowDrop = true; roots.DragEnter += delegate(object s, DragEventArgs e) { if (!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        roots.DragDrop += delegate(object s, DragEventArgs e) { AddPaths((string[])e.Data.GetData(DataFormats.FileDrop)); };
        folderPanel.Controls.Add(roots,0,0);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        actions.Controls.Add(Button("添加文件夹", delegate { using (var dlg = new FolderBrowserDialog { Description = "选择要提取名称的文件夹", ShowNewFolderButton = false }) { if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(new string[] { dlg.SelectedPath }); } }));
        actions.Controls.Add(Button("移除选中", delegate { if (busy) return; foreach (var item in roots.SelectedItems.Cast<object>().ToArray()) roots.Items.Remove(item); InvalidateResult(); }));
        actions.Controls.Add(Button("清空", delegate { if (!busy) { roots.Items.Clear(); InvalidateResult(); } }));
        folderPanel.Controls.Add(actions,1,0); layout.Controls.Add(folderPanel);
        var modes = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0,8,0,0) };
        stem.Text = "只提取文件名（不带后缀）"; stem.AutoSize = true; stem.Checked = true;
        full.Text = "文件名＋后缀"; full.AutoSize = true; full.Margin = new Padding(24,3,3,3);
        modes.Controls.Add(stem); modes.Controls.Add(full); layout.Controls.Add(modes);
        var types = new FlowLayoutPanel { Dock = DockStyle.Fill };
        types.Controls.Add(new Label { Text = "提取内容", AutoSize = true, Margin = new Padding(0,7,12,0) });
        kind.DropDownStyle = ComboBoxStyle.DropDownList; kind.Width = 180;
        kind.Items.AddRange(new object[] { "文件", "子文件夹", "文件和子文件夹" }); kind.SelectedIndex = 0;
        types.Controls.Add(kind);
        types.Controls.Add(new Label { Text = "文件夹名称始终保留完整，包括点号", AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(16,7,0,0) }); layout.Controls.Add(types);
        var flags = new FlowLayoutPanel { Dock = DockStyle.Fill };
        recursive.Text = "包括所有子文件夹"; hidden.Text = "包括隐藏／系统项"; relative.Text = "保留路径（区分重名）";
        foreach (var c in new CheckBox[] { recursive, hidden, relative }) { c.AutoSize = true; c.Margin = new Padding(0,5,22,0); flags.Controls.Add(c); c.CheckedChanged += delegate { InvalidateResult(); }; }
        layout.Controls.Add(flags);
        preview.Multiline = true; preview.ReadOnly = true; preview.ScrollBars = ScrollBars.Both; preview.WordWrap = false;
        preview.Dock = DockStyle.Fill; preview.BackColor = Color.White; preview.Font = new Font("Microsoft YaHei UI",10);
        layout.Controls.Add(preview);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0,8,0,0) };
        scan = Button("开始提取", StartScan); scan.BackColor = Color.FromArgb(43,99,217); scan.ForeColor = Color.White;
        copy = Button("复制全部", delegate { try { Clipboard.SetText(String.Join("\r\n", result.Names)); status.Text = "已复制 " + result.Names.Count + " 项"; } catch (Exception ex) { Error(ex); } });
        save = Button("导出 TXT", Save);
        warnings = Button("查看跳过详情", delegate { MessageBox.Show(this,String.Join("\r\n",result.Warnings),"扫描提示"); });
        bottom.Controls.AddRange(new Control[] { scan, copy, save, warnings }); layout.Controls.Add(bottom);
        status.Dock = DockStyle.Fill; status.Text = "可将多个文件夹拖入上方列表。所有处理均在本机完成。"; status.ForeColor = Color.DimGray; layout.Controls.Add(status);
        stem.CheckedChanged += delegate { InvalidateResult(); }; full.CheckedChanged += delegate { InvalidateResult(); };
        kind.SelectedIndexChanged += delegate { stem.Enabled = full.Enabled = kind.SelectedIndex != 1 && !busy; InvalidateResult(); };
        FormClosing += delegate(object s, FormClosingEventArgs e) { if (busy) { e.Cancel = true; status.Text = "正在提取，请完成后关闭窗口。"; } };
        AddPaths(paths); InvalidateResult();
    }
    Button Button(string text, EventHandler action) {
        var b = new Button { Text = text, Width = 116, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Margin = new Padding(0,0,10,5) };
        b.FlatAppearance.BorderColor = Color.FromArgb(210,217,227); b.Click += action; return b;
    }
    void AddPaths(string[] paths) {
        if (busy) return;
        foreach (string p in paths) { if (!Directory.Exists(p)) continue; string path = Path.GetFullPath(p); if (!roots.Items.Cast<string>().Any(x => String.Equals(x,path,StringComparison.OrdinalIgnoreCase))) roots.Items.Add(path); }
        InvalidateResult();
    }
    void InvalidateResult() {
        if (busy) return; result = null; preview.Clear();
        if (copy != null) { copy.Enabled = save.Enabled = warnings.Enabled = false; status.Text = "已选择 " + roots.Items.Count + " 个文件夹；选择模式后点击“开始提取”。"; }
    }
    void StartScan(object sender, EventArgs e) {
        if (roots.Items.Count == 0) { MessageBox.Show(this,"请先添加文件夹，也可以拖入文件夹。","提示"); return; }
        var options = new ScanOptions { Roots = roots.Items.Cast<string>().ToArray(), Extension = full.Checked, Kind = new string[] { "files","folders","all" }[kind.SelectedIndex], Recursive = recursive.Checked, Hidden = hidden.Checked, Relative = relative.Checked };
        InvalidateResult(); busy = true; scan.Enabled = roots.Enabled = kind.Enabled = stem.Enabled = full.Enabled = recursive.Enabled = hidden.Enabled = relative.Enabled = false;
        status.Text = "正在提取，请稍候…";
        var worker = new BackgroundWorker(); worker.DoWork += delegate(object s, DoWorkEventArgs args) { args.Result = Extractor.Scan(options); };
        worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args) {
            busy = false; scan.Enabled = roots.Enabled = kind.Enabled = recursive.Enabled = hidden.Enabled = relative.Enabled = true; stem.Enabled = full.Enabled = kind.SelectedIndex != 1;
            if (args.Error != null) { Error(args.Error); worker.Dispose(); return; }
            result = (ScanResult)args.Result;
            preview.Text = String.Join("\r\n",result.Names.Take(5000));
            copy.Enabled = save.Enabled = result.Names.Count > 0; warnings.Enabled = result.Warnings.Count > 0;
            status.Text = "共 " + result.Names.Count + " 项" + (result.Names.Count > 5000 ? "（预览前 5000 项，导出全部）" : "") + "；提示 " + result.Warnings.Count + " 条。";
            worker.Dispose();
        }; worker.RunWorkerAsync();
    }
    void Save(object sender, EventArgs e) {
        using (var dialog = new SaveFileDialog { Filter = "文本文件 (*.txt)|*.txt", DefaultExt = "txt", AddExtension = true, FileName = Extractor.Prefix + (kind.SelectedIndex == 1 ? "文件夹" : full.Checked ? "带后缀" : "不带后缀") + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt", OverwritePrompt = true }) {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try { Extractor.Write(dialog.FileName, result.Names, true); status.Text = "已导出 " + result.Names.Count + " 项：" + dialog.FileName; }
            catch (Exception ex) { Error(ex); }
        }
    }
    void Error(Exception ex) { status.Text = "操作失败，请查看提示。"; MessageBox.Show(this,ex.Message,"操作失败",MessageBoxButtons.OK,MessageBoxIcon.Error); }
}
public static class Program {
    [STAThread] public static void Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            if (args.Length >= 3 && args[0] == "--quick") {
                bool extension = args[1] == "full";
                string root = Path.GetFullPath(args[2]);
                var result = Extractor.Scan(new ScanOptions { Roots = new string[] { root }, Extension = extension });
                if (result.Warnings.Count > 0) { MessageBox.Show(String.Join("\r\n",result.Warnings),"提取未完成，请使用主程序查看",MessageBoxButtons.OK,MessageBoxIcon.Warning); Environment.ExitCode = 1; return; }
                string output = Path.Combine(root, Extractor.Prefix + (extension ? "带后缀_" : "不带后缀_") + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt");
                Extractor.Write(output,result.Names,false);
                MessageBox.Show("已提取 " + result.Names.Count + " 个文件名。\r\n\r\n已保存到：\r\n" + output,"提取完成"); return;
            }
            Application.Run(new MainForm(args));
        } catch (Exception ex) { MessageBox.Show(ex.Message,"名称提取工具",MessageBoxButtons.OK,MessageBoxIcon.Error); Environment.ExitCode = 1; }
    }
}
