using AutoUpdaterDotNET;
using Dalamud.Updater.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Configuration;
using Newtonsoft.Json.Linq;
using System.Security.Principal;
using System.Xml;
using XIVLauncher.Common.Dalamud;
using Serilog.Core;
using Serilog;
using Serilog.Events;

namespace Dalamud.Updater
{
    public partial class FormMain : Form
    {
        private string updateUrl = "https://raw.githubusercontent.com/dohwacorp/DalamudResource/main/updater.xml";

        // private List<string> pidList = new List<string>();
        private bool firstHideHint = true;
        private bool isThreadRunning = true;
        private bool dotnetDownloadFinished = true;
        private bool desktopDownloadFinished = true;
        //private string dotnetDownloadPath;
        //private string desktopDownloadPath;
        //private DirectoryInfo runtimePath;
        //private DirectoryInfo[] runtimePaths;
        //private string RuntimeVersion = "5.0.17";
        private double injectDelaySeconds = 1;
        private DalamudLoadingOverlay dalamudLoadingOverlay;

        private readonly DirectoryInfo addonDirectory;
        private readonly DirectoryInfo runtimeDirectory;
        private readonly DirectoryInfo xivlauncherDirectory;
        private readonly DirectoryInfo assetDirectory;
        private readonly DirectoryInfo configDirectory;

        private readonly DalamudUpdater dalamudUpdater;

        public string windowsTitle = "DalamudKR v" + Assembly.GetExecutingAssembly().GetName().Version;
        #region Oversea Accelerate Helper
        private bool RemoteFileExists(string url)
        {
            try
            {
                //HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                var request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "HEAD";
                //HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                var response = request.GetResponse() as HttpWebResponse;
                response.Close();
                return (response.StatusCode == HttpStatusCode.OK);
            }
            catch
            {
                return false;
            }
        }

        private string GetProperUrl(string url)
        {
            if (RemoteFileExists(url))
                return url;
            var accUrl = url.Replace("/updater.xml", "/acce_updater.xml").Replace("ap-nanjing", "accelerate");
            return accUrl;
        }

        #endregion

