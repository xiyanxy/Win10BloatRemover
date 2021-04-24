using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using Win10BloatRemover.Utils;
using Env = System.Environment;

namespace Win10BloatRemover.Operations
{
    public enum UWPAppRemovalMode
    {
        CurrentUser,
        AllUsers
    }

    public enum UWPAppGroup
    {
        AlarmsAndClock,
        Bing,               // Weather, News, Finance and Sports
        Calculator,
        Camera,
        CommunicationsApps,
        Cortana,
        HelpAndFeedback,
        LegacyEdge,
        Maps,
        Messaging,
        MixedReality,       // 3D Viewer, Print 3D and Mixed Reality Portal
        Mobile,             // Your Phone and Mobile plans (aka OneConnect)
        OfficeHub,
        OneNote,
        Paint3D,
        Photos,
        SecurityCenter,
        Skype,
        SnipAndSketch,
        SolitaireCollection,
        SoundRecorder,
        StickyNotes,
        Store,
        Xbox,
        Zune                // Groove Music and Movies
    }

    public class UWPAppRemover : IOperation
    {
        // This dictionary contains the exact apps names corresponding to every defined group
        private static readonly Dictionary<UWPAppGroup, string[]> appNamesForGroup = new Dictionary<UWPAppGroup, string[]> {
            { UWPAppGroup.AlarmsAndClock, new[] { "Microsoft.WindowsAlarms" } },
            {
                UWPAppGroup.Bing, new[] {
                    "Microsoft.BingNews",
                    "Microsoft.BingWeather",
                    "Microsoft.BingFinance",
                    "Microsoft.BingSports"
                }
            },
            { UWPAppGroup.Calculator, new[] { "Microsoft.WindowsCalculator" } },
            { UWPAppGroup.Camera, new[] { "Microsoft.WindowsCamera" } },
            { UWPAppGroup.CommunicationsApps, new[] { "microsoft.windowscommunicationsapps", "Microsoft.People" } },
            { UWPAppGroup.Cortana, new[] { "Microsoft.549981C3F5F10" } },
            {
                UWPAppGroup.HelpAndFeedback, new[] {
                    "Microsoft.WindowsFeedbackHub",
                    "Microsoft.GetHelp",
                    "Microsoft.Getstarted"
                }
            },
            { UWPAppGroup.LegacyEdge, new[] { "Microsoft.MicrosoftEdge", "Microsoft.MicrosoftEdgeDevToolsClient" } },
            { UWPAppGroup.Maps, new[] { "Microsoft.WindowsMaps" } },
            { UWPAppGroup.Messaging, new[] { "Microsoft.Messaging" } },
            {
                UWPAppGroup.MixedReality, new[] {
                    "Microsoft.Microsoft3DViewer",
                    "Microsoft.Print3D",
                    "Microsoft.MixedReality.Portal"
                }
            },
            { UWPAppGroup.Mobile, new[] { "Microsoft.YourPhone", "Microsoft.OneConnect" } },
            { UWPAppGroup.OfficeHub, new[] { "Microsoft.MicrosoftOfficeHub" } },
            { UWPAppGroup.OneNote, new[] { "Microsoft.Office.OneNote" } },
            { UWPAppGroup.Paint3D, new[] { "Microsoft.MSPaint" } },
            { UWPAppGroup.Photos, new[] { "Microsoft.Windows.Photos" } },
            { UWPAppGroup.SecurityCenter, new[] { "Microsoft.Windows.SecHealthUI" } },
            { UWPAppGroup.Skype, new[] { "Microsoft.SkypeApp" } },
            { UWPAppGroup.SnipAndSketch, new[] { "Microsoft.ScreenSketch" } },
            { UWPAppGroup.SolitaireCollection, new[] { "Microsoft.MicrosoftSolitaireCollection" } },
            { UWPAppGroup.SoundRecorder, new[] { "Microsoft.WindowsSoundRecorder" } },
            { UWPAppGroup.StickyNotes, new[] { "Microsoft.MicrosoftStickyNotes" } },
            {
                UWPAppGroup.Store, new[] {
                    "Microsoft.WindowsStore",
                    "Microsoft.StorePurchaseApp",
                    "Microsoft.Services.Store.Engagement",
                }
            },
            {
                UWPAppGroup.Xbox, new[] {
                    "Microsoft.XboxGameCallableUI",
                    "Microsoft.XboxSpeechToTextOverlay",
                    "Microsoft.XboxApp",
                    "Microsoft.XboxGameOverlay",
                    "Microsoft.XboxGamingOverlay",
                    "Microsoft.XboxIdentityProvider",
                    "Microsoft.Xbox.TCUI"
                }
            },
            { UWPAppGroup.Zune, new[] { "Microsoft.ZuneMusic", "Microsoft.ZuneVideo" } }
        };

