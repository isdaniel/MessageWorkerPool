using Microsoft.Extensions.DependencyInjection;
using MessageWorkerPool.Extensions;
using MessageWorkerPool.RabbitMQ;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using MessageWorkerPool.Utilities;
using System.Text;

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
        public void ConvertToStringMap_WithByteArray_ShouldConvertToUtf8String()
        {
            // Arrange
            var testString = "Hello World";
            var byteArray = Encoding.UTF8.GetBytes(testString);
            var source = new Dictionary<string, object>
            {
                { "ByteKey", byteArray }
            };

            // Act
            var result = HelperExtension.ConvertToStringMap(source);

            // Assert
            result.Should().ContainKey("ByteKey");
            result["ByteKey"].Should().Be(testString);
        }

        [Fact]
        public void ConvertToStringMap_WithMixedTypes_ShouldConvertAll()
        {
            // Arrange
            var byteArray = Encoding.UTF8.GetBytes("ByteValue");
            var source = new Dictionary<string, object>
            {
                { "String", "StringValue" },
                { "Integer", 42 },
                { "Double", 3.14 },
                { "Boolean", true },
                { "Bytes", byteArray },
                { "Null", null }
            };

            // Act
            var result = HelperExtension.ConvertToStringMap(source);

            // Assert
            result.Should().HaveCount(6);
            result["String"].Should().Be("StringValue");
            result["Integer"].Should().Be("42");
            result["Double"].Should().Be("3.14");
            result["Boolean"].Should().Be("True");
            result["Bytes"].Should().Be("ByteValue");
            result["Null"].Should().BeNull();
        }

        [Fact]
        public void ConvertToStringMap_WithEmptyByteArray_ShouldReturnEmptyString()
        {
            // Arrange
            var source = new Dictionary<string, object>
            {
                { "EmptyBytes", new byte[0] }
            };

            // Act
            var result = HelperExtension.ConvertToStringMap(source);

            // Assert
            result["EmptyBytes"].Should().Be("");
        }

        [Fact]
        public void ConvertToStringMap_WithSpecialCharactersInByteArray_ShouldPreserveEncoding()
        {
            // Arrange
            var specialString = "こんにちは世界"; // Japanese characters
            var byteArray = Encoding.UTF8.GetBytes(specialString);
            var source = new Dictionary<string, object>
            {
                { "Special", byteArray }
            };

            // Act
            var result = HelperExtension.ConvertToStringMap(source);

            // Assert
            result["Special"].Should().Be(specialString);
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
        public void ConvertToObjectMap_WithMultipleEntries_ShouldConvertAll()
        {
            // Arrange
            var source = new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" },
                { "Key3", "Value3" }
            };

            // Act
            var result = HelperExtension.ConvertToObjectMap(source);

            // Assert
            result.Should().HaveCount(3);
            result["Key1"].Should().Be("Value1");
            result["Key2"].Should().Be("Value2");
            result["Key3"].Should().Be("Value3");
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

        [Fact]
        public void TryGetValueOrDefault_WithComplexType_ShouldReturnValue()
        {
            // Arrange
            var complexObject = new { Name = "Test", Value = 123 };
            var dictionary = new Dictionary<string, object> { { "complex", complexObject } };

            // Act
            var result = dictionary.TryGetValueOrDefault("complex");

            // Assert
            result.Should().Be(complexObject);
        }

        [Fact]
        public void TryGetValueOrDefault_WithNullValue_ShouldReturnNull()
        {
            // Arrange
            var dictionary = new Dictionary<string, string> { { "nullKey", null } };

            // Act
            var result = dictionary.TryGetValueOrDefault("nullKey");

            // Assert
            result.Should().BeNull();
        }
    }
}
