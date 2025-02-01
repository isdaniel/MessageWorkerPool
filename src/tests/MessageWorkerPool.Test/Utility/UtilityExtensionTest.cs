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
            var result = HelperExtension.ConvertToStringMap(source);

            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void ConvertToString_EmptyDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            var source = new Dictionary<string, object>();

            // Act
            var result = HelperExtension.ConvertToStringMap(source);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ConvertToString_NullDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            IDictionary<string, object> source = null;

            // Act
            var result = HelperExtension.ConvertToStringMap(source);

            // Assert
            result.Should().BeEmpty();
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
            var result = HelperExtension.ConvertToObjectMap(source);

            // Assert
            result.Should().BeEquivalentTo(result);
        }

        [Fact]
        public void ConvertToObject_EmptyDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            var source = new Dictionary<string, string>();

            // Act
            var result = HelperExtension.ConvertToObjectMap(source);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ConvertToObject_NullDictionary_ReturnsEmptyDictionary()
        {
            // Arrange
            IDictionary<string, string> source = null;

            // Act
            var result = HelperExtension.ConvertToObjectMap(source);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void TryGetValueOrDefault_ShouldReturnValue_WhenKeyExists()
        {
            var dictionary = new Dictionary<string, int> { { "key", 42 } };
            var result = dictionary.TryGetValueOrDefault("key");
            result.Should().Be(42);
        }

        [Fact]
        public void TryGetValueOrDefault_ShouldReturnDefault_WhenKeyDoesNotExist_String()
        {
            var dictionary = new Dictionary<string, string>();
            var result = dictionary.TryGetValueOrDefault("missingKey");
            result.Should().Be(default);
        }

        [Fact]
        public void TryGetValueOrDefault_ShouldReturnDefault_WhenKeyDoesNotExist_Int()
        {
            var dictionary = new Dictionary<string, int>();
            var result = dictionary.TryGetValueOrDefault("missingKey");
            result.Should().Be(default);
        }

        [Fact]
        public void TryGetValueOrDefault_ShouldThrowArgumentNullException_WhenKeyIsNullOrEmpty()
        {
            var dictionary = new Dictionary<string, int>();
            Assert.Throws<ArgumentNullException>(() => dictionary.TryGetValueOrDefault(null));
            Assert.Throws<ArgumentNullException>(() => dictionary.TryGetValueOrDefault(""));
        }

    }
}
