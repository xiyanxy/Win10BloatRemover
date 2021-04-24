﻿using System;
using System.Linq;
using Win10BloatRemover.Operations;
using Win10BloatRemover.Utils;

namespace Win10BloatRemover
{
    class ConsoleMenu
    {
        private bool exitRequested = false;
        private readonly MenuEntry[] entries;
        private readonly RebootRecommendedFlag rebootFlag;

        private static readonly Version programVersion = typeof(ConsoleMenu).Assembly.GetName().Version!;

        public ConsoleMenu(MenuEntry[] entries, RebootRecommendedFlag rebootFlag)
        {
            this.entries = entries;
            this.rebootFlag = rebootFlag;
        }

        public void RunLoopUntilExitRequested()
        {
            while (!exitRequested)
            {
                Console.Clear();
                PrintHeading();
                PrintMenuEntries();
                MenuEntry chosenEntry = RequestUserChoice();

                Console.Clear();
                PrintTitleAndExplanation(chosenEntry);
                if (UserWantsToProceed())
                    TryPerformEntryOperation(chosenEntry);
            }
        }

        private void PrintHeading()
        {
            Console.WriteLine("┌────────────────────────────────────────────┐");
            Console.WriteLine("│        Windows 10 修改器_曦颜XY汉化        │");
            Console.WriteLine($"│                 版本: {programVersion.Major}.{programVersion.Minor}                  │");
            Console.WriteLine("└────────────────────────────────────────────┘");
            Console.WriteLine();
        }

        private void PrintMenuEntries()
        {
            ConsoleHelpers.WriteLine("-- 菜单 --", ConsoleColor.Green);
            for (int i = 0; i < entries.Length; i++)
            {
                ConsoleHelpers.Write($"{i}: ", ConsoleColor.Green);
                Console.WriteLine(entries[i].FullName);
            }
            Console.WriteLine();
        }

        private MenuEntry RequestUserChoice()
        {
            MenuEntry? chosenEntry = null;
            bool isUserInputCorrect = false;
            while (!isUserInputCorrect)
            {
                Console.Write("选项: ");
                chosenEntry = GetEntryCorrespondingToUserInput(Console.ReadLine());
                if (chosenEntry == null)
                    ConsoleHelpers.WriteLine("输入错误. 必须是有效的菜单项序号。", ConsoleColor.Red);
                else
                    isUserInputCorrect = true;
            }
            return chosenEntry!;
        }

        private MenuEntry? GetEntryCorrespondingToUserInput(string userInput)
        {
            bool inputIsNumeric = int.TryParse(userInput, out int entryIndex);
            if (inputIsNumeric)
                return entries.ElementAtOrDefault(entryIndex);

            return null;
        }

        private void PrintTitleAndExplanation(MenuEntry entry)
        {
            ConsoleHelpers.WriteLine($"-- {entry.FullName} --", ConsoleColor.Green);
            Console.WriteLine(entry.GetExplanation());
        }

        private bool UserWantsToProceed()
        {
            Console.WriteLine("\n按Enter键继续，或按其他键返回菜单.");
            return Console.ReadKey().Key == ConsoleKey.Enter;
        }

        private void TryPerformEntryOperation(MenuEntry entry)
        {
            try
            {
                Console.WriteLine();
                IOperation operation = entry.CreateNewOperation(new ConsoleUserInterface());
                operation.Run();
                if (operation.IsRebootRecommended)
                {
                    ConsoleHelpers.WriteLine("\n建议系统重启一下.", ConsoleColor.Cyan);
                    rebootFlag.SetRebootRecommended();
                }

                if (entry.ShouldQuit)
                {
                    exitRequested = true;
                    return;
                }

                Console.Write("\n完成! ");
            }
            catch (Exception exc)
            {
                ConsoleHelpers.WriteLine($"修改失败: {exc.Message}", ConsoleColor.Red);
                #if DEBUG
                ConsoleHelpers.WriteLine(exc.StackTrace, ConsoleColor.Red);
                #endif
                Console.WriteLine();
            }

            ConsoleHelpers.FlushStandardInput();
            Console.WriteLine("按任意键返回主菜单");
            Console.ReadKey();
        }
    }
}
