using Win10BloatRemover.Utils;
using static Win10BloatRemover.Operations.IUserInterface;

namespace Win10BloatRemover.Operations
{
    class AskForRebootOperation : IOperation
    {
        private readonly IUserInterface ui;
        private readonly RebootRecommendedFlag rebootFlag;

        public AskForRebootOperation(IUserInterface ui, RebootRecommendedFlag rebootFlag)
        {
            this.ui = ui;
            this.rebootFlag = rebootFlag;
        }

        public void Run()
        {
            if (rebootFlag.IsRebootRecommended)
            {
                ui.PrintWarning("您已执行一项或多项操作，需要重新启动系统才能完全生效.");
                var choice = ui.AskUserConsent("现在可以重启吗?");
                if (choice == UserChoice.Yes)
                    SystemUtils.RebootSystem();
            }
        }
    }
}
