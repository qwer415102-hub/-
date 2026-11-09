using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
public static class Verify {
    static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); }
    [STAThread] public static void Main(string[] args) {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try { Run(args); } catch (Exception ex) { Console.WriteLine(ex.GetType().FullName); Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); Environment.ExitCode = 1; }
    }
    static void Run(string[] args) {
        string root = Path.Combine(Path.GetTempPath(), "NameExtractor-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            foreach (string name in new string[] { "研究报告.docx", "archive.tar.gz", "README", ".gitignore", "空 格 & 100% ! [1].txt", "__名称提取_旧结果.txt" }) File.WriteAllText(Path.Combine(root,name),"sample");
            Directory.CreateDirectory(Path.Combine(root,"实验.v2"));
            File.WriteAllText(Path.Combine(root,"实验.v2","研究报告.pdf"),"sample");
            string hidden = Path.Combine(root,"hidden.txt"); File.WriteAllText(hidden,"sample"); File.SetAttributes(hidden,FileAttributes.Hidden);
            var options = new ScanOptions { Roots = new string[] { root }, Extension = false };
            var result = Extractor.Scan(options);
            Check(result.Names.Count == 5 && result.Names.Contains("研究报告") && result.Names.Contains("archive.tar") && result.Names.Contains("README") && result.Names.Contains(".gitignore") && result.Names.Contains("空 格 & 100% ! [1]"),"stem / Chinese / special characters / dotfile / excludes previous output");
            options.Extension = true; result = Extractor.Scan(options);
            Check(result.Names.Contains("研究报告.docx") && result.Names.Contains("archive.tar.gz"),"full extension");
            options.Kind = "folders"; options.Extension = false; result = Extractor.Scan(options);
            Check(result.Names.Count == 1 && result.Names[0] == "实验.v2","folder dots preserved");
            options.Kind = "all"; options.Recursive = true; options.Relative = true; result = Extractor.Scan(options);
            Check(result.Names.Count == 7 && result.Names.Contains("实验.v2\\研究报告"),"recursive / relative path / both kinds");
            options.Kind = "files"; options.Relative = false; result = Extractor.Scan(options);
            Check(result.Names.Count(x => x == "研究报告") == 2,"duplicate resulting names preserved");
            options.Hidden = true; result = Extractor.Scan(options);
            Check(result.Names.Count == 7 && result.Names.Contains("hidden"),"hidden option");
            options.Roots = new string[] { root, Path.Combine(root,"实验.v2") }; options.Relative = true; result = Extractor.Scan(options);
            Check(result.Names.Count == 7 && result.Names.All(x => Path.IsPathRooted(x)),"overlapping roots / multi-root paths");
            string output = Path.Combine(root,"__名称提取_测试.txt"); Extractor.Write(output,result.Names,false);
            byte[] bytes = File.ReadAllBytes(output);
            Check(bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 && File.ReadAllLines(output).SequenceEqual(result.Names),"UTF-8 BOM / all lines round trip");
            bool refused = false; try { Extractor.Write(output,new string[] { "bad" },false); } catch (IOException) { refused = true; }
            Check(refused && File.ReadAllLines(output).SequenceEqual(result.Names),"quick output cannot overwrite");
            bool missing = false; try { Extractor.Scan(new ScanOptions { Roots = new string[] { Path.Combine(root,"missing") } }); } catch (DirectoryNotFoundException) { missing = true; }
            Check(missing,"missing folder error");
            Application.EnableVisualStyles();
            var form = new MainForm(new string[] { root }); {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000,-20000); form.ShowInTaskbar = false;
                form.Show(); Application.DoEvents();
                System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                form.GetType().GetMethod("StartScan",flags).Invoke(form,new object[] { null,EventArgs.Empty });
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                while ((bool)form.GetType().GetField("busy",flags).GetValue(form) && DateTime.UtcNow < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                var preview = (TextBox)form.GetType().GetField("preview",flags).GetValue(form);
                Check(preview.Text.Contains("研究报告") && !preview.Text.Contains("研究报告.docx"),"GUI async extraction and preview");
                ((RadioButton)form.GetType().GetField("full",flags).GetValue(form)).Checked = true;
                Check(preview.Text.Length == 0 && !((Button)form.GetType().GetField("save",flags).GetValue(form)).Enabled,"mode change invalidates stale export");
                System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                form.GetType().GetMethod("StartScan",flags).Invoke(form,new object[] { null,EventArgs.Empty });
                deadline = DateTime.UtcNow.AddSeconds(10);
                while ((bool)form.GetType().GetField("busy",flags).GetValue(form) && DateTime.UtcNow < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                Check(preview.Text.Contains("研究报告.docx"),"GUI full extension mode");
                using (var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save(args[0]); }
            }
            Console.WriteLine("ALL CHECKS PASSED");
        } finally {
            // Only remove this explicitly created, randomly named temporary fixture.
            if (Path.GetDirectoryName(root) == Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) && Path.GetFileName(root).StartsWith("NameExtractor-test-")) Directory.Delete(root,true);
        }
    }
}
