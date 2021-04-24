using Microsoft.Win32;

namespace Win10BloatRemover.Operations
{
    public class ErrorReportingDisabler : IOperation
    {
        private static readonly string[] errorReportingServices = { "WerSvc", "wercplsupport" };
        private static readonly string[] errorReportingScheduledTasks = {
            @"\Microsoft\Windows\Windows Error Reporting\QueueReporting"
        };

        private readonly IUserInterface ui;
        private readonly ServiceRemover serviceRemover;

        public bool IsRebootRecommended { get; private set; }

        public ErrorReportingDisabler(IUserInterface ui, ServiceRemover serviceRemover)
        {
            this.ui = ui;
            this.serviceRemover = serviceRemover;
        }

        public void Run()
        {
            DisableErrorReporting();
            RemoveErrorReportingServices();
            DisableErrorReportingScheduledTasks();
        }
        
        private void DisableErrorReporting()
        {
            ui.PrintHeading("将值写入注册表...");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\PCHealth\ErrorReporting", "DoReport", 0);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);
        }

        private void RemoveErrorReportingServices()
        {
            ui.PrintHeading("备份和删除错误报告服务...");
            serviceRemover.BackupAndRemove(errorReportingServices);
            IsRebootRecommended = serviceRemover.IsRebootRecommended;
        }
        
        private void DisableErrorReportingScheduledTasks()
        {
            ui.PrintHeading("禁用错误报告计划任务...");
            new ScheduledTasksDisabler(errorReportingScheduledTasks, ui).Run();
        }
    }
}
