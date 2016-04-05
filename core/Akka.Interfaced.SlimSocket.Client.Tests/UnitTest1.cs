using System;
using System.Linq;
using Xunit;

namespace Akka.Interfaced.SlimSocket.Client.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void TestDummy()
        {
            var dummy = new TcpConnection(null, null);
            Assert.NotNull(dummy);
        }
    }
}