        public static string GetAppSettings(string key, string def = null)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                var ele = settings[key];
                if (ele == null) return def;
                return ele.Value;
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
            }
            return def;
        }
        public static void AddOrUpdateAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error writing app settings");
            }
        }

        private int checkTimes = 0;

        private void CheckUpdate()
        {
            //checkTimes++;
            //if (checkTimes == 8)
            //{
            //    MessageBox.Show("8 Times", windowsTitle);
            //}
            //else if (checkTimes == 9)
            //{
            //    MessageBox.Show("9 Times", windowsTitle);
            //}
            //else if (checkTimes > 10)
            //{
            //    MessageBox.Show("Too many Tries...", windowsTitle);
            //}
            // [[ Removing Useless Update Check Message ]] //
            dalamudUpdater.Run();
        }

        private Version GetUpdaterVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        private string getVersion()
        {
            var rgx = new Regex(@"^\d+\.\d+\.\d+\.\d+$");
            var stgRgx = new Regex(@"^[\da-zA-Z]{7}$");
            var di = new DirectoryInfo(Path.Combine(addonDirectory.FullName, "Hooks"));
            var version = new Version("0.0.0.0");
            if (!di.Exists)
                return version.ToString();
            var dirs = di.GetDirectories("*", SearchOption.TopDirectoryOnly).Where(dir => rgx.IsMatch(dir.Name)).ToArray();
            bool releaseVersionExists = false;
            foreach (var dir in dirs)
            {
                var newVersion = new Version(dir.Name);
                if (newVersion > version)
                {
                    releaseVersionExists = true;
                    version = newVersion;
                }
            }
            if (!releaseVersionExists)
            {
                var stgDirs = di.GetDirectories("*", SearchOption.TopDirectoryOnly).Where(dir => stgRgx.IsMatch(dir.Name)).ToArray();
                if (stgDirs.Length > 0)
                {
                    return stgDirs[0].Name;
                }
            }
            return version.ToString();
        }


        public FormMain()
        {
            InitLogging();
            InitializeComponent();
            InitializePIDCheck();
            InitializeDeleteShit();
            InitializeConfig();
            addonDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            dalamudLoadingOverlay = new DalamudLoadingOverlay(this);
            dalamudLoadingOverlay.OnProgressBar += setProgressBar;
            dalamudLoadingOverlay.OnSetVisible += setVisible;
            dalamudLoadingOverlay.OnStatusLabel += setStatus;
            addonDirectory = new DirectoryInfo(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "XIVLauncher", "addon"));
            runtimeDirectory = new DirectoryInfo(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "XIVLauncher", "runtime"));
            xivlauncherDirectory = new DirectoryInfo(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "XIVLauncher"));
            assetDirectory = new DirectoryInfo(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "XIVLauncher", "dalamudAssets"));
            configDirectory = new DirectoryInfo(Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "XIVLauncher"));
            //labelVersion.Text = string.Format("DalamudKR : {0}", getVersion());
            delayBox.Value = (decimal)this.injectDelaySeconds;
            SetDalamudVersion();
            //string[] strArgs = Environment.GetCommandLineArgs();
            var strArgs = Environment.GetCommandLineArgs();
            if (strArgs.Length >= 2 && strArgs[1].Equals("-startup"))
            {
                //this.WindowState = FormWindowState.Minimized;
                //this.ShowInTaskbar = false;
                if (firstHideHint)
                {
                    firstHideHint = false;
                    this.DalamudUpdaterIcon.ShowBalloonTip(2000, "자동 시작", "백그라운드에서 자동으로 시작했습니다.", ToolTipIcon.Info);
                }
            }
            try
            {
                var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var remoteUrl = GetProperUrl(updateUrl);
                //XmlDocument remoteXml = new XmlDocument();
                var remoteXml = new XmlDocument();
                remoteXml.Load(remoteUrl);
                //foreach (XmlNode child in remoteXml.SelectNodes("/item/version"))
                var nodes = remoteXml.SelectNodes("/item/version");
                if (nodes == null) throw new NullReferenceException("/item/version not exist");

                foreach (XmlNode child in nodes)
                {
                    if (child.InnerText != localVersion.ToString())
                    {
                        AutoUpdater.Start(remoteUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to check Program Start Version",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            dalamudUpdater = new DalamudUpdater(addonDirectory, runtimeDirectory, assetDirectory, configDirectory);
            dalamudUpdater.Overlay = dalamudLoadingOverlay;
            dalamudUpdater.OnUpdateEvent += DalamudUpdater_OnUpdateEvent;
        }

        private void DalamudUpdater_OnUpdateEvent(DalamudUpdater.DownloadState value)
        {
            switch (value)
            {
                case DalamudUpdater.DownloadState.Failed:
                    MessageBox.Show("Failed Update Dalamud", windowsTitle, MessageBoxButtons.YesNo);
                    setStatus("Failed Update Dalamud");
                    break;
                case DalamudUpdater.DownloadState.Unknown:
                    setStatus("Unknown Error");
                    break;
                case DalamudUpdater.DownloadState.NoIntegrity:
                    setStatus("Version Imcompatible");
                    break;
                case DalamudUpdater.DownloadState.Done:
                    SetDalamudVersion();
                    setStatus("Success Update Dalamud");
                    break;
                case DalamudUpdater.DownloadState.Checking:
                    setStatus("Checking Update...");
                    break;
            }
        }

        public void SetDalamudVersion()
        {
            var verStr = string.Format("DalamudKR : {0}", getVersion());
            if (this.labelVersion.InvokeRequired)
            {
                Action<string> actionDelegate = (x) => { labelVersion.Text = x; };
                this.labelVersion.Invoke(actionDelegate, verStr);
            }
            else
            {
                labelVersion.Text = verStr;
            }
        }
        #region init
        private static void InitLogging()
        {
            var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var logPath = Path.Combine(baseDirectory, "Dalamud.Updater.log");

            var levelSwitch = new LoggingLevelSwitch();

#if DEBUG
            levelSwitch.MinimumLevel = LogEventLevel.Verbose;
#else
            levelSwitch.MinimumLevel = LogEventLevel.Information;
#endif


            Log.Logger = new LoggerConfiguration()
                //.WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
                .WriteTo.Async(a => a.File(logPath))
                .MinimumLevel.ControlledBy(levelSwitch)
                .CreateLogger();
        }
        private void InitializeConfig()
        {
            if (GetAppSettings("AutoInject", "false") == "true")
            {
                this.checkBoxAutoInject.Checked = true;
            }
            if (GetAppSettings("AutoStart", "false") == "true")
            {
                this.checkBoxAutoStart.Checked = true;
            }
            var tempInjectDelaySeconds = GetAppSettings("InjectDelaySeconds", "0");
            if (tempInjectDelaySeconds != "0")
            {
                this.injectDelaySeconds = double.Parse(tempInjectDelaySeconds);
            }
        }

        private void InitializeDeleteShit()
        {
            var shitInjector = Path.Combine(Directory.GetCurrentDirectory(), "Dalamud.Injector.exe");
            if (File.Exists(shitInjector))
            {
                File.Delete(shitInjector);
            }

            var shitDalamud = Path.Combine(Directory.GetCurrentDirectory(), "6.3.0.9");
            if (Directory.Exists(shitDalamud))
            {
                Directory.Delete(shitDalamud, true);
            }

            var shitUIRes = Path.Combine(Directory.GetCurrentDirectory(), "XIVLauncher", "dalamudAssets", "UIRes");
            if (Directory.Exists(shitUIRes))
            {
                Directory.Delete(shitUIRes, true);
            }

            var shitAddon = Path.Combine(Directory.GetCurrentDirectory(), "addon");
            if (Directory.Exists(shitAddon))
            {
                Directory.Delete(shitAddon, true);
            }

            var shitRuntime = Path.Combine(Directory.GetCurrentDirectory(), "runtime");
            if (Directory.Exists(shitRuntime))
            {
                Directory.Delete(shitRuntime, true);
            }
        }

        private void InitializePIDCheck()
        {
            var thread = new Thread(() =>
            {
                while (this.isThreadRunning)
                {
                    try
                    {
                        // Fix not catching KR Client's PID
                        //var newPidList = Process.GetProcessesByName("ffxiv_dx11").Where(process =>
                        //{
                        //    return !process.MainWindowTitle.Contains("FINAL FANTASY XIV");
                        //}).ToList().ConvertAll(process => process.Id.ToString()).ToArray();
                        var newPidList = Process.GetProcessesByName("ffxiv_dx11").Select(x => x.Id.ToString()).ToArray();
                        var newHash = String.Join(", ", newPidList).GetHashCode();
                        var oldPidList = this.comboBoxFFXIV.Items.Cast<Object>().Select(item => item.ToString()).ToArray();
                        var oldHash = String.Join(", ", oldPidList).GetHashCode();
                        if (oldHash != newHash && this.comboBoxFFXIV.IsHandleCreated)
                        {
                            this.comboBoxFFXIV.Invoke((MethodInvoker)delegate
                            {
                                // Running on the UI thread
                                comboBoxFFXIV.Items.Clear();
                                comboBoxFFXIV.Items.AddRange(newPidList.Cast<object>().ToArray());
                                if (newPidList.Length > 0)
                                {
                                    if (!comboBoxFFXIV.DroppedDown)
                                        this.comboBoxFFXIV.SelectedIndex = 0;
                                    if (this.checkBoxAutoInject.Checked)
                                    {
                                        foreach (var pidStr in newPidList)
                                        {
                                            //Thread.Sleep((int)(this.injectDelaySeconds * 1000));
                                            var pid = int.Parse(pidStr);
                                            if (this.Inject(pid, (int)this.injectDelaySeconds * 1000))
                                            {
                                                this.DalamudUpdaterIcon.ShowBalloonTip(2000, "자동 적용", $"Process ID : {pid}, 적용 완료.", ToolTipIcon.Info);
                                            }
                                        }
                                    }
                                }
                            });
                        }
                    }
                    catch
                    {

                    }
                    Thread.Sleep(1000);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        #endregion
        private void FormMain_Load(object sender, EventArgs e)
        {
            AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;
            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
            AutoUpdater.InstalledVersion = GetUpdaterVersion();
            labelVer.Text = $"v{Assembly.GetExecutingAssembly().GetName().Version}";
            CheckUpdate();
        }
        private void FormMain_Disposed(object sender, EventArgs e)
        {
            this.isThreadRunning = false;
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
            //this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            //this.ShowInTaskbar = false;
            //this.Visible = false;
            if (firstHideHint)
            {
                firstHideHint = false;
                this.DalamudUpdaterIcon.ShowBalloonTip(2000, "달라가브 최소화", "트레이아이콘을 눌러 메뉴를 열 수 있습니다.", ToolTipIcon.Info);
            }
        }

        private void DalamudUpdaterIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //if (this.WindowState == FormWindowState.Minimized)
                //{
                //    this.WindowState = FormWindowState.Normal;
                //    this.FormBorderStyle = FormBorderStyle.FixedDialog;
                //    this.ShowInTaskbar = true;
                //}
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                }
                this.Activate();
            }
        }

        private void MenuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //WindowState = FormWindowState.Normal;
            if (!this.Visible) this.Visible = true;
            this.Activate();
        }
        private void QuitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Dispose();
            //this.Close();
            this.DalamudUpdaterIcon.Dispose();
            Application.Exit();
        }

        private void AutoUpdater_ApplicationExitEvent()
        {
            Text = @"Closing application...";
            Thread.Sleep(5000);
            Application.Exit();
        }


        private void AutoUpdaterOnParseUpdateInfoEvent(ParseUpdateInfoEventArgs args)
        {
            dynamic json = JsonConvert.DeserializeObject(args.RemoteData);
            args.UpdateInfo = new UpdateInfoEventArgs
            {
                CurrentVersion = json.version,
                ChangelogURL = json.changelog,
                DownloadURL = json.url,
                Mandatory = new Mandatory
                {
                    Value = json.mandatory.value,
                    UpdateMode = json.mandatory.mode,
                    MinimumVersion = json.mandatory.minVersion
                },
                CheckSum = new CheckSum
                {
                    Value = json.checksum.value,
                    HashingAlgorithm = json.checksum.hashingAlgorithm
                }
            };
        }

        private void OnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args.Error == null)
            {
                if (args.IsUpdateAvailable)
                {
                    DialogResult dialogResult;
                    if (args.Mandatory.Value)
                    {
                        dialogResult =
                            MessageBox.Show(
                                $@"달라가브 버전 {args.CurrentVersion} 이 나왔습니다. 현재 버전은 {args.InstalledVersion}입니다. 확인을 눌러 업데이트합니다.", @"Update",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                    }
                    else
                    {
                        dialogResult =
                            MessageBox.Show(
                                $@"달라가브 버전 {args.CurrentVersion} 이 나왔습니다. 현재 버전은 {args.InstalledVersion}입니다. 업데이트하시겠습니까?", @"Update Available",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information);
                    }


                    if (dialogResult.Equals(DialogResult.Yes) || dialogResult.Equals(DialogResult.OK))
                    {
                        try
                        {
                            //You can use Download Update dialog used by AutoUpdater.NET to download the update.

                            if (AutoUpdater.DownloadUpdate(args))
                            {
                                this.Dispose();
                                this.DalamudUpdaterIcon.Dispose();
                                Application.Exit();
                            }
                        }
                        catch (Exception exception)
                        {
                            MessageBox.Show(exception.Message, exception.GetType().ToString(), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
                else
                {
                    Log.Information("[Updater] No Update. Check Later.");
                    /*
                    MessageBox.Show(@"No Update. Check Later.", @"Update Unavailable.",
                                   MessageBoxButtons.OK, MessageBoxIcon.Information);
                    */
                }
            }
            else
            {
                if (args.Error is WebException)
                {
                    MessageBox.Show(
                        @"업데이트 서버 연결 실패",
                        @"Failed Update Check", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    MessageBox.Show(args.Error.Message,
                        args.Error.GetType().ToString(), MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            // [[ Disable Check Update ]] //
            // [[ Need to enable When update Dalamud ]] //
            //OnCheckForUpdateEvent(args);
        }

        private void ButtonCheckForUpdate_Click(object sender, EventArgs e)
        {
            if (this.comboBoxFFXIV.SelectedItem != null)
            {
                var pid = int.Parse((string)this.comboBoxFFXIV.SelectedItem);
                var process = Process.GetProcessById(pid);
                if (isInjected(process))
                {
                    var choice = MessageBox.Show("달라가브가 구버전입니다. 클라이언트를 종료합니다.", "Shutdown",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Information);
                    if (choice == DialogResult.Yes)
                    {
                        process.Kill();
                    }
                    else
                    {
                        return;
                    }
                }
            }
            CheckUpdate();
        }

        private void comboBoxFFXIV_Clicked(object sender, EventArgs e)
        {

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://discord.gg/Fdb9TTW9aD ");
        }

        private DalamudStartInfo GeneratingDalamudStartInfo(Process process, string dalamudPath, int injectDelay)
        {
            var ffxivDir = Path.GetDirectoryName(process.MainModule.FileName);
            var appDataDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            var xivlauncherDir = xivlauncherDirectory.FullName;

            var gameVerStr = File.ReadAllText(Path.Combine(ffxivDir, "ffxivgame.ver"));

            var startInfo = new DalamudStartInfo
            {
                ConfigurationPath = Path.Combine(xivlauncherDir, "dalamudConfig.json"),
                PluginDirectory = Path.Combine(xivlauncherDir, "installedPlugins"),
                DefaultPluginDirectory = Path.Combine(xivlauncherDir, "devPlugins"),
                RuntimeDirectory = runtimeDirectory.FullName,
                AssetDirectory = this.dalamudUpdater.AssetDirectory.FullName,
                GameVersion = gameVerStr,
                Language = "4",
                OptOutMbCollection = false,
                WorkingDirectory = dalamudPath,
                DelayInitializeMs = injectDelay
            };

            return startInfo;
        }

        private bool isInjected(Process process)
        {
            try
            {
                for (var j = 0; j < process.Modules.Count; j++)
                {
                    if (process.Modules[j].ModuleName == "Dalamud.dll")
                    {
                        return true;
                    }
                }
            }
            catch
            {

            }
            return false;
        }

        private void DetectNetEase(Process process)
        {
            try
            {
                for (var j = 0; j < process.Modules.Count; j++)
                {
                    if (process.Modules[j].ModuleName == "ws2detour_x64.dll")
                    {
                        MessageBox.Show("Detected Netease UU Accelerator Process Mode No Effect. Use Root.", windowsTitle, MessageBoxButtons.OK);
                    }
                }
            }
            catch
            {
            }
        }

        private bool Inject(int pid, int injectDelay = 0)
        {
            var process = Process.GetProcessById(pid);
            if (isInjected(process))
            {
                return false;
            }
            //DetectNetEase(process);

            //var dalamudStartInfo = Convert.ToBase64String(Encoding.UTF8.GetBytes(GeneratingDalamudStartInfo(process)));
            //var startInfo = new ProcessStartInfo(injectorFile, $"{pid} {dalamudStartInfo}");
            //startInfo.WorkingDirectory = dalamudPath.FullName;
            //Process.Start(startInfo);
            Log.Information($"[Updater] dalamudUpdater.State:{dalamudUpdater.State}");
            if (dalamudUpdater.State == DalamudUpdater.DownloadState.NoIntegrity)
            {
                if (MessageBox.Show("현재 달라가브 버전이 게임과 호환되지 않을 수 있습니다. 그래도 적용하시겠습니까?", windowsTitle, MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    return false;
                }
            }
            //return false;
            var dalamudStartInfo = GeneratingDalamudStartInfo(process, Directory.GetParent(dalamudUpdater.Runner.FullName).FullName, injectDelay);
            var environment = new Dictionary<string, string>();
            // No use cuz we're injecting instead of launching, the Dalamud.Boot.dll is reading environment variables from ffxiv_dx11.exe
            /*
            var prevDalamudRuntime = Environment.GetEnvironmentVariable("DALAMUD_RUNTIME");
            if (string.IsNullOrWhiteSpace(prevDalamudRuntime))
                environment.Add("DALAMUD_RUNTIME", runtimeDirectory.FullName);
            */
            WindowsDalamudRunner.Inject(dalamudUpdater.Runner, process.Id, environment, DalamudLoadMethod.DllInject, dalamudStartInfo);
            return true;
        }

        private void ButtonInject_Click(object sender, EventArgs e)
        {
            if (this.comboBoxFFXIV.SelectedItem != null
                && this.comboBoxFFXIV.SelectedItem.ToString().Length > 0)
            {
                var pidStr = this.comboBoxFFXIV.SelectedItem.ToString();
                if (int.TryParse(pidStr, out var pid))
                {
                    if (Inject(pid))
                    {
                        Log.Information("[DINJECT] Inject finished.");
                    }
                }
                else
                {
                    MessageBox.Show("클라이언트 검색 실패", "Error) FFXIV Not Found 1",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("클라이언트 선택 실패", "Error) FFXIV Not Found 2",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }

        }
        private void SetAutoRun()
        {
            // [[ No idea why changed this ]] //
            //string strFilePath = Application.ExecutablePath;
            var strFilePath = Application.ExecutablePath;
            try
            {
                SystemHelper.SetAutoRun($"\"{strFilePath}\"" + " -startup", "DalamudAutoInjector", checkBoxAutoStart.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void checkBoxAutoStart_CheckedChanged(object sender, EventArgs e)
        {
            SetAutoRun();
            AddOrUpdateAppSettings("AutoStart", checkBoxAutoStart.Checked ? "true" : "false");
        }

        private void checkBoxAutoInject_CheckedChanged(object sender, EventArgs e)
        {
            AddOrUpdateAppSettings("AutoInject", checkBoxAutoInject.Checked ? "true" : "false");
        }

        private void delayBox_ValueChanged(object sender, EventArgs e)
        {
            this.injectDelaySeconds = (double)delayBox.Value;
            AddOrUpdateAppSettings("InjectDelaySeconds", this.injectDelaySeconds.ToString());
        }

        private void setProgressBar(int v)
        {
            if (this.toolStripProgressBar1.Owner.InvokeRequired)
            {
                Action<int> actionDelegate = (x) => { toolStripProgressBar1.Value = x; };
                this.toolStripProgressBar1.Owner.Invoke(actionDelegate, v);
            }
            else
            {
                this.toolStripProgressBar1.Value = v;
            }
        }
        private void setStatus(string v)
        {
            if (toolStripStatusLabel1.Owner.InvokeRequired)
            {
                Action<string> actionDelegate = (x) => { toolStripStatusLabel1.Text = x; };
                this.toolStripStatusLabel1.Owner.Invoke(actionDelegate, v);
            }
            else
            {
                this.toolStripStatusLabel1.Text = v;
            }
        }
        private void setVisible(bool v)
        {
            if (toolStripProgressBar1.Owner.InvokeRequired)
            {
                Action<bool> actionDelegate = (x) =>
                {
                    toolStripProgressBar1.Visible = x;
                    //toolStripStatusLabel1.Visible = v; 
                };
                this.toolStripStatusLabel1.Owner.Invoke(actionDelegate, v);
            }
            else
            {
                toolStripProgressBar1.Visible = v;
                //toolStripStatusLabel1.Visible = v;
            }
        }
    }
}
