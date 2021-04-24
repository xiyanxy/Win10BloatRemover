using Microsoft.Win32;
using Win10BloatRemover.Utils;

namespace Win10BloatRemover.Operations
{
    public class DefenderDisabler : IOperation
    {
        private static readonly string[] securityHealthServices = {
            "SecurityHealthService",
            "wscsvc",
            "Sense",
            "SgrmBroker",
            "SgrmAgent"
        };

        private static readonly string[] defenderScheduledTasks = {
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Verification"
        };

        private readonly IUserInterface ui;
        private readonly IOperation securityCenterRemover;
        private readonly ServiceRemover serviceRemover;

        public bool IsRebootRecommended { get; private set; }

        public DefenderDisabler(IUserInterface ui, IOperation securityCenterRemover, ServiceRemover serviceRemover)
        {
            this.ui = ui;
            this.securityCenterRemover = securityCenterRemover;
            this.serviceRemover = serviceRemover;
        }

        public void Run()
        {
            DowngradeAntimalwarePlatform();
            EditWindowsRegistryKeys();
            RemoveSecurityHealthServices();
            DisableDefenderScheduledTasks();
            securityCenterRemover.Run();
        }

        // DisableAntiSpyware policy is not honored anymore on Defender antimalware platform version 4.18.2007.8+
        // This workaround will last until Windows ships with a lower version of that platform pre-installed
        private void DowngradeAntimalwarePlatform()
        {
            ui.PrintHeading("降级Defender杀毒软件...");
            var exitCode = SystemUtils.RunProcessBlockingWithOutput(
                $@"{SystemUtils.GetProgramFilesFolder()}\Windows Defender\MpCmdRun.exe", "-resetplatform", ui);

            if (exitCode.IsNotSuccessful())
            {
                ui.PrintWarning(
                    "杀毒软件降级失败. 发生这种情况的可能是因为您已经禁用了Windows Defender.\n" +
                    "如果这不是您的情况，则无论如何都可以继续进行，但是请注意，如果通过系统更新将" +
                    "Defender更新为4.18.2007.8版或更高版本，则Defender将不会被完全禁用.");
                ui.ThrowIfUserDenies("是否继续?");
            }
            IsRebootRecommended = true;
        }

        private void EditWindowsRegistryKeys()
        {
            ui.PrintHeading("在Windows注册表中编辑数据...");

            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 0);
            RegistryUtils.SetForCurrentAndDefaultUser(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\MicrosoftEdge\PhishingFilter", "EnabledV9", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1);
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet"))
            {
                key.SetValue("SpynetReporting", 0);
                key.SetValue("SubmitSamplesConsent", 2);
            }
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\MRT"))
            {
                key.SetValue("DontReportInfectionInformation", 1);
                key.SetValue("DontOfferThroughWUAU", 1);
            }

            using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            localMachine.DeleteSubKeyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "SecurityHealth");
            localMachine.DeleteSubKeyValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "SecurityHealth");

            using RegistryKey notificationSettings = localMachine.CreateSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance"
            );
            notificationSettings.SetValue("Enabled", 0);
        }

        private void RemoveSecurityHealthServices()
        {
            ui.PrintHeading("删除安全服务...");
            serviceRemover.BackupAndRemove(securityHealthServices);
        }

        private void DisableDefenderScheduledTasks()
        {
            ui.PrintHeading("禁用Windows Defender预定任务...");
            new ScheduledTasksDisabler(defenderScheduledTasks, ui).Run();
        }
    }
}
