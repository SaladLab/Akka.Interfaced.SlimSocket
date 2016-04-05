using System;
using System.Linq;
using Xunit;

namespace Akka.Interfaced.SlimSocket.Server.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void TestDummy()
        {
            var dummy = new TcpConnection(null);
            Assert.NotNull(dummy);
        }
    }
}
