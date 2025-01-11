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
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 2)]
        [InlineData(4, 4)]
        [InlineData(5, 4)]
        [InlineData(6, 4)]
        [InlineData(9, 8)]
        [InlineData(20, 16)]
        public void MaxPowerOfTwo_ReturnsNearestPowerOfTwo_WhenInputIsPositive(int orignal, int expect)
        {
            UtilityHelper.MaxPowerOfTwo(orignal).Should().Be(expect);
        }

        [Fact]
        public void MaxPowerOfTwo_ThrowsArgumentException_WhenInputIsZero()
        {
            Action act = () => UtilityHelper.MaxPowerOfTwo(0);

            act.Should().Throw<ArgumentException>();
        }



        [Fact]
        public void ConvertToString_ValidDictionary_ReturnsConvertedDictionary()
        {
            // Arrange
            var source = new Dictionary<string, object>
            {
                { "Key1", 123 },
                { "Key2", true },
                { "Key3", null }
            };

            var expected = new Dictionary<string, string>
            {
                { "Key1", "123" },
                { "Key2", "True" },
                { "Key3", null }
            };

            // Act
            var result = HelperExtension.ConvertToString(source);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertToString_EmptyDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            var source = new Dictionary<string, object>();

            // Act
            var result = HelperExtension.ConvertToString(source);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ConvertToString_NullDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            IDictionary<string, object> source = null;

            // Act
            var result = HelperExtension.ConvertToString(source);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ConvertToObject_ValidDictionary_ReturnsConvertedDictionary()
        {
            // Arrange
            var source = new Dictionary<string, string>
            {
                { "Key1", "123" },
                { "Key2", "True" },
                { "Key3", null }
            };

            var expected = new Dictionary<string, object>
            {
                { "Key1", "123" },
                { "Key2", "True" },
                { "Key3", null }
            };

            // Act
            var result = HelperExtension.ConvertToObject(source);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertToObject_EmptyDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            var source = new Dictionary<string, string>();

            // Act
            var result = HelperExtension.ConvertToObject(source);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ConvertToObject_NullDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            IDictionary<string, string> source = null;

            // Act
            var result = HelperExtension.ConvertToObject(source);

            // Assert
            Assert.Empty(result);
        }
    }
}
