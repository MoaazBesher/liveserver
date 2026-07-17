using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) =>
            {
                var msg = "Unhandled error:\n" + e.Exception.Message +
                          "\n\n" + e.Exception.StackTrace;
                MessageBox.Show(msg, "Universal Live Server - Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                var msg = "Fatal error:\n" + (ex != null ? ex.Message : "Unknown") +
                          "\n\nPlease report this issue.";
                MessageBox.Show(msg, "Universal Live Server - Fatal Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            string initialPath = null;
            if (args.Length > 0)
            {
                var path = args[0].Trim().Trim('"');
                if (Directory.Exists(path)) initialPath = path;
            }

            Application.Run(new MainForm(initialPath));
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to start:\n" + ex.Message,
                "Universal Live Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

// ── Version & update config ───────────────────────────────────────────────────
static class AppVersion
{
    public static readonly string Current = "1.0.0";

    // ── CHANGE THESE to match your GitHub repo ────────────────────────────────
    public static readonly string GitHubUser = "MoaazBesher";
    public static readonly string GitHubRepo = "liveserver";
    // ───────────────────────────────────────────────────────────────────────────

    public static string RawVersionUrl
    {
        get { return "https://raw.githubusercontent.com/" + GitHubUser + "/" + GitHubRepo + "/main/version.json"; }
    }

    public static string ReleasesUrl
    {
        get { return "https://github.com/" + GitHubUser + "/" + GitHubRepo + "/releases"; }
    }

    public static string DownloadUrlFor(string version)
    {
        return ReleasesUrl + "/download/v" + version + "/UniversalLiveServer.zip";
    }
}

// ── Vista-style folder picker ────────────────────────────────────────────────
static class FolderBrowser
{
    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint n, IntPtr types);
        void SetFileTypeIndex(uint i);
        void GetFileTypeIndex(out uint i);
        void Advise(IntPtr sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint fos);
        void GetOptions(out uint fos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string ext);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
        void GetResults(out IShellItemArray ppenum);
        void GetSelectedItems(out IShellItemArray ppenum);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdn, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attribs);
        void Compare(IShellItem psi, uint hint, out int order);
    }

    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray { }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string path, IntPtr pbc, ref Guid riid, out IShellItem ppv);

    private static readonly Guid CLSID_FileOpenDialog = new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
    private static readonly Guid IID_IShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private const uint FOS_PICKFOLDERS = 0x00000020;
    private const uint FOS_FORCEFILESYSTEM = 0x00000040;
    private const uint FOS_PATHMUSTEXIST = 0x00000800;
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    public static string Show(string initialPath, IWin32Window owner = null)
    {
        try
        {
            var dialog = (IFileOpenDialog)Activator.CreateInstance(
                Type.GetTypeFromCLSID(CLSID_FileOpenDialog));
            try
            {
                uint opts;
                dialog.GetOptions(out opts);
                dialog.SetOptions(opts | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST);
                dialog.SetTitle("Select Project Folder");
                dialog.SetOkButtonLabel("Select Folder");

                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                {
                    var riid = IID_IShellItem;
                    IShellItem folder;
                    if (SHCreateItemFromParsingName(initialPath, IntPtr.Zero, ref riid, out folder) == 0)
                        dialog.SetFolder(folder);
                }

                var hwnd = owner != null ? owner.Handle : IntPtr.Zero;
                if (dialog.Show(hwnd) != 0) return null;

                IShellItem result;
                dialog.GetResult(out result);
                string path;
                result.GetDisplayName(SIGDN_FILESYSPATH, out path);
                return path;
            }
            finally { Marshal.ReleaseComObject(dialog); }
        }
        catch
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select Project Folder";
                dlg.SelectedPath = Directory.Exists(initialPath)
                    ? initialPath
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedPath : null;
            }
        }
    }
}

// ── Project type information ─────────────────────────────────────────────────
class ProjectInfo
{
    public string TypeName { get; set; }
    public string Language { get; set; }
    public string Framework { get; set; }
    public string RunCommand { get; set; }
    public string DisplayCommand { get; set; }
    public string InstallCommand { get; set; }
    public string InstallLabel { get; set; }
    public int DefaultPort { get; set; }
    public string[] AvailableScripts { get; set; }
    public string SelectedScript { get; set; }
}

