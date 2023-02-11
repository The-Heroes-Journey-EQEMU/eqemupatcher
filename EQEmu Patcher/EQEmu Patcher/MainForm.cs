using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
namespace EQEmu_Patcher
{
    
    public partial class MainForm : Form
    {

        public static string serverName;
        public static string filelistUrl;
        public static string patcherUrl;
        public static bool defaultAutoPlay = false; //When a user runs this first time, what should Autoplay be set to?
        public static bool defaultAutoPatch = false; //When a user runs this first time, what should Autopatch be set to?

        //Note that for supported versions, the 3 letter suffix is needed on the filelist_###.yml file.
        public static List<VersionTypes> supportedClients = new List<VersionTypes> { //Supported clients for patcher
            //VersionTypes.Unknown, //unk
            //VersionTypes.Titanium, //tit
            //VersionTypes.Underfoot, //und
            //VersionTypes.Secrets_Of_Feydwer, //sof
            //VersionTypes.Seeds_Of_Destruction, //sod
            VersionTypes.Rain_Of_Fear, //rof
            VersionTypes.Rain_Of_Fear_2 //rof
            //VersionTypes.Broken_Mirror, //bro
        }; 


        bool isLoading;
        bool isNeedingPatch;
        private Dictionary<VersionTypes, ClientVersion> clientVersions = new Dictionary<VersionTypes, ClientVersion>();

        VersionTypes currentVersion;

       // TaskbarItemInfo tii = new TaskbarItemInfo();
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Console.WriteLine("Initializing");
            serverName = Assembly.GetExecutingAssembly().GetCustomAttribute<ServerName>().Value;
            if (serverName == "") {
                MessageBox.Show("This patcher was built incorrectly. Please contact the distributor of this and inform them the server name is not provided or screenshot this message.");
                this.Close();
                return;
            }
            if (!serverName.EndsWith("/")) serverName += "/";
            
            filelistUrl = Assembly.GetExecutingAssembly().GetCustomAttribute<FileListUrl>().Value;            
            if (filelistUrl == "") {
                MessageBox.Show("This patcher was built incorrectly. Please contact the distributor of this and inform them the file list url is not provided or screenshot this message.", serverName);
                this.Close();
                return;
            }
            if (!filelistUrl.EndsWith("/")) filelistUrl += "/";

            var currentDirectory = new DirectoryInfo(Application.StartupPath);
            if (currentDirectory.Parent != null)
            {
                
            }

            patcherUrl = Assembly.GetExecutingAssembly().GetCustomAttribute<PatcherUrl>().Value;
            if (patcherUrl == "")
            {
                MessageBox.Show("This patcher was built incorrectly. Please contact the distributor of this and inform them the patcher url is not provided or screenshot this message.", serverName);
                this.Close();
                return;
            }
            Console.WriteLine("Start patch");
            Console.WriteLine("End Patch");
            if (defaultAutoPlay || defaultAutoPatch)
            {
                Console.WriteLine("Auto default enabled");
            }

            isLoading = true;
            txtList.Visible = false;
            splashLogo.Visible = true;
            if (this.Width < 432) {
                this.Width = 432;
            }
            if (this.Height < 550)
            {
                this.Height = 550;
            }
            buildClientVersions();
            IniLibrary.Load();
            detectClientVersion();
            
            if (IniLibrary.instance.ClientVersion == VersionTypes.Unknown)
            {
                detectClientVersion();
                if (currentVersion == VersionTypes.Unknown)
                {
                    this.Close();
                }
                IniLibrary.instance.ClientVersion = currentVersion;
                IniLibrary.Save();
            }
            string suffix = "unk";
            if (currentVersion == VersionTypes.Titanium) suffix = "tit";
            if (currentVersion == VersionTypes.Underfoot) suffix = "und";
            if (currentVersion == VersionTypes.Seeds_Of_Destruction) suffix = "sod";
            if (currentVersion == VersionTypes.Broken_Mirror) suffix = "bro";
            if (currentVersion == VersionTypes.Secrets_Of_Feydwer) suffix = "sof";
            if (currentVersion == VersionTypes.Rain_Of_Fear || currentVersion == VersionTypes.Rain_Of_Fear_2) suffix = "rof";

            bool isSupported = false;
            foreach (var ver in supportedClients)
            {
                if (ver != currentVersion) continue;                
                isSupported = true;
                break;
            }
            if (!isSupported) {
                MessageBox.Show("The server " + serverName + " does not work with this copy of Everquest (" + currentVersion.ToString().Replace("_", " ") + ")", serverName);
                this.Close();
                return;
            }

