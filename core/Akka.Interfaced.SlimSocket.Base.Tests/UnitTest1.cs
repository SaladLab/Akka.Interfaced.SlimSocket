using System;
using System.Linq;
using Xunit;

namespace Akka.Interfaced.SlimSocket.Base.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void TestDummy()
        {
            var dummy = new Packet();
            Assert.NotNull(dummy);
        }
    }
}
