using System;
using System.Diagnostics;
using System.Security.Principal;
using Win10BloatRemover.Utils;

namespace Win10BloatRemover
{
    static class Program
    {
        public const string SUPPORTED_WINDOWS_RELEASE_ID = "2004";
        private const string SUPPORTED_WINDOWS_RELEASE_NAME = "May 2020 Update";

        private static void Main()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            Console.Title = "Windows 10 修改器";

            EnsureProgramIsRunningAsAdmin();
            ShowWarningIfRunningOnIncompatibleOS();
            RegisterExitEventHandlers();

            var configuration = LoadConfigurationFromFileOrDefault();
            var rebootFlag = new RebootRecommendedFlag();
            var menu = new ConsoleMenu(CreateMenuEntries(configuration, rebootFlag), rebootFlag);
            menu.RunLoopUntilExitRequested();
        }

        private static MenuEntry[] CreateMenuEntries(Configuration configuration, RebootRecommendedFlag rebootFlag)
        {
            return new MenuEntry[] {
                new SystemAppsRemovalEnablingEntry(),
                new UWPAppRemovalEntry(configuration),
                new EdgeRemovalEntry(),
                new OneDriveRemovalEntry(),
                new ServicesRemovalEntry(configuration),
                new WindowsFeaturesRemovalEntry(configuration),
                new PrivacySettingsTweakEntry(),
                new TelemetryDisablingEntry(),
                new DefenderDisablingEntry(),
                new AutoUpdatesDisablingEntry(),
                new ScheduledTasksDisablingEntry(configuration),
                new ErrorReportingDisablingEntry(),
                new TipsAndFeedbackDisablingEntry(),
                new NewGitHubIssueEntry(),
                new AboutEntry(),
                new QuitEntry(rebootFlag)
            };
        }

        private static void EnsureProgramIsRunningAsAdmin()
        {
            if (!Program.HasAdministratorRights())
            {
                ConsoleHelpers.WriteLine("此应用程序需要以管理员权限运行!", ConsoleColor.Red);
                Console.ReadKey();
                Environment.Exit(-1);
            }
        }

        private static void ShowWarningIfRunningOnIncompatibleOS()
        {
            string? installedWindowsVersion = SystemUtils.RetrieveWindowsReleaseId();
            if (installedWindowsVersion != SUPPORTED_WINDOWS_RELEASE_ID)
            {
                ConsoleHelpers.WriteLine(
                    "-- 兼容性警告 --\n\n" +
                    $"此版本的程序仅支持Windows 10 {SUPPORTED_WINDOWS_RELEASE_NAME} (版本 {SUPPORTED_WINDOWS_RELEASE_ID}).\n" +
                    $"您正在运行Windows 10版本 {installedWindowsVersion}.\n\n" +
                    "您应该在下面链接下载与此Windows 10版本兼容的程序版本:",
                    ConsoleColor.DarkYellow);
                Console.WriteLine("  https://github.com/Fs00/Win10BloatRemover/releases/\n");
                ConsoleHelpers.WriteLine(
                    "如果尚没有兼容版本，您仍然可以继续使用该程序.\n" +
                    "但是，请注意，某些功能可能无法正常工作或根本无法工作，甚至可能会产生意想不到的效果\n" +
                    "在您的系统上（包括系统损坏或不稳定）.\n" +
                    "继续需要您自担风险.", ConsoleColor.DarkYellow);
                
                Console.WriteLine("\n按Enter继续，或按任意键键退出.");
                if (Console.ReadKey().Key != ConsoleKey.Enter)
                    Environment.Exit(-1);
            }
        }

        private static bool HasAdministratorRights()
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        
        private static Configuration LoadConfigurationFromFileOrDefault()
        {
            try
            {
                return Configuration.LoadFromFileOrDefault();
            }
            catch (ConfigurationException exc)
            {
                ConsoleHelpers.WriteLine(exc.Message, ConsoleColor.DarkYellow);
                Console.WriteLine("按任意键返回主菜单.");
                Console.ReadKey();
                return Configuration.Default;
            }
        }

        private static void RegisterExitEventHandlers()
        {
            #if !DEBUG
            bool cancelKeyPressedOnce = false;
            Console.CancelKeyPress += (sender, args) => {
                if (!cancelKeyPressedOnce)
                {
                    ConsoleHelpers.WriteLine("Press Ctrl+C again to terminate the program.", ConsoleColor.Red);
                    cancelKeyPressedOnce = true;
                    args.Cancel = true;
                }
                else
                    Process.GetCurrentProcess().KillChildProcesses();
            };
            #endif

            // Executed when the user closes the window. This handler is not fired when process is terminated with Ctrl+C
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => Process.GetCurrentProcess().KillChildProcesses();
        }
    }
}