            this.Text = serverName + " (Client: " + currentVersion.ToString().Replace("_", " ") + ")";

            StatusLibrary.SubscribeProgress(new StatusLibrary.ProgressHandler((int value) => {
                Invoke((MethodInvoker)delegate {
                    progressBar.Value = value;
                });
            }));

            StatusLibrary.SubscribeLogAdd(new StatusLibrary.LogAddHandler((string message) => {
                Invoke((MethodInvoker)delegate {
                    if (!txtList.Visible)
                    {
                        txtList.Visible = true;
                        splashLogo.Visible = false;
                    }
                    txtList.AppendText(message + "\r\n");
                });
            }));

            string webUrl = $"{filelistUrl}{suffix}/filelist_{suffix}.yml";
            string response = DownloadFile(webUrl, "filelist.yml");
            if (response != "")
            {
                webUrl = $"{filelistUrl}/filelist_{ suffix}.yml";
                response = DownloadFile(webUrl, "filelist.yml");
                if (response != "")
                {
                    MessageBox.Show("Failed to fetch filelist from " + webUrl + ": " + response);
                    this.Close();
                    return;
                }
            }

            txtList.Visible = false;
            splashLogo.Visible = true;
            FileList filelist;            

            using (var input = File.OpenText("filelist.yml"))
            {
                var deserializerBuilder = new DeserializerBuilder().WithNamingConvention(new CamelCaseNamingConvention());

                var deserializer = deserializerBuilder.Build();

                filelist = deserializer.Deserialize<FileList>(input);
            }
            
            if (filelist.version != IniLibrary.instance.LastPatchedVersion)
            {
                isNeedingPatch = true;
               btnCheck.BackColor = Color.Red;
            } else
            {                
                if ( IniLibrary.instance.AutoPlay.ToLower() == "true") PlayGame();
            }
            chkAutoPlay.Checked = (IniLibrary.instance.AutoPlay == "true");
            chkAutoPatch.Checked = (IniLibrary.instance.AutoPatch == "true");
            isLoading = false;
            if (File.Exists("eqemupatcher.png"))
            {
                splashLogo.Load("eqemupatcher.png");
            }
        }

        System.Diagnostics.Process process;
      

        System.Collections.Specialized.StringCollection log = new System.Collections.Specialized.StringCollection();

        Dictionary<string, string> WalkDirectoryTree(System.IO.DirectoryInfo root)
        {
            var fileMap = new Dictionary<string, string>();
            FileInfo[] files;
            try
            {
                files = root.GetFiles("*.*");
            }
            // This is thrown if even one of the files requires permissions greater
            // than the application provides.
            catch (UnauthorizedAccessException e)
            {
                StatusLibrary.Log(e.Message);
                return fileMap;
            }

            catch (System.IO.DirectoryNotFoundException e)
            {
                StatusLibrary.Log(e.Message);
                return fileMap;
            }

            if (files != null)
            {
                
                foreach (System.IO.FileInfo fi in files)
                {
                    if (fi.Name.Contains(".ini"))
                    { //Skip INI files
                        progressBar.Value++;
                        continue;
                    }
                    if (fi.Name == System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)
                    { //Skip self EXE
                        progressBar.Value++;
                        continue;
                    }

                    // In this example, we only access the existing FileInfo object. If we
                    // want to open, delete or modify the file, then
                    // a try-catch block is required here to handle the case
                    // where the file has been deleted since the call to TraverseTree().
                    var md5 = UtilityLibrary.GetMD5(fi.FullName);
                    StatusLibrary.Log($"{fi.Name}: {md5}\r\n");
                    if (progressBar.Maximum > progressBar.Value) {
                        progressBar.Value++;
                    }
                    fileMap[fi.Name] = md5;
                    txtList.Refresh();
                    updateTaskbarProgress();
                    Application.DoEvents();
                    
                }
                //One final update of data
                if (progressBar.Maximum > progressBar.Value)
                {
                    progressBar.Value++;
                }
                txtList.Refresh();
                updateTaskbarProgress();
                Application.DoEvents();
            }
            return fileMap;
        }
        