        private readonly Dictionary<UWPAppGroup, Action> postUninstallOperationsForGroup;
        private readonly UWPAppGroup[] appsToRemove;
        private readonly UWPAppRemovalMode removalMode;
        private readonly IUserInterface ui;
        private readonly ServiceRemover serviceRemover;

        private /*lateinit*/ PowerShell powerShell;
        private int removedApps = 0;

        public bool IsRebootRecommended { get; private set; }

        #nullable disable warnings
        public UWPAppRemover(UWPAppGroup[] appsToRemove, UWPAppRemovalMode removalMode, IUserInterface ui, ServiceRemover serviceRemover)
        {
            this.appsToRemove = appsToRemove;
            this.removalMode = removalMode;
            this.ui = ui;
            this.serviceRemover = serviceRemover;

            postUninstallOperationsForGroup = new Dictionary<UWPAppGroup, Action> {
                { UWPAppGroup.CommunicationsApps, RemoveOneSyncServiceFeature },
                { UWPAppGroup.Cortana, HideCortanaFromTaskBar },
                { UWPAppGroup.LegacyEdge, RemoveEdgeLeftovers },
                { UWPAppGroup.Maps, RemoveMapsServicesAndTasks },
                { UWPAppGroup.Messaging, RemoveMessagingService },
                { UWPAppGroup.Paint3D, RemovePaint3DContextMenuEntries },
                { UWPAppGroup.Photos, RestoreWindowsPhotoViewer },
                { UWPAppGroup.MixedReality, RemoveMixedRealityAppsLeftovers },
                { UWPAppGroup.Xbox, RemoveXboxServicesAndTasks },
                { UWPAppGroup.Store, DisableStoreFeaturesAndServices }
            };
        }
        #nullable restore warnings

        public void Run()
        {
            using (powerShell = PowerShellExtensions.CreateWithImportedModules("AppX").WithOutput(ui))
            {
                foreach (UWPAppGroup appGroup in appsToRemove)
                    UninstallAppsOfGroup(appGroup);
            }

            if (removedApps > 0)
                RestartExplorer();
        }

        private void UninstallAppsOfGroup(UWPAppGroup appGroup)
        {
            string[] appsInGroup = appNamesForGroup[appGroup];
            ui.PrintHeading($"卸载 {appGroup} {(appsInGroup.Length == 1 ? "app" : "apps")}...");
            int removedAppsForGroup = 0;
            foreach (string appName in appsInGroup)
            {
                // Starting from OS version 1909, the PowerShell command used by UninstallApp should already remove
                // the corresponding provisioned package when the app is removed for all users.
                // Since this behavior is not officially documented and seems not to be consistent across all Windows versions,
                // we want to make sure that the provisioned package gets uninstalled to provide a consistent behavior.
                // Also, in version 2004 uninstalling an app for all users raises an error if the provisioned package has not
                // been already removed.
                if (removalMode == UWPAppRemovalMode.AllUsers)
                    UninstallAppProvisionedPackage(appName);

                bool removalSuccessful = UninstallApp(appName);
                if (removalSuccessful)
                    removedAppsForGroup++;
            }
            removedApps += removedAppsForGroup;
            if (removalMode == UWPAppRemovalMode.AllUsers && removedAppsForGroup > 0)
                TryPerformPostUninstallOperations(appGroup);
        }