// ── Project detection engine ─────────────────────────────────────────────────
static class ProjectDetector
{
    public static ProjectInfo Detect(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return null;

        var files = Directory.GetFiles(path);
        var dirs = Directory.GetDirectories(path);
        var allFiles = files.Concat(dirs).Select(f => Path.GetFileName(f).ToLowerInvariant()).ToArray();
        var allFilePaths = files.ToDictionary(f => Path.GetFileName(f).ToLowerInvariant(), f => f);
        var allDirPaths = dirs.ToDictionary(d => Path.GetFileName(d).ToLowerInvariant(), d => d);

        // 1. Docker Compose
        if (allFiles.Any(f => f == "docker-compose.yml" || f == "docker-compose.yaml" || f == "docker-compose.override.yml"))
            return MakeInfo("Docker Compose", "Docker", "Docker Compose",
                "docker-compose up", "docker-compose up", null, null, 8080);

        // 2. Docker
        if (allFiles.Any(f => f == "dockerfile" || f == "dockerfile.txt"))
            return MakeInfo("Docker", "Docker", "Docker",
                "docker build -t project . && docker run -p {port}:80 project",
                "docker build && docker run", null, null, 8080);

        // 3. Next.js
        if (allFiles.Any(f => f.StartsWith("next.config.")))
        {
            var info = MakeInfo("Next.js", "Node.js", "Next.js",
                "npm run dev", "npm run dev", "npm install", "npm install", 3000);
            info.AvailableScripts = GetNpmScripts(path);
            return info;
        }

        // 4. Nuxt.js
        if (allFiles.Any(f => f.StartsWith("nuxt.config.")))
        {
            var info = MakeInfo("Nuxt.js", "Node.js", "Nuxt.js",
                "npm run dev", "npm run dev", "npm install", "npm install", 3000);
            info.AvailableScripts = GetNpmScripts(path);
            return info;
        }

        // 5. Astro
        if (allFiles.Any(f => f.StartsWith("astro.config.")))
        {
            var info = MakeInfo("Astro", "Node.js", "Astro",
                "npm run dev", "npm run dev", "npm install", "npm install", 4321);
            info.AvailableScripts = GetNpmScripts(path);
            return info;
        }

        // 6. Gatsby
        if (allFiles.Any(f => f.StartsWith("gatsby-config.")))
        {
            var info = MakeInfo("Gatsby", "Node.js", "Gatsby",
                "npm run develop", "npm run develop", "npm install", "npm install", 8000);
            info.AvailableScripts = GetNpmScripts(path);
            return info;
        }

        // 7. SvelteKit
        if (allFiles.Any(f => f == "svelte.config.js" || f == "svelte.config.ts"))
        {
            var info = MakeInfo("SvelteKit", "Node.js", "SvelteKit",
                "npm run dev", "npm run dev", "npm install", "npm install", 5173);
            info.AvailableScripts = GetNpmScripts(path);
            return info;
        }

        // 8. Angular
        if (allFiles.Any(f => f == "angular.json"))
        {
            var info = MakeInfo("Angular", "Node.js", "Angular CLI",
                "npx ng serve --port {port} --host 0.0.0.0",
                "ng serve", "npm install", "npm install", 4200);
            info.AvailableScripts = GetNpmScripts(path);
            return info;
        }

        // 9. Vite project (React / Vue / Svelte / Vanilla)
        if (allFiles.Any(f => f.StartsWith("vite.config.")))
        {
            var pkg = TryReadPackageJson(path);
            var framework = "Vite";
            if (pkg != null)
            {
                string allDeps = "";
                if (pkg.ContainsKey("dependencies")) allDeps += pkg["dependencies"] + " ";
                if (pkg.ContainsKey("devDependencies")) allDeps += pkg["devDependencies"] + " ";
                if (allDeps.Contains("react")) framework = "React (Vite)";
                else if (allDeps.Contains("vue")) framework = "Vue (Vite)";
                else if (allDeps.Contains("svelte")) framework = "Svelte (Vite)";
                else if (allDeps.Contains("lit")) framework = "Lit (Vite)";
                else if (allDeps.Contains("preact")) framework = "Preact (Vite)";
            }
            var info = MakeInfo(framework, "Node.js", "Vite",
                "npx vite --port {port} --host",
                "vite --port {port}", "npm install", "npm install", 5173);
            info.AvailableScripts = GetNpmScripts(path);
            return info;
        }

        // 10. Vue CLI
        if (allFiles.Any(f => f == "vue.config.js" || f == "vue.config.ts"))
        {
            var info = MakeInfo("Vue CLI", "Node.js", "Vue CLI",
                "npx vue-cli-service serve --port {port}",
                "vue-cli-service serve", "npm install", "npm install", 8080);
            info.AvailableScripts = GetNpmScripts(path);
            return info;
        }

        // 11. React CRA (Create React App)
        if (allFiles.Contains("package.json"))
        {
            var pkg = TryReadPackageJson(path);
            if (pkg != null)
            {
                string allDeps = "";
                if (pkg.ContainsKey("dependencies")) allDeps += pkg["dependencies"] + " ";
                if (pkg.ContainsKey("devDependencies")) allDeps += pkg["devDependencies"] + " ";
                if (allDeps.Contains("react-scripts"))
                {
                    var info = MakeInfo("React CRA", "Node.js", "Create React App",
                        "npm start", "npm start", "npm install", "npm install", 3000);
                    info.AvailableScripts = GetNpmScripts(path);
                    return info;
                }
            }
        }

        // 12. Node.js with package.json (generic)
        if (allFiles.Contains("package.json"))
        {
            var scripts = GetNpmScripts(path);
            var pkg = TryReadPackageJson(path);
            string runCmd = null;
            string displayCmd = null;
            int defPort = 3000;

            if (pkg != null && pkg.ContainsKey("main") && !string.IsNullOrEmpty(pkg["main"]))
            {
                var mainFile = pkg["main"].Trim().Trim('"');
                if (mainFile.EndsWith(".js") || mainFile.EndsWith(".ts") || mainFile.EndsWith(".mjs"))
                {
                    runCmd = "node " + mainFile;
                    displayCmd = "node " + mainFile;
                }
            }

            if (runCmd == null)
            {
                foreach (var guess in new[] { "server.js", "app.js", "index.js", "main.js", "server.ts", "app.ts", "index.ts" })
                {
                    if (File.Exists(Path.Combine(path, guess)))
                    {
                        runCmd = "node " + guess;
                        displayCmd = "node " + guess;
                        break;
                    }
                }
            }

            if (runCmd == null && scripts.Length > 0)
            {
                var preferred = new[] { "dev", "start", "serve", "watch", "develop" };
                foreach (var p in preferred)
                {
                    if (scripts.Any(s => s.Equals(p, StringComparison.OrdinalIgnoreCase)))
                    {
                        runCmd = "npm run " + p;
                        displayCmd = "npm run " + p;
                        break;
                    }
                }
            }

            if (runCmd == null)
            {
                runCmd = "npx live-server --port={port} --no-browser";
                displayCmd = "live-server";
                defPort = 5500;
            }

            var nodeInfo = MakeInfo("Node.js", "Node.js", "Node.js",
                runCmd, displayCmd, "npm install", "npm install", defPort);
            nodeInfo.AvailableScripts = scripts;
            return nodeInfo;
        }

        // 13. Laravel
        if (allFiles.Contains("artisan"))
        {
            return MakeInfo("Laravel", "PHP", "Laravel",
                "php artisan serve --port={port} --host=0.0.0.0",
                "php artisan serve", "composer install", "composer install", 8000);
        }

        // 14. Generic PHP / Composer
        if (allFiles.Contains("composer.json") || HasExtension(files, ".php"))
        {
            string docRoot = path;
            if (Directory.Exists(Path.Combine(path, "public"))) docRoot = Path.Combine(path, "public");
            else if (Directory.Exists(Path.Combine(path, "web"))) docRoot = Path.Combine(path, "web");
            else if (Directory.Exists(Path.Combine(path, "htdocs"))) docRoot = Path.Combine(path, "htdocs");
            else if (Directory.Exists(Path.Combine(path, "www"))) docRoot = Path.Combine(path, "www");
            return MakeInfo("PHP", "PHP", allFiles.Contains("composer.json") ? "Composer" : "PHP",
                "php -S 0.0.0.0:{port} -t \"" + docRoot + "\"",
                "php -S", "composer install", allFiles.Contains("composer.json") ? "composer install" : null, 8000);
        }

        // 15. Django
        if (allFiles.Contains("manage.py"))
        {
            return MakeInfo("Django", "Python", "Django",
                "python manage.py runserver 0.0.0.0:{port}",
                "python manage.py runserver",
                "pip install -r requirements.txt", "pip install -r requirements.txt", 8000);
        }

        // 16. Flask
        if (allFiles.Contains("app.py") || allFiles.Contains("wsgi.py") || HasFileContaining(files, path, "flask"))
        {
            var appFile = allFiles.Contains("wsgi.py") ? "wsgi.py" : "app.py";
            return MakeInfo("Flask", "Python", "Flask",
                "set FLASK_APP=" + appFile + " && flask run --host=0.0.0.0 --port:{port}",
                "flask run", "pip install -r requirements.txt", "pip install -r requirements.txt", 5000);
        }

        // 17. FastAPI
        if (allFiles.Contains("main.py") && HasFileContaining(files, path, "fastapi"))
        {
            return MakeInfo("FastAPI", "Python", "FastAPI",
                "uvicorn main:app --host=0.0.0.0 --port={port}",
                "uvicorn main:app", "pip install -r requirements.txt", "pip install -r requirements.txt", 8000);
        }

        // 18. Generic Python
        if (allFiles.Contains("requirements.txt") || allFiles.Contains("setup.py") || HasExtension(files, ".py"))
        {
            return MakeInfo("Python", "Python", "Python",
                "python -m http.server {port}",
                "python -m http.server", "pip install -r requirements.txt",
                allFiles.Contains("requirements.txt") ? "pip install -r requirements.txt" : null, 8000);
        }

        // 19. Ruby on Rails
        if ((allFiles.Contains("gemfile") || allFiles.Contains("gemfile.lock")) && FileContains(files, path, "rails"))
        {
            return MakeInfo("Ruby on Rails", "Ruby", "Rails",
                "rails server -p {port} -b 0.0.0.0",
                "rails server", "bundle install", "bundle install", 3000);
        }

        // 20. Jekyll
        if (allFiles.Contains("_config.yml"))
        {
            return MakeInfo("Jekyll", "Ruby", "Jekyll",
                "jekyll serve --port {port}",
                "jekyll serve", "bundle install", "bundle install", 4000);
        }

        // 21. Go
        if (allFiles.Contains("go.mod"))
        {
            return MakeInfo("Go", "Go", "Go",
                "go run .", "go run .", null, null, 8080);
        }

        // 22. .NET Core / .NET 5+
        var csprojFiles = files.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (csprojFiles.Length > 0)
        {
            return MakeInfo(".NET - " + Path.GetFileNameWithoutExtension(csprojFiles[0]), "C#", ".NET",
                "dotnet run --urls http://0.0.0.0:{port}",
                "dotnet run", "dotnet restore", "dotnet restore", 5000);
        }

        // 23. Static HTML
        if (allFiles.Contains("index.html") || allFiles.Contains("index.htm"))
        {
            return MakeInfo("Static HTML", "HTML/CSS/JS", "Static",
                "npx live-server --port={port} --no-browser",
                "live-server", null, null, 5500);
        }

        // 24. Fallback - check any HTML files
        if (HasExtension(files, ".html") || HasExtension(files, ".htm"))
        {
            return MakeInfo("Static HTML", "HTML/CSS/JS", "Static",
                "npx live-server --port={port} --no-browser",
                "live-server", null, null, 5500);
        }

        return null;
    }