        private void detectClientVersion()
        {

            try
            {

                var hash = UtilityLibrary.GetEverquestExecutableHash(AppDomain.CurrentDomain.BaseDirectory);
                if (hash == "")
                {
                    MessageBox.Show("Please run this patcher in your Everquest directory.");
                    this.Close();
                    return;
                }
                switch (hash)
                {
                    case "85218FC053D8B367F2B704BAC5E30ACC":
                        currentVersion = VersionTypes.Secrets_Of_Feydwer;
                        splashLogo.Image = Properties.Resources.sof;
                        break;
                    case "859E89987AA636D36B1007F11C2CD6E0":
                    case "EF07EE6649C9A2BA2EFFC3F346388E1E78B44B48": //one of the torrented uf clients, used by B&R too
                        currentVersion = VersionTypes.Underfoot;
                        splashLogo.Image = Properties.Resources.underfoot;
                        break;
                    case "A9DE1B8CC5C451B32084656FCACF1103": //p99 client
                    case "BB42BC3870F59B6424A56FED3289C6D4": //vanilla titanium
                        currentVersion = VersionTypes.Titanium;
                        splashLogo.Image = Properties.Resources.titanium;
                        break;
                    case "368BB9F425C8A55030A63E606D184445":
                        currentVersion = VersionTypes.Rain_Of_Fear;
                        splashLogo.Image = Properties.Resources.rof;
                        break;
                    case "240C80800112ADA825C146D7349CE85B":
                    case "A057A23F030BAA1C4910323B131407105ACAD14D": //This is a custom ROF2 from a torrent download
                        currentVersion = VersionTypes.Rain_Of_Fear_2;
                        splashLogo.Image = Properties.Resources.rof;
                        break;
                    case "6BFAE252C1A64FE8A3E176CAEE7AAE60": //This is one of the live EQ binaries.
                    case "AD970AD6DB97E5BB21141C205CAD6E68": //2016/08/27
                    case "2FD5E6243BCC909D9FD0587A156A1165": //https://github.com/xackery/eqemupatcher/issues/20
                    case "26DC13388395A20B73E1B5A08415B0F8": //Legacy of Norrath Custom RoF2 Client https://github.com/xackery/eqemupatcher/issues/16
                    case "3B44C6CD42313CB80C323647BCB296EF": //https://github.com/xackery/eqemupatcher/issues/15
                        currentVersion = VersionTypes.Broken_Mirror;
                        splashLogo.Image = Properties.Resources.brokenmirror;
                        break;
                    default:
                        currentVersion = VersionTypes.Unknown;
                        break;
                }
                if (currentVersion == VersionTypes.Unknown)
                {
                    if (MessageBox.Show("Unable to recognize the Everquest client in this directory, open a web page to report to devs?", "Visit", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start("https://github.com/Xackery/eqemupatcher/issues/new?title=A+New+EQClient+Found&body=Hi+I+Found+A+New+Client!+Hash:+" + hash);
                    }
                    StatusLibrary.Log($"Unable to recognize the Everquest client in this directory, send to developers: {hash}");
                }
                else
                {
                    //StatusLibrary.Log($"You seem to have put me in a {clientVersions[currentVersion].FullName} client directory");
                }
                
                //MessageBox.Show(""+currentVersion);
                //StatusLibrary.Log($"If you wish to help out, press the scan button on the bottom left and wait for it to complete, then copy paste this data as an Issue on github!");                
            }
            catch (UnauthorizedAccessException err)
            {
                MessageBox.Show("You need to run this program with Administrative Privileges" + err.Message);
                return;
            }
        }

        //Build out all client version's dictionary
        private void buildClientVersions()
        {
            clientVersions.Clear();
            clientVersions.Add(VersionTypes.Titanium, new ClientVersion("Titanium", "titanium"));
            clientVersions.Add(VersionTypes.Secrets_Of_Feydwer, new ClientVersion("Secrets Of Feydwer", "sof"));
            clientVersions.Add(VersionTypes.Seeds_Of_Destruction, new ClientVersion("Seeds of Destruction", "sod"));
            clientVersions.Add(VersionTypes.Rain_Of_Fear, new ClientVersion("Rain of Fear", "rof"));
            clientVersions.Add(VersionTypes.Rain_Of_Fear_2, new ClientVersion("Rain of Fear 2", "rof2"));
            clientVersions.Add(VersionTypes.Underfoot, new ClientVersion("Underfoot", "underfoot"));
            clientVersions.Add(VersionTypes.Broken_Mirror, new ClientVersion("Broken Mirror", "brokenmirror"));
        }

        private int getFileCount(System.IO.DirectoryInfo root) {
            int count = 0;
                           
            System.IO.FileInfo[] files = null;
            try
            {
                files = root.GetFiles("*.*");
            }
            // This is thrown if even one of the files requires permissions greater
            // than the application provides.
            catch (UnauthorizedAccessException e)
            {
                StatusLibrary.Log(e.Message);
                return 0;
            }

            catch (System.IO.DirectoryNotFoundException e)
            {
                StatusLibrary.Log(e.Message);
                return 0;
            }

            if (files != null)
            {
              return files.Length;
            }
            return count;
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            progressBar.Maximum = getFileCount(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory));
            progressBar.Maximum += getFileCount(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\Resources"));
            progressBar.Maximum += getFileCount(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\sounds"));
            progressBar.Maximum += getFileCount(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\SpellEffects"));
            progressBar.Maximum += getFileCount(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\storyline"));
          //  progressBar.Maximum += getFileCount(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\uifiles"));
          //  progressBar.Maximum += getFileCount(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\atlas"));
          
            PatchVersion pv = new PatchVersion();
            pv.ClientVersion = clientVersions[currentVersion].ShortName;
            //Root
            var fileMap = WalkDirectoryTree(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory));
            pv.RootFiles = fileMap;
            //Resources
            fileMap = WalkDirectoryTree(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\Resources"));
            pv.ResourceFiles = fileMap;
            //Sounds
            fileMap = WalkDirectoryTree(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\sounds"));
            pv.SoundFiles = fileMap;
            //SpellEffects
            fileMap = WalkDirectoryTree(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\SpellEffects"));
            pv.SpellEffectFiles = fileMap;
            //Storyline
            fileMap = WalkDirectoryTree(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\storyline"));
            pv.StorylineFiles = fileMap;
           /*
            //UIFiles
            fileMap = WalkDirectoryTree(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\uifiles"));
            pv.UIFiles = fileMap;
            //Atlas
            fileMap = WalkDirectoryTree(new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory + "\\atlas"));
            pv.AtlasFiles = fileMap;
            */
            //StatusLibrary.Log(JsonConvert.SerializeObject(pv));
        }

        private void updateTaskbarProgress()
        {
            
            if (Environment.OSVersion.Version.Major < 6)
            { //Only works on 6 or greater
                return;
            }
            
            
           // tii.ProgressState = TaskbarItemProgressState.Normal;            
           // tii.ProgressValue = (double)progressBar.Value / progressBar.Maximum;            
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            PlayGame();            
        }

        private void PlayGame()
        {
            try
            {
                process = UtilityLibrary.StartEverquest();
                if (process != null) this.Close();
                else MessageBox.Show("The process failed to start");
            }
            catch (Exception err)
            {
                MessageBox.Show("An error occured while trying to start everquest: " + err.Message);
            }
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {

        }

        bool isPatching = false;

        public object Keyboard { get; private set; }

        private void btnCheck_Click(object sender, EventArgs e)
        {
            if (isPatching)
            {
                btnCheck.Text = "Patch";
                isPatching = false;
                return;
            }

            StartPatch();
        }        

        private string DownloadFile(string url, string path)
        {

            path = path.Replace("/", "\\");
            if (path.Contains("\\")) { //Make directory if needed.
                string dir = Application.StartupPath + "\\" + path.Substring(0, path.LastIndexOf("\\"));
                Directory.CreateDirectory(dir);
            }
            return UtilityLibrary.DownloadFile(url, path);
        }

        private void StartPatch()
        {
            if (isPatching) return;
            isPatching = true;
            btnCheck.Text = "Cancel";

            StatusLibrary.Log("Patching...");
            FileList filelist;

            using (var input = File.OpenText("filelist.yml"))
            {
                var deserializerBuilder = new DeserializerBuilder().WithNamingConvention(new CamelCaseNamingConvention());

                var deserializer = deserializerBuilder.Build();

                filelist = deserializer.Deserialize<FileList>(input);
            }
            int totalBytes = 0;
            List<FileEntry> filesToDownload = new List<FileEntry>();
            foreach (var entry in filelist.downloads)
            {
                Application.DoEvents();
                var path = entry.name.Replace("/", "\\");
                if (!UtilityLibrary.IsPathChild(path))
                {
                    StatusLibrary.Log("Path " + path + " might be outside of your Everquest directory. Skipping download to this location.");
                    continue;
                }
                //See if file exists.
                if (!File.Exists(path))
                {
                    //Console.WriteLine("Downloading: "+ entry.name);
                    filesToDownload.Add(entry);
                    if (entry.size < 1) totalBytes += 1;
                    else totalBytes += entry.size;
                }
                else
                {
                    var md5 = UtilityLibrary.GetMD5(path);

                    if (md5.ToUpper() != entry.md5.ToUpper())
                    {
                        Console.WriteLine(entry.name + ": " + md5 + " vs " + entry.md5);
                        filesToDownload.Add(entry);
                        if (entry.size < 1) totalBytes += 1;
                        else totalBytes += entry.size;
                    }
                }
                Application.DoEvents();
                if (!isPatching) { 
                    StatusLibrary.Log("Patching cancelled.");
                    return;
                }

            }

            if (filelist.deletes != null && filelist.deletes.Count > 0)
            {
                foreach (var entry in filelist.deletes)
                {
                    if (!UtilityLibrary.IsPathChild(entry.name))
                    {
                        StatusLibrary.Log("Path " + entry.name + " might be outside your Everquest directory. Skipping deletion of this file.");
                        continue;
                    }
                    if (File.Exists(entry.name))
                    {
                        StatusLibrary.Log("Deleting " + entry.name + "...");
                        File.Delete(entry.name);
                    }
                    Application.DoEvents();
                    if (!isPatching)
                    {
                        StatusLibrary.Log("Patching cancelled.");
                        return;
                    }
                }
            }

            if (filesToDownload.Count == 0)
            {
                string version = filelist.version;
                if (version.Length >= 8)
                {
                    version = version.Substring(0, 8);
                }
                StatusLibrary.Log($"Up to date with patch {version}.");
                progressBar.Maximum = progressBar.Value = 1;
                IniLibrary.instance.LastPatchedVersion = filelist.version;
                IniLibrary.Save();
                btnCheck.BackColor = SystemColors.Control;
                btnCheck.Text = "Patch";
                return;
            }

            StatusLibrary.Log("Downloading " + totalBytes + " bytes for " + filesToDownload.Count + " files...");
            int curBytes = 0;
            progressBar.Maximum = totalBytes;
            progressBar.Value = 0;
            foreach (var entry in filesToDownload)
            {
                progressBar.Value = (curBytes > totalBytes) ? totalBytes : curBytes;
                string url = filelist.downloadprefix + entry.name.Replace("\\", "/");
                string resp = DownloadFile(url, entry.name);
                if (resp != "")
                {
                    if (resp == "404")
                    {
                        StatusLibrary.Log($"Failed to download {url}, 404 error (website may be down?)");
                        return;
                    }
                    StatusLibrary.Log($"Failed to download {url}: {resp}");
                    return;
                }
                curBytes += entry.size;
                Application.DoEvents();
                if (!isPatching)
                {
                    StatusLibrary.Log("Patching cancelled.");
                    return;
                }
            }
            progressBar.Value = progressBar.Maximum;
            StatusLibrary.Log("Complete! Press Play to begin.");
            IniLibrary.instance.LastPatchedVersion = filelist.version;
            IniLibrary.Save();
            btnCheck.BackColor = SystemColors.Control;
            btnCheck.Text = "Patch";
        }


        private void chkAutoPlay_CheckedChanged(object sender, EventArgs e)
        {
            if (isLoading) return;
            IniLibrary.instance.AutoPlay = (chkAutoPlay.Checked) ? "true" : "false";
            if (chkAutoPlay.Checked) StatusLibrary.Log("To disable autoplay: edit eqemupatcher.yml or wait until next patch.");
            IniLibrary.Save();
        }

        private void chkAutoPatch_CheckedChanged(object sender, EventArgs e)
        {
            if (isLoading) return;
            IniLibrary.instance.AutoPatch = (chkAutoPatch.Checked) ? "true" : "false";
            IniLibrary.Save();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (isNeedingPatch && IniLibrary.instance.AutoPatch == "true")
            {
                btnCheck.BackColor = SystemColors.Control;
                StartPatch();
            }
        }
    }
    public class FileList
    {
        public string version { get; set; }
        
        public List<FileEntry> deletes { get; set; }
        public string downloadprefix { get; set; }
        public List<FileEntry> downloads { get; set; }
        public List<FileEntry> unpacks { get; set; }

    }
    public class FileEntry
    {
        public string name { get; set;  }
        public string md5 { get; set; }
        public string date { get; set; }
        public string zip { get; set; }
        public int size { get; set; }
    }    
}