        private bool UninstallApp(string appName)
        {
            var packages = powerShell.Run(GetAppxPackageCommand(appName));
            if (packages.Length == 0)
            {
                ui.PrintMessage($"App {appName} 尚未安装.");
                return false;
            }

            ui.PrintMessage($"卸载app {appName}...");
            foreach (var package in packages) // some apps have both x86 and x64 variants installed
            {
                string command = RemoveAppxPackageCommand(package.PackageFullName);
                powerShell.Run(command);
            }
            return powerShell.Streams.Error.Count == 0;
        }

        private string GetAppxPackageCommand(string appName)
        {
            string command = "Get-AppxPackage ";
            if (removalMode == UWPAppRemovalMode.AllUsers)
                command += "-AllUsers ";
            return command + $"-Name \"{appName}\"";
        }

        private string RemoveAppxPackageCommand(string fullPackageName)
        {
            string command = "Remove-AppxPackage ";
            if (removalMode == UWPAppRemovalMode.AllUsers)
                command += "-AllUsers ";
            return command + $"-Package \"{fullPackageName}\"";
        }

        private void UninstallAppProvisionedPackage(string appName)
        {
            var provisionedPackage = powerShell.Run("Get-AppxProvisionedPackage -Online")
                .FirstOrDefault(package => package.DisplayName == appName);
            if (provisionedPackage != null)
            {
                ui.PrintMessage($"删除应用的预配包 {appName}...");
                powerShell.Run(
                    $"Remove-AppxProvisionedPackage -Online -PackageName \"{provisionedPackage.PackageName}\""
                );
            }
        }

        private void RestartExplorer()
        {
            ui.PrintHeading("重新启动资源管理器以避免在\"开始\"菜单中出现过时的应用程序条目...");
            SystemUtils.KillProcess("explorer");
            Process.Start("explorer");
        }

        private void TryPerformPostUninstallOperations(UWPAppGroup appGroup)
        {
            try
            {
                if (postUninstallOperationsForGroup.ContainsKey(appGroup))
                {
                    ui.PrintEmptySpace();
                    postUninstallOperationsForGroup[appGroup]();
                    IsRebootRecommended = true;
                }
            }
            catch (Exception exc)
            {
                ui.PrintError(
                    $"对应用程序组执行卸载/清理后操作时发生错误 {appGroup}: {exc.Message}");
            }
        }

        private void RemoveEdgeLeftovers()
        {
            ui.PrintMessage("删除旧文件...");
            SystemUtils.TryDeleteDirectoryIfExists(
                $@"{Env.GetFolderPath(Env.SpecialFolder.UserProfile)}\MicrosoftEdgeBackups",
                ui
            );
            SystemUtils.TryDeleteDirectoryIfExists(
                $@"{Env.GetFolderPath(Env.SpecialFolder.LocalApplicationData)}\MicrosoftEdge",
                ui
            );
        }

        private void HideCortanaFromTaskBar()
        {
            ui.PrintMessage("从当前用户和默认用户的任务栏中隐藏Cortana...");
            RegistryUtils.SetForCurrentAndDefaultUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCortanaButton", 0);
        }

        private void RemoveMapsServicesAndTasks()
        {
            ui.PrintMessage("删除与应用程序相关的计划任务和服务...");
            new ScheduledTasksDisabler(new[] {
                @"\Microsoft\Windows\Maps\MapsUpdateTask",
                @"\Microsoft\Windows\Maps\MapsToastTask"
            }, ui).Run();
            serviceRemover.BackupAndRemove("MapsBroker", "lfsvc");
        }

        private void RemoveXboxServicesAndTasks()
        {
            ui.PrintMessage("删除与应用程序相关的计划任务和服务...");
            new ScheduledTasksDisabler(new[] { @"Microsoft\XblGameSave\XblGameSaveTask" }, ui).Run();
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
            serviceRemover.BackupAndRemove("XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc");
        }

        private void RemoveMessagingService()
        {
            ui.PrintMessage("删除与应用程序相关的服务...");
            serviceRemover.BackupAndRemove("MessagingService");
        }