    static ProjectInfo MakeInfo(string typeName, string language, string framework,
        string runCmd, string displayCmd, string installCmd, string installLabel, int defaultPort)
    {
        return new ProjectInfo
        {
            TypeName = typeName,
            Language = language,
            Framework = framework,
            RunCommand = runCmd,
            DisplayCommand = displayCmd,
            InstallCommand = installCmd,
            InstallLabel = installLabel,
            DefaultPort = defaultPort,
            AvailableScripts = new string[0]
        };
    }

    static string[] GetNpmScripts(string path)
    {
        var pkgPath = Path.Combine(path, "package.json");
        if (!File.Exists(pkgPath)) return new string[0];

        try
        {
            var json = File.ReadAllText(pkgPath, Encoding.UTF8);
            var block = ExtractJsonBlock(json, "scripts");
            if (block == null) return new string[0];

            var scripts = new List<string>();
            foreach (Match m in Regex.Matches(block, @"""([^""]+)""\s*:"))
            {
                var name = m.Groups[1].Value;
                if (!string.IsNullOrEmpty(name)) scripts.Add(name);
            }
            return scripts.ToArray();
        }
        catch { return new string[0]; }
    }

    static Dictionary<string, string> TryReadPackageJson(string path)
    {
        var pkgPath = Path.Combine(path, "package.json");
        if (!File.Exists(pkgPath)) return null;

        try
        {
            var json = File.ReadAllText(pkgPath, Encoding.UTF8);
            var result = new Dictionary<string, string>();

            var main = ExtractJsonString(json, "main");
            if (main != null) result["main"] = main;

            var deps = ExtractJsonBlock(json, "dependencies");
            if (deps != null) result["dependencies"] = deps;

            var devDeps = ExtractJsonBlock(json, "devDependencies");
            if (devDeps != null) result["devDependencies"] = devDeps;

            return result;
        }
        catch { return null; }
    }

    static string ExtractJsonString(string json, string key)
    {
        var m = Regex.Match(json, @"""" + key + @"""\s*:\s*""([^""]*)""");
        return m.Success ? m.Groups[1].Value : null;
    }

    static string ExtractJsonBlock(string json, string key)
    {
        var match = Regex.Match(json, @"""" + key + @"""\s*:\s*(\{)");
        if (!match.Success) return null;
        int start = match.Groups[1].Index;
        int depth = 0;
        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
        }
        return null;
    }

    static bool HasExtension(string[] files, params string[] exts)
    {
        return files.Any(f => exts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
    }

    static bool HasFileContaining(string[] filePaths, string dir, string keyword)
    {
        try
        {
            return filePaths.Any(f =>
            {
                if (f.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var content = File.ReadAllText(Path.Combine(dir, f));
                        return content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    catch { return false; }
                }
                return false;
            });
        }
        catch { return false; }
    }

    static bool FileContains(string[] filePaths, string dir, string keyword)
    {
        try
        {
            foreach (var f in filePaths)
            {
                var lower = f.ToLowerInvariant();
                if (lower == "gemfile" || lower == "gemfile.lock")
                {
                    try
                    {
                        var content = File.ReadAllText(Path.Combine(dir, f));
                        if (content.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    catch { }
                }
            }
        }
        catch { }
        return false;
    }

    public static bool IsToolInstalled(string tool)
    {
        try
        {
            var psi = new ProcessStartInfo("where", tool)
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            proc.WaitForExit(2000);
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }
}

// ── Main form ────────────────────────────────────────────────────────────────
class MainForm : Form
{
    static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UniversalLiveServer");
    static readonly string ConfigFile = Path.Combine(ConfigDir, "settings.txt");

    ToolTip tooltip;
    TextBox pathBox;
    NumericUpDown portBox;
    Button browseBtn, startBtn, stopBtn, openBtn, detectBtn, installBtn;
    RichTextBox logBox;
    Label statusLabel;
    Label typeLabel, langLabel, cmdLabel;
    ComboBox scriptCombo;
    Label scriptLabel;
    Process serverProcess;
    bool isRunning;
    bool pendingStart;
    int runningPort;
    ProjectInfo currentProject;
    bool detectionScheduled;
    LinkLabel updateLabel;

    public MainForm(string initialPath = null)
    {
        Text = "Universal Live Server";
        Size = new Size(780, 610);
        MinimumSize = new Size(780, 610);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9);
        AllowDrop = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        tooltip = new ToolTip();
        int y = 12;

        // Title
        var titleBar = new Panel
        {
            Location = new Point(15, y),
            Size = new Size(743, 40),
            BackColor = Color.FromArgb(45, 45, 45)
        };

        var title = new Label
        {
            Text = "Universal Live Server",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Size = new Size(330, 40),
            Location = new Point(15, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(100, 200, 255),
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(title);

        var verBadge = new Label
        {
            Text = "v" + AppVersion.Current,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(55, 22),
            Location = new Point(350, 9),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(150, 150, 150),
            BackColor = Color.FromArgb(55, 55, 55)
        };
        titleBar.Controls.Add(verBadge);

        updateLabel = new LinkLabel
        {
            Text = "",
            Location = new Point(660, 8),
            Size = new Size(75, 25),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ActiveLinkColor = Color.Cyan,
            LinkColor = Color.FromArgb(255, 200, 50),
            VisitedLinkColor = Color.FromArgb(255, 200, 50),
            Visible = false,
            BackColor = Color.Transparent
        };
        updateLabel.Click += (s, e) => Process.Start(AppVersion.ReleasesUrl);
        titleBar.Controls.Add(updateLabel);

        Controls.Add(titleBar);
        y += 52;

        // ── Project path row ──────────────────────────────────────────────────
        Controls.Add(new Label
        {
            Text = "Project Path:",
            Location = new Point(15, y + 3),
            Size = new Size(90, 22),
            ForeColor = Color.LightGray
        });

        pathBox = new TextBox
        {
            Location = new Point(105, y),
            Size = new Size(475, 22),
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        if (!string.IsNullOrEmpty(initialPath))
        {
            pathBox.Text = initialPath;
        }
        else
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var saved = File.ReadAllText(ConfigFile, Encoding.UTF8).Trim();
                    if (Directory.Exists(saved)) pathBox.Text = saved;
                }
            }
            catch { }
        }
        pathBox.TextChanged += (s, e) => ScheduleDetection();
        pathBox.Leave += (s, e) => { pathBox.Text = pathBox.Text.Trim().Trim('"'); };
        pathBox.GotFocus += (s, e) => { pathBox.SelectAll(); };
        Controls.Add(pathBox);

        browseBtn = new Button
        {
            Text = "Browse...",
            Location = new Point(585, y - 1),
            Size = new Size(80, 25),
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        browseBtn.Click += (s, e) =>
        {
            var path = FolderBrowser.Show(pathBox.Text.Trim().Trim('"'), this);
            if (path != null) pathBox.Text = path;
        };
        Controls.Add(browseBtn);

        detectBtn = new Button
        {
            Text = "Detect",
            Location = new Point(670, y - 1),
            Size = new Size(58, 25),
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat
        };
        detectBtn.Click += (s, e) => RunDetection();
        Controls.Add(detectBtn);

        var clearLogBtn = new Button
        {
            Text = "Clear",
            Location = new Point(733, y - 1),
            Size = new Size(48, 25),
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat
        };
        clearLogBtn.Click += (s, e) => { logBox.Clear(); };
        Controls.Add(clearLogBtn);

        y += 32;

        // ── Detection result panel ────────────────────────────────────────────
        var panel = new Panel
        {
            Location = new Point(15, y),
            Size = new Size(743, 80),
            BackColor = Color.FromArgb(38, 38, 38),
            BorderStyle = BorderStyle.FixedSingle
        };

        typeLabel = new Label
        {
            Text = "Project: --",
            Location = new Point(10, 8),
            Size = new Size(350, 20),
            ForeColor = Color.FromArgb(100, 200, 255),
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        panel.Controls.Add(typeLabel);

        langLabel = new Label
        {
            Text = "",
            Location = new Point(10, 30),
            Size = new Size(350, 18),
            ForeColor = Color.LightGray
        };
        panel.Controls.Add(langLabel);

        cmdLabel = new Label
        {
            Text = "",
            Location = new Point(10, 50),
            Size = new Size(350, 18),
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Consolas", 8.5f)
        };
        panel.Controls.Add(cmdLabel);

        // Script selector
        scriptLabel = new Label
        {
            Text = "Script:",
            Location = new Point(380, 10),
            Size = new Size(45, 22),
            ForeColor = Color.LightGray,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(scriptLabel);

        scriptCombo = new ComboBox
        {
            Location = new Point(425, 9),
            Size = new Size(200, 22),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Visible = false
        };
        scriptCombo.SelectedIndexChanged += (s, e) => UpdateScriptCommand();
        panel.Controls.Add(scriptCombo);

        installBtn = new Button
        {
            Text = "Install Deps",
            Location = new Point(425, 36),
            Size = new Size(120, 25),
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Visible = false
        };
        installBtn.Click += RunInstall;
        panel.Controls.Add(installBtn);

        Controls.Add(panel);
        y += 90;

        // ── Port & controls row ───────────────────────────────────────────────
        Controls.Add(new Label
        {
            Text = "Port:",
            Location = new Point(15, y + 3),
            Size = new Size(40, 22),
            ForeColor = Color.LightGray
        });

        portBox = new NumericUpDown
        {
            Location = new Point(55, y),
            Size = new Size(80, 22),
            Minimum = 1,
            Maximum = 65535,
            Value = 5500,
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(portBox);

        startBtn = new Button
        {
            Text = "Start Server",
            Location = new Point(155, y - 1),
            Size = new Size(130, 28),
            BackColor = Color.FromArgb(46, 125, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        startBtn.Click += StartServer;
        Controls.Add(startBtn);

        stopBtn = new Button
        {
            Text = "Stop Server",
            Location = new Point(295, y - 1),
            Size = new Size(130, 28),
            BackColor = Color.FromArgb(198, 40, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        stopBtn.Click += StopServer;
        Controls.Add(stopBtn);

        openBtn = new Button
        {
            Text = "Open Browser",
            Location = new Point(435, y - 1),
            Size = new Size(130, 28),
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        openBtn.Click += (s, e) => Process.Start("http://localhost:" + portBox.Value);
        Controls.Add(openBtn);

        y += 38;

        // ── Separator ─────────────────────────────────────────────────────────
        Controls.Add(new Label
        {
            Location = new Point(15, y),
            Size = new Size(743, 1),
            BackColor = Color.FromArgb(70, 70, 70)
        });
        y += 10;

        // ── Status ────────────────────────────────────────────────────────────
        statusLabel = new Label
        {
            Text = "Status: Idle",
            Location = new Point(15, y),
            Size = new Size(743, 22),
            ForeColor = Color.Gray
        };
        Controls.Add(statusLabel);
        y += 28;

        // ── Log ───────────────────────────────────────────────────────────────
        logBox = new RichTextBox
        {
            Location = new Point(15, y),
            Size = new Size(743, 300),
            ReadOnly = true,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Consolas", 9.5f),
            BorderStyle = BorderStyle.None,
            DetectUrls = true
        };
        logBox.LinkClicked += (s, e) => Process.Start(e.LinkText);
        Controls.Add(logBox);
        y += 305;

        // ── Footer ────────────────────────────────────────────────────────────
        var footerPanel = new Panel
        {
            Location = new Point(15, y),
            Size = new Size(743, 40),
            BackColor = Color.FromArgb(35, 35, 35)
        };

        var versionLabel = new Label
        {
            Text = "v" + AppVersion.Current,
            Location = new Point(10, 10),
            Size = new Size(50, 20),
            ForeColor = Color.FromArgb(100, 100, 100),
            Font = new Font("Segoe UI", 8),
            TextAlign = ContentAlignment.MiddleLeft
        };
        footerPanel.Controls.Add(versionLabel);

        var creditLabel = new Label
        {
            Text = "Made by ",
            Location = new Point(260, 8),
            Size = new Size(55, 22),
            ForeColor = Color.FromArgb(130, 130, 130),
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleRight
        };
        footerPanel.Controls.Add(creditLabel);

        var nameLink = new LinkLabel
        {
            Text = "Moaaz Besher",
            Location = new Point(315, 6),
            Size = new Size(100, 25),
            ActiveLinkColor = Color.Cyan,
            LinkColor = Color.FromArgb(100, 200, 255),
            VisitedLinkColor = Color.FromArgb(100, 200, 255),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            LinkBehavior = LinkBehavior.HoverUnderline,
            TextAlign = ContentAlignment.MiddleLeft
        };
        nameLink.Links.Add(0, nameLink.Text.Length, "https://moaazbesher.vercel.app/");
        nameLink.LinkClicked += (s, e) => Process.Start(e.Link.LinkData as string);
        footerPanel.Controls.Add(nameLink);

        var aboutBtn = new Button
        {
            Text = "About",
            Location = new Point(660, 7),
            Size = new Size(75, 25),
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.LightGray,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        aboutBtn.Click += (s, e) =>
        {
            MessageBox.Show(
                "Universal Live Server v" + AppVersion.Current + "\n\n" +
                "Auto-detect & run any web project with one click.\n\n" +
                "Created & Developed by Moaaz Besher\n" +
                "Portfolio: moaazbesher.vercel.app\n\n" +
                "GitHub: github.com/MoaazBesher/liveserver",
                "About Universal Live Server",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        footerPanel.Controls.Add(aboutBtn);

        Controls.Add(footerPanel);

        // ── Drag & Drop ───────────────────────────────────────────────────────
        DragEnter += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                pathBox.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                RunDetection();
            }
        };

        // Initial detection after form loads
        Shown += (s, e) =>
        {
            RunDetection();
            System.Threading.ThreadPool.QueueUserWorkItem(_ => CheckForUpdates());
        };

        // Tooltips
        tooltip.SetToolTip(browseBtn, "Browse for project folder");
        tooltip.SetToolTip(detectBtn, "Re-detect project type");
        tooltip.SetToolTip(startBtn, "Start the development server");
        tooltip.SetToolTip(stopBtn, "Stop the running server");
        tooltip.SetToolTip(openBtn, "Open in default browser");
        tooltip.SetToolTip(installBtn, "Install project dependencies");
        tooltip.SetToolTip(portBox, "Server port number (1-65535)");
        tooltip.SetToolTip(clearLogBtn, "Clear log window");

        // Save config on close
        FormClosing += (s, e) =>
        {
            if (isRunning) StopServer(null, null);
            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                File.WriteAllText(ConfigFile, pathBox.Text.Trim().Trim('"'), Encoding.UTF8);
            }
            catch { }
        };
    }

    // ── Auto-update checker ───────────────────────────────────────────────────
    void CheckForUpdates()
    {
        try
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "UniversalLiveServer/" + AppVersion.Current);
                var json = wc.DownloadString(AppVersion.RawVersionUrl);

                var match = Regex.Match(json, @"""version""\s*:\s*""([^""]+)""");
                if (!match.Success) return;

                var remoteVer = match.Groups[1].Value;
                if (CompareVersions(remoteVer, AppVersion.Current) <= 0) return;

                var dlMatch = Regex.Match(json, @"""downloadUrl""\s*:\s*""([^""]+)""");
                var dlUrl = dlMatch.Success ? dlMatch.Groups[1].Value : AppVersion.ReleasesUrl;

                var changelog = "";
                var clMatch = Regex.Match(json, @"""changelog""\s*:\s*""((?:[^""\\]|\\.)*)""");
                if (clMatch.Success)
                    changelog = clMatch.Groups[1].Value.Replace("\\n", "\n");

                BeginInvoke((MethodInvoker)delegate
                {
                    updateLabel.Text = "v" + remoteVer + " \u2191";  // arrow up
                    updateLabel.Visible = true;
                    tooltip.SetToolTip(updateLabel,
                        "Update v" + remoteVer + " available!\nCurrent: v" + AppVersion.Current +
                        (string.IsNullOrEmpty(changelog) ? "" : "\n\n" + changelog) +
                        "\n\nClick to download");
                    AddLog("Update available: v" + remoteVer + "  (current: v" + AppVersion.Current + ")", "Orange");
                    AddLog("Download: " + dlUrl, "Cyan");
                });
            }
        }
        catch { }
    }

    int CompareVersions(string a, string b)
    {
        try
        {
            var va = a.Split('.').Select(int.Parse).ToArray();
            var vb = b.Split('.').Select(int.Parse).ToArray();
            for (int i = 0; i < Math.Max(va.Length, vb.Length); i++)
            {
                int na = i < va.Length ? va[i] : 0;
                int nb = i < vb.Length ? vb[i] : 0;
                if (na != nb) return na.CompareTo(nb);
            }
            return 0;
        }
        catch { return 0; }
    }

    void ScheduleDetection()
    {
        if (detectionScheduled) return;
        detectionScheduled = true;
        var t = new System.Threading.Timer(_ =>
        {
            detectionScheduled = false;
            try { Invoke((MethodInvoker)RunDetection); }
            catch { }
        }, null, 600, System.Threading.Timeout.Infinite);
    }

    void RunDetection()
    {
        var path = pathBox.Text.Trim().Trim('"').Trim();
        if (!Directory.Exists(path))
        {
            typeLabel.Text = string.IsNullOrEmpty(path)
                ? "Drop a project folder here \u2193"
                : "Project: -- (path not found)";
            typeLabel.ForeColor = Color.FromArgb(255, 150, 150);
            langLabel.Text = "";
            cmdLabel.Text = "";
            scriptCombo.Visible = false;
            scriptLabel.Visible = false;
            installBtn.Visible = false;
            currentProject = null;
            return;
        }

        currentProject = ProjectDetector.Detect(path);
        if (currentProject == null)
        {
            typeLabel.Text = "Project: Unknown (no recognized project type)";
            typeLabel.ForeColor = Color.Orange;
            langLabel.Text = "Tip: Drop an HTML/PHP/Python/Node.js project folder";
            cmdLabel.Text = "";
            scriptCombo.Visible = false;
            scriptLabel.Visible = false;
            installBtn.Visible = false;
            return;
        }

        typeLabel.Text = "Project: " + currentProject.TypeName;
        typeLabel.ForeColor = Color.FromArgb(100, 200, 255);

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(currentProject.Language)) parts.Add(currentProject.Language);
        if (!string.IsNullOrEmpty(currentProject.Framework) && currentProject.Framework != currentProject.Language)
            parts.Add(currentProject.Framework);
        langLabel.Text = string.Join("  |  ", parts);

        cmdLabel.Text = "Run: " + currentProject.DisplayCommand;

        // Script selector for Node.js projects
        if (currentProject.AvailableScripts != null && currentProject.AvailableScripts.Length > 0)
        {
            scriptCombo.Visible = true;
            scriptLabel.Visible = true;
            scriptCombo.Items.Clear();
            scriptCombo.Items.AddRange(currentProject.AvailableScripts);
            var preferred = new[] { "dev", "start", "serve", "watch", "develop" };
            int idx = -1;
            foreach (var p in preferred)
            {
                idx = Array.FindIndex(currentProject.AvailableScripts,
                    s => s.Equals(p, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) break;
            }
            scriptCombo.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            scriptCombo.Visible = false;
            scriptLabel.Visible = false;
        }

        installBtn.Visible = !string.IsNullOrEmpty(currentProject.InstallCommand);
        portBox.Value = currentProject.DefaultPort;
    }

    void UpdateScriptCommand()
    {
        if (currentProject == null || scriptCombo.SelectedItem == null) return;
        currentProject.SelectedScript = scriptCombo.SelectedItem.ToString();
        var runCmd = "npm run " + currentProject.SelectedScript;
        currentProject.RunCommand = runCmd;
        currentProject.DisplayCommand = runCmd;
        cmdLabel.Text = "Run: " + runCmd;
    }

    void RunInstall(object sender, EventArgs e)
    {
        if (currentProject == null || string.IsNullOrEmpty(currentProject.InstallCommand)) return;

        var path = pathBox.Text.Trim().Trim('"').Trim();
        AddLog("Installing dependencies...", "Cyan");
        AddLog("> " + currentProject.InstallCommand, "Cyan");
        SetStatus("Installing dependencies...", "Orange");

        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + currentProject.InstallCommand)
            {
                WorkingDirectory = path,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var proc = Process.Start(psi);
            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit(60000);

            if (!string.IsNullOrEmpty(output))
                foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    AddLog(line, "Gray");
            if (!string.IsNullOrEmpty(error))
                foreach (var line in error.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    AddLog(line, "Orange");

            if (proc.ExitCode == 0)
            {
                AddLog("Dependencies installed successfully!", "Green");
                SetStatus("Dependencies installed", "Green");
            }
            else
            {
                AddLog("Installation exited with code " + proc.ExitCode, "Red");
                SetStatus("Installation failed", "Red");
            }
        }
        catch (Exception ex)
        {
            AddLog("Install error: " + ex.Message, "Red");
            SetStatus("Install error", "Red");
        }
    }

    // ── Port & process management ────────────────────────────────────────────

    int[] GetPidsForPort(int port)
    {
        var pids = new List<int>();
        try
        {
            var psi = new ProcessStartInfo("netstat", "-ano")
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2000);

            var pattern = port + "\\s+[^\\s]+\\s+LISTENING\\s+(\\d+)";
            var matches = Regex.Matches(output, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                int pid = int.Parse(m.Groups[1].Value);
                if (pid > 0 && !pids.Contains(pid)) pids.Add(pid);
            }
        }
        catch (Exception ex) { AddLog("Port check error: " + ex.Message, "Red"); }
        return pids.ToArray();
    }

    void KillProcessOnPort(int port)
    {
        var pids = GetPidsForPort(port);
        foreach (var pid in pids)
        {
            try
            {
                var kill = Process.Start(new ProcessStartInfo("taskkill", "/F /T /PID " + pid)
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                kill.WaitForExit(3000);
                AddLog("Killed process PID " + pid + " on port " + port, "Orange");
            }
            catch (Exception ex)
            {
                AddLog("Failed to kill PID " + pid + ": " + ex.Message, "Red");
            }
        }
    }

    // ── Server control ───────────────────────────────────────────────────────

    void AddLog(string text, string colorName = "White")
    {
        var color = Color.FromName(colorName);
        logBox.SelectionStart = logBox.TextLength;
        logBox.SelectionLength = 0;
        logBox.SelectionColor = color;
        logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + "\r\n");
        logBox.ScrollToCaret();
    }

    void SetStatus(string text, string colorName = "Gray")
    {
        statusLabel.Text = "Status: " + text;
        statusLabel.ForeColor = Color.FromName(colorName);
    }

    void CheckToolAvailability(ProjectInfo project)
    {
        if (project.Language == "Node.js")
        {
            if (!ProjectDetector.IsToolInstalled("node"))
                AddLog("Node.js not found! Install from https://nodejs.org", "Red");
        }
        else if (project.Language == "PHP")
        {
            if (!ProjectDetector.IsToolInstalled("php"))
                AddLog("PHP not found! Install from https://php.net", "Red");
        }
        else if (project.Language == "Python")
        {
            if (!ProjectDetector.IsToolInstalled("python"))
                AddLog("Python not found! Install from https://python.org", "Red");
        }
        else if (project.Language == "Ruby")
        {
            if (!ProjectDetector.IsToolInstalled("ruby"))
                AddLog("Ruby not found! Install from https://ruby-lang.org", "Red");
        }
        else if (project.Language == "Go")
        {
            if (!ProjectDetector.IsToolInstalled("go"))
                AddLog("Go not found! Install from https://go.dev", "Red");
        }
        else if (project.Language == "C#" || project.Language == ".NET")
        {
            if (!ProjectDetector.IsToolInstalled("dotnet"))
                AddLog(".NET SDK not found! Install from https://dotnet.microsoft.com", "Red");
        }
    }

    void SetControlsEnabled(bool enabled)
    {
        startBtn.Enabled = enabled && !isRunning;
        stopBtn.Enabled = isRunning || (!enabled && serverProcess != null);
        openBtn.Enabled = isRunning;
        pathBox.Enabled = enabled && !isRunning;
        browseBtn.Enabled = enabled && !isRunning;
        detectBtn.Enabled = enabled && !isRunning;
        portBox.Enabled = enabled && !isRunning;
        scriptCombo.Enabled = enabled && !isRunning;
        installBtn.Enabled = enabled && !isRunning;
    }

    void CompleteStart()
    {
        if (serverProcess == null) return;

        int port = runningPort;

        if (!serverProcess.HasExited)
        {
            isRunning = true;
            pendingStart = false;
            SetStatus("Running on port " + port, "Green");
            AddLog("Server started successfully!", "Green");
            AddLog("Local:   http://localhost:" + port, "Cyan");

            var localIP = "";
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localIP = ip.ToString();
                        break;
                    }
                }
            }
            catch { }
            if (!string.IsNullOrEmpty(localIP))
                AddLog("Network: http://" + localIP + ":" + port + "  (share this with others on your network)", "Cyan");

            SetControlsEnabled(true);
            Thread.Sleep(300);
            Process.Start("http://localhost:" + port);
        }
        else
        {
            pendingStart = false;
            var err = serverProcess.StandardError.ReadToEnd();
            var outText = serverProcess.StandardOutput.ReadToEnd();
            if (!string.IsNullOrEmpty(err))
                AddLog("Server error: " + err.Trim(), "Red");
            if (!string.IsNullOrEmpty(outText))
                AddLog("Server output: " + outText.Trim(), "Orange");
            AddLog("Failed to start. Check if required tools are installed.", "Red");
            CheckToolAvailability(currentProject);
            SetStatus("Failed to start", "Red");
            SetControlsEnabled(true);
        }
    }

    void StartServer(object sender, EventArgs e)
    {
        var path = pathBox.Text.Trim().Trim('"').Trim();
        var port = (int)portBox.Value;

        if (!Directory.Exists(path))
        {
            MessageBox.Show("Project path does not exist!", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (isRunning || pendingStart)
        {
            AddLog("Server is already running or starting...", "Orange");
            return;
        }

        if (currentProject == null)
        {
            currentProject = ProjectDetector.Detect(path);
            if (currentProject == null)
            {
                MessageBox.Show("Could not detect project type.\n" +
                    "Make sure your project has recognizable files (package.json, index.html, etc.).",
                    "Unknown Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        var rawCmd = currentProject.RunCommand;
        if (currentProject.AvailableScripts != null && currentProject.AvailableScripts.Length > 0 &&
            scriptCombo.SelectedItem != null)
        {
            rawCmd = "npm run " + scriptCombo.SelectedItem.ToString();
        }

        var finalCmd = rawCmd.Replace("{port}", port.ToString())
                             .Replace("{path}", path);

        AddLog("Starting server on port " + port + "...", "Cyan");
        AddLog("Type: " + currentProject.TypeName, "Cyan");
        AddLog("Command: " + finalCmd, "Cyan");
        AddLog("Path: " + path, "Cyan");
        SetStatus("Starting...", "Orange");

        var pids = GetPidsForPort(port);
        if (pids.Length > 0)
        {
            var msg = "Port " + port + " is already in use by:\n";
            foreach (var pid in pids)
            {
                try { msg += "  - " + Process.GetProcessById(pid).ProcessName + " (PID: " + pid + ")\n"; }
                catch { msg += "  - PID: " + pid + "\n"; }
            }
            msg += "\nKill it and continue?";
            var result = MessageBox.Show(msg, "Port In Use", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                KillProcessOnPort(port);
            }
            else
            {
                AddLog("Cancelled - port " + port + " is in use", "Red");
                SetStatus("Cancelled", "Red");
                return;
            }
        }

        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + finalCmd)
            {
                WorkingDirectory = path,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            serverProcess = Process.Start(psi);
            runningPort = port;
            pendingStart = true;
            SetControlsEnabled(false);

            var checkTimer = new System.Windows.Forms.Timer();
            checkTimer.Interval = 2500;
            checkTimer.Tick += (s, args) =>
            {
                checkTimer.Stop();
                checkTimer.Dispose();
                CompleteStart();
            };
            checkTimer.Start();
        }
        catch (Exception ex)
        {
            pendingStart = false;
            AddLog("Error: " + ex.Message, "Red");
            SetStatus("Error", "Red");
            SetControlsEnabled(true);
        }
    }

    void StopServer(object sender, EventArgs e)
    {
        if (!isRunning && !pendingStart) return;

        AddLog("Stopping server...", "Orange");
        SetStatus("Stopping...", "Orange");

        try
        {
            if (serverProcess != null && !serverProcess.HasExited)
            {
                int pid = serverProcess.Id;
                var kill = Process.Start(new ProcessStartInfo("taskkill", "/F /T /PID " + pid)
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                if (kill != null) kill.WaitForExit(3000);
                serverProcess.WaitForExit(2000);
            }
        }
        catch { }

        serverProcess = null;
        isRunning = false;
        pendingStart = false;
        runningPort = 0;

        SetControlsEnabled(true);
        SetStatus("Stopped", "Gray");
        AddLog("Server stopped", "Red");
    }
}
