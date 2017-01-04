using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace PollyPlayground
{
    public class PollyShould
    {
        [Fact]
        public void execute_empty_test()
        {
            true.Should().BeTrue();
        }
    }
}
