using System;
using System.Linq;
using Kazi.Core;
using Xunit;

namespace Kazi.Core.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void GateEventuallyUnlocksNextCatalog()
        {
            var s = T.NewSession(1000000);
            var ev = T.Capture(s);
            for (int i = 0; i < 20 && s.State.UnlockedCatalog == 1; i++)
            {
                Assert.True(s.BuyCard("mahalle_sonsans"));
                for (int k = 0; k < 20; k++) s.ScratchTap();
                s.NextCard();
            }
            Assert.Equal(2, s.State.UnlockedCatalog);
        }
    }
}
