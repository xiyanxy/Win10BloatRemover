﻿using System;
using Win10BloatRemover.Operations;
using Xunit;
using Xunit.Abstractions;

namespace Win10BloatRemover.Tests.Operations
{
    [Collection("ModifiesSystemState")]
    public class SystemAppsRemovalEnablerTests
    {
        private readonly ITestOutputHelper output;
        public SystemAppsRemovalEnablerTests(ITestOutputHelper output) => this.output = output;

        private readonly UWPAppGroup[] groupsWithSystemApps = {
            UWPAppGroup.LegacyEdge,
            UWPAppGroup.SecurityCenter,
            UWPAppGroup.Xbox
        };

        [Fact]
        public void ShouldMakeSystemAppsRemovableWithoutErrors()
        {
            var ui = new TestUserInterface(output);
            var removalEnabler = new SystemAppsRemovalEnabler(ui);
            var appRemover = new UWPAppRemover(groupsWithSystemApps, UWPAppRemovalMode.AllUsers, ui, new ServiceRemover(ui, DateTime.Now));

            removalEnabler.Run();
            appRemover.Run();

            Assert.Equal(0, ui.ErrorMessagesCount);
        }
    }
}
