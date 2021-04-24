using System;
using Win10BloatRemover.Operations;

namespace Win10BloatRemover
{
    abstract class MenuEntry
    {
        public abstract string FullName { get; }
        public virtual bool ShouldQuit => false;
        public abstract string GetExplanation();
        public abstract IOperation CreateNewOperation(IUserInterface ui);
    }

    class SystemAppsRemovalEnablingEntry : MenuEntry
    {
        public override string FullName => "使系统应用程序可移动";
        public override string GetExplanation()
        {
            return "此过程将编辑一个内部数据库，以允许通过PowerShell（此工具使用）和“设置”应用程序" +
                   "删除系统UWP应用程序，例如旧版Edge和安全中心.\n" +
                   "建议在继续之前创建系统还原点.\n\n" +
                   "通常，仅删除可以在\"开始\"菜单中找到的那些系统应用程序是安全的.\n" +
                   "某些\"隐藏\"应用程序在那里提供关键的系统功能" +
                   "因此卸载它们可能会导致系统不稳定或无法使用：请当心卸载.\n\n" +
                   "还请记住，在任何Windows累积更新之后，可能会重新安装任何系统应用程序.\n" +
                   "在开始之前，请确保微软应用商店不在后台安装或更新应用程序.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui) => new SystemAppsRemovalEnabler(ui);
    }

    class UWPAppRemovalEntry : MenuEntry
    {
        private readonly Configuration configuration;

        public UWPAppRemovalEntry(Configuration configuration) => this.configuration = configuration;

        public override string FullName => "卸载UWP软件";
        public override string GetExplanation()
        {
            string impactedUsers = configuration.UWPAppsRemovalMode == UWPAppRemovalMode.CurrentUser
                ? "the current user"
                : "对于所有现在和将来的用户";
            string explanation = $"以下UWP应用将被删除： {impactedUsers}:\n";
            foreach (UWPAppGroup app in configuration.UWPAppsToRemove)
                explanation += $"  {app}\n";

            if (configuration.UWPAppsRemovalMode == UWPAppRemovalMode.AllUsers)
                explanation += "\n这些应用专门使用的服务，组件和计划任务以及" +
                               "所有剩余数据也将被禁用或删除.";

            return explanation + "\n为了删除Xbox的某些组件，您需要首先使系统应用程序可移动(选项0先操作一遍).";
        }
        public override IOperation CreateNewOperation(IUserInterface ui)
            => new UWPAppRemover(configuration.UWPAppsToRemove, configuration.UWPAppsRemovalMode, ui, new ServiceRemover(ui));
    }

    class DefenderDisablingEntry : MenuEntry
    {
        public override string FullName => "禁用Windows Defender";
        public override string GetExplanation()
        {
            return "重要说明：在开始之前，请在\"病毒和威胁防护\"设置下的 " +
                   "\"Windows安全\"应用程序中禁用防篡改功能.\n" +
                   "Defender服务将被删除，其反恶意软件引擎将通过组策略禁用, " +
                   "与SmartScreen功能一起.\n\n" +
                   "如果您已经使系统应用程序可移动，则Windows安全应用程序也将被删除.\n" +
                   "请记住，任何Windows累积更新都可能会重新安装该应用程序.";
        }

        public override IOperation CreateNewOperation(IUserInterface ui)
        {
            return new DefenderDisabler(
                ui,
                new UWPAppRemover(
                    new[] { UWPAppGroup.SecurityCenter },
                    UWPAppRemovalMode.AllUsers,
                    ui, new ServiceRemover(ui)
                ),
                new ServiceRemover(ui)
            );
        }
    }