        private void RemovePaint3DContextMenuEntries()
        {
            ui.PrintMessage("删除3D画图上下文菜单项...");
            SystemUtils.ExecuteWindowsPromptCommand(
                @"echo off & for /f ""tokens=1* delims="" %I in " +
                 @"(' reg query ""HKEY_CLASSES_ROOT\SystemFileAssociations"" /s /k /f ""3D Edit"" ^| find /i ""3D Edit"" ') " +
                @"do (reg delete ""%I"" /f )",
                ui
            );
        }

        private void RemoveMixedRealityAppsLeftovers()
        {
            Remove3DObjectsFolder();
            Remove3DPrintContextMenuEntries();
        }

        private void Remove3DObjectsFolder()
        {
            ui.PrintMessage("删除3D对象文件夹...");
            using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey key = localMachine.OpenSubKeyWritable(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace"
            );
            key.DeleteSubKeyTree("{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}", throwOnMissingSubKey: false);

            SystemUtils.TryDeleteDirectoryIfExists($@"{Env.GetFolderPath(Env.SpecialFolder.UserProfile)}\3D Objects", ui);
        }

        private void Remove3DPrintContextMenuEntries()
        {
            ui.PrintMessage("删除3D打印上下文菜单项...");
            SystemUtils.ExecuteWindowsPromptCommand(
                @"echo off & for /f ""tokens=1* delims="" %I in " +
                @"(' reg query ""HKEY_CLASSES_ROOT\SystemFileAssociations"" /s /k /f ""3D Print"" ^| find /i ""3D Print"" ') " +
                @"do (reg delete ""%I"" /f )",
                ui
            );
        }

        private void RestoreWindowsPhotoViewer()
        {
            ui.PrintMessage("为BMP，GIF，JPEG，PNG和TIFF图片设置与旧照片查看器的文件关联...");

            const string PHOTO_VIEWER_SHELL_COMMAND =
                @"%SystemRoot%\System32\rundll32.exe ""%ProgramFiles%\Windows Photo Viewer\PhotoViewer.dll"", ImageView_Fullscreen %1";
            const string PHOTO_VIEWER_CLSID = "{FFE2A43C-56B9-4bf5-9A79-CC6D4285608A}";

            Registry.SetValue(@"HKEY_CLASSES_ROOT\Applications\photoviewer.dll\shell\open", "MuiVerb", "@photoviewer.dll,-3043");
            Registry.SetValue(
                @"HKEY_CLASSES_ROOT\Applications\photoviewer.dll\shell\open\command", valueName: null,
                PHOTO_VIEWER_SHELL_COMMAND, RegistryValueKind.ExpandString
            );
            Registry.SetValue(@"HKEY_CLASSES_ROOT\Applications\photoviewer.dll\shell\open\DropTarget", "Clsid", PHOTO_VIEWER_CLSID);

            string[] imageTypes = { "Paint.Picture", "giffile", "jpegfile", "pngfile" };
            foreach (string type in imageTypes)
            {
                Registry.SetValue(
                    $@"HKEY_CLASSES_ROOT\{type}\shell\open\command", valueName: null,
                    PHOTO_VIEWER_SHELL_COMMAND, RegistryValueKind.ExpandString
                );
                Registry.SetValue($@"HKEY_CLASSES_ROOT\{type}\shell\open\DropTarget", "Clsid", PHOTO_VIEWER_CLSID);
            }
        }

        private void RemoveOneSyncServiceFeature()
        {
            new FeaturesRemover(new[] { "OneCoreUAP.OneSync" }, ui).Run();
        }

        private void DisableStoreFeaturesAndServices()
        {
            ui.PrintMessage("将值写入注册表...");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore", "RemoveWindowsStore", 1);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\PushToInstall", "DisablePushToInstall", 1);
            RegistryUtils.SetForCurrentAndDefaultUser(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                "SilentInstalledAppsEnabled", 0
            );
            RegistryUtils.SetForCurrentAndDefaultUser(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost",
                "EnableWebContentEvaluation", 0
            );

            ui.PrintMessage("删除与应用程序相关的服务...");
            serviceRemover.BackupAndRemove("PushToInstall");
        }
    }
}
