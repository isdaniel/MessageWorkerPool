using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.RabbitMq;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using MessageWorkerPool.Utilities;

namespace MessageWorkerPool.Test.Utility
{
    public class UtilityExtensionTest
    {
        [Theory]
        [InlineData(1,1)]
        [InlineData(2,2)]
        [InlineData(3,2)]
        [InlineData(4,4)]
        [InlineData(5,4)]
        [InlineData(6,4)]
        [InlineData(9,8)]
        [InlineData(20, 16)]
        public void MaxPowerOfTwo_ReturnsNearestPowerOfTwo_WhenInputIsPositive(int orignal,int expect)
        {
            UtilityHelper.MaxPowerOfTwo(orignal).Should().Be(expect);
        }

        [Fact]
        public void MaxPowerOfTwo_ThrowsArgumentException_WhenInputIsZero()
        {
            Action act = () => UtilityHelper.MaxPowerOfTwo(0);

            act.Should().Throw<ArgumentException>();
        }


    }
}