    class EdgeRemovalEntry : MenuEntry
    {
        public override string FullName => "卸载Microsoft Edge";
        public override string GetExplanation()
        {
            return "新版Edge和旧版Edge浏览器都将从系统中卸载.\n" +
                   "为了能够卸载后者（一旦卸载第一个就可以还原），需要使系统应用程序可移动.\n" +
                   "请注意，在任何Windows累积更新之后，可能会重新安装两个浏览器.\n" +
                   "在继续之前，请确保新版Edge不会自动更新.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui)
        {
            return new EdgeRemover(ui,
                new UWPAppRemover(
                    new[] { UWPAppGroup.LegacyEdge },
                    UWPAppRemovalMode.AllUsers,
                    ui, new ServiceRemover(ui)
                )
            );
        }
    }

    class OneDriveRemovalEntry : MenuEntry
    {
        public override string FullName => "卸载OneDrive";
        public override string GetExplanation()
        {
            return "OneDrive将使用组策略禁用，然后为当前用户卸载.\n" +
                   "此外，将防止新用户首次登录时安装该软件.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui) => new OneDriveRemover(ui);
    }

    class ServicesRemovalEntry : MenuEntry
    {
        private readonly Configuration configuration;

        public ServicesRemovalEntry(Configuration configuration) => this.configuration = configuration;

        public override string FullName => "删除杂项服务";
        public override string GetExplanation()
        {
            string explanation = "以以下名称开头的服务将被删除:\n";
            foreach (string service in configuration.ServicesToRemove)
                explanation += $"  {service}\n";
            return explanation + "服务将与此程序可执行文件备份在同一文件夹中.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui)
            => new ServiceRemovalOperation(configuration.ServicesToRemove, ui, new ServiceRemover(ui));
    }

    class WindowsFeaturesRemovalEntry : MenuEntry
    {
        private readonly Configuration configuration;

        public WindowsFeaturesRemovalEntry(Configuration configuration) => this.configuration = configuration;

        public override string FullName => "删除Windows功能";
        public override string GetExplanation()
        {
            string explanation = "以下按需功能将被删除:";
            foreach (string feature in configuration.WindowsFeaturesToRemove)
                explanation += $"\n  {feature}";
            return explanation;
        }
        public override IOperation CreateNewOperation(IUserInterface ui)
            => new FeaturesRemover(configuration.WindowsFeaturesToRemove, ui);
    }

    class PrivacySettingsTweakEntry : MenuEntry
    {
        public override string FullName => "调整隐私设置";
        public override string GetExplanation()
        {
            return "几个默认设置和策略将被更改，以使Windows更加尊重用户的隐私.\n" +
                   "这些变化主要包括:\n" +
                   "  - 在\"设置\"应用的\"隐私\"部分下调整各种选项 " +
                   "(禁用广告ID，应用启动跟踪等.)\n" +
                   "  - 防止将输入数据(墨迹书写/键入个性化，语音)发送给Microsoft以改善其服务\n" +
                   "  - 默认情况下，拒绝所有UWP应用访问(位置，文档，活动，帐户详细信息，诊断信息)\n" +
                   "  - 禁用语音助手的语音激活(这样它们就不能总是在听)\n" +
                   "  - 禁用敏感数据(用户活动，剪贴板，文本消息)的云同步\n" +
                   "  - 在底部搜索栏中禁用网页搜索\n\n" +
                   "尽管几乎所有这些设置都适用于所有用户，但是其中某些设置仅" +
                   "适用于当前用户和运行此过程之后创建的新用户.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui) => new PrivacySettingsTweaker(ui);
    }

    class TelemetryDisablingEntry : MenuEntry
    {
        public override string FullName => "禁用微软远程接收测试数据";
        public override string GetExplanation()
        {
            return "此过程将禁用计划的任务，服务和功能，这些任务，服务和功能负责收集和\n" +
                   "向Microsoft报告数据，包括兼容性遥测，设备普查," +
                   "客户体验改善计划和兼容性助手.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui)
            => new TelemetryDisabler(ui, new ServiceRemover(ui));
    }

    class AutoUpdatesDisablingEntry : MenuEntry
    {
        public override string FullName => "禁用自动更新";
        public override string GetExplanation()
        {
            return "Windows，商店应用和语音模型的自动更新将使用组策略禁用.\n" +
                   "至少需要Windows 10 专业版才能禁用Windows自动更新.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui) => new AutoUpdatesDisabler(ui);
    }

    class ScheduledTasksDisablingEntry : MenuEntry
    {
        private readonly Configuration configuration;

        public ScheduledTasksDisablingEntry(Configuration configuration) => this.configuration = configuration;

        public override string FullName => "禁用其他计划任务";
        public override string GetExplanation()
        {
            string explanation = "以下计划任务将被禁用:";
            foreach (string task in configuration.ScheduledTasksToDisable)
                explanation += $"\n  {task}";
            return explanation;
        }
        public override IOperation CreateNewOperation(IUserInterface ui)
            => new ScheduledTasksDisabler(configuration.ScheduledTasksToDisable, ui);
    }

    class ErrorReportingDisablingEntry : MenuEntry
    {
        public override string FullName => "禁用Windows错误报告";
        public override string GetExplanation()
        {
            return "通过编辑组策略以及删除其服务(在备份它们之后)，" +
                   "将禁用Windows错误报告.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui)
            => new ErrorReportingDisabler(ui, new ServiceRemover(ui));
    }

    class TipsAndFeedbackDisablingEntry : MenuEntry
    {
        public override string FullName => "禁用提示和反馈请求";
        public override string GetExplanation()
        {
            return "反馈通知/请求，应用程序建议，提示和Spotlight(包括动态锁定屏幕背景)" +
                   "将通过相应地设置组策略和\n禁用一些相关的计划任务来关闭\n\n" +
                   "请注意，其中某些功能仅适用于当前登录的用户和运行此过程之后创建的新用户";
        }
        public override IOperation CreateNewOperation(IUserInterface ui) => new TipsDisabler(ui);
    }

    class NewGitHubIssueEntry : MenuEntry
    {
        public override string FullName => "报告问题/建议功能";
        public override string GetExplanation()
        {
            return "现在将带您到一个网页，您可以在其中打开GitHub问题，以报告错误或提出新功能";
        }
        public override IOperation CreateNewOperation(IUserInterface ui)
            => new BrowserOpener("https://github.com/Fs00/Win10BloatRemover/issues/new");
    }

    class AboutEntry : MenuEntry
    {
        public override string FullName => "关于";
        public override string GetExplanation()
        {
            Version programVersion = GetType().Assembly.GetName().Version!;
            return $"Windows 10 修改器 {programVersion.Major}.{programVersion.Minor} " +
                   $"对于Windows版本 {programVersion.Build}\n" +
                   "由Fs00开发，由曦颜XY(Github: Vaimibao或xiyanxy)汉化\n" +
                   "官方GitHub存储库: github.com/Fs00/Win10BloatRemover\n\n" +
                   "最初基于Federico Dossena的Windows 10隐私控制管理指南: http://fdossena.com\n" +
                   "归功于其工作已用于改进该软件的所有开源项目:\n" +
                   "  - privacy.sexy网站: github.com/undergroundwires/privacy.sexy\n" +
                   "  - 瘦身Windows 10脚本: github.com/W4RH4WK/Debloat-Windows-10\n\n" +
                   "该软件根据BSD 3-Clause Clear许可证发行（继续阅读全文）.";
        }
        public override IOperation CreateNewOperation(IUserInterface ui) => new LicensePrinter(ui);
    }

    class QuitEntry : MenuEntry
    {
        private readonly RebootRecommendedFlag rebootFlag;

        public QuitEntry(RebootRecommendedFlag rebootFlag) => this.rebootFlag = rebootFlag;

        public override string FullName => "退出";
        public override bool ShouldQuit => true;
        public override string GetExplanation() => "确定退出吗?";
        public override IOperation CreateNewOperation(IUserInterface ui) => new AskForRebootOperation(ui, rebootFlag);
    }
}
