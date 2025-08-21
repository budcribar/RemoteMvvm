using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests.GeneratorHelpersTests;

public class GetWrapperTypeTests
{
    [Fact]
    public void GetWrapperType_ReturnsInt64Value_ForLong()
    {
        Assert.Equal("Int64Value", GeneratorHelpers.GetWrapperType("long"));
    }

    [Fact]
    public void GetWrapperType_ReturnsUInt32Value_ForUInt()
    {
        Assert.Equal("UInt32Value", GeneratorHelpers.GetWrapperType("uint"));
    }

    [Fact]
    public void GetWrapperType_ReturnsUInt64Value_ForULong()
    {
        Assert.Equal("UInt64Value", GeneratorHelpers.GetWrapperType("ulong"));
    }

    [Fact]
    public void GetWrapperType_ReturnsInt32Value_ForShort()
    {
        Assert.Equal("Int32Value", GeneratorHelpers.GetWrapperType("short"));
    }

    [Fact]
    public void GetWrapperType_ReturnsStringValue_ForDecimal()
    {
        Assert.Equal("StringValue", GeneratorHelpers.GetWrapperType("decimal"));
    }

    [Fact]
    public void GetWrapperType_ReturnsFloatValue_ForHalf()
    {
        Assert.Equal("FloatValue", GeneratorHelpers.GetWrapperType("half"));
    }

    [Fact]
    public void GetWrapperType_ReturnsTimestamp_ForDateTime()
    {
        Assert.Equal("Timestamp", GeneratorHelpers.GetWrapperType("System.DateTime"));
    }

    [Fact]
    public void GetWrapperType_ReturnsUInt32Value_ForByte()
    {
        Assert.Equal("UInt32Value", GeneratorHelpers.GetWrapperType("byte"));
    }

    [Fact]
    public void GetWrapperType_ReturnsInt32Value_ForSByte()
    {
        Assert.Equal("Int32Value", GeneratorHelpers.GetWrapperType("sbyte"));
    }

    [Fact]
    public void GetWrapperType_ReturnsStringValue_ForChar()
    {
        Assert.Equal("StringValue", GeneratorHelpers.GetWrapperType("char"));
    }

    [Fact]
    public void GetWrapperType_ReturnsUInt32Value_ForUShort()
    {
        Assert.Equal("UInt32Value", GeneratorHelpers.GetWrapperType("ushort"));
    }

    [Fact]
    public void GetWrapperType_ReturnsInt64Value_ForNInt()
    {
        Assert.Equal("Int64Value", GeneratorHelpers.GetWrapperType("nint"));
    }

    [Fact]
    public void GetWrapperType_ReturnsUInt64Value_ForNUInt()
    {
        Assert.Equal("UInt64Value", GeneratorHelpers.GetWrapperType("nuint"));
    }

    [Fact]
    public void GetWrapperType_ReturnsFloatValue_ForFloat()
    {
        Assert.Equal("FloatValue", GeneratorHelpers.GetWrapperType("float"));
    }

    [Fact]
    public void GetWrapperType_ReturnsDoubleValue_ForDouble()
    {
        Assert.Equal("DoubleValue", GeneratorHelpers.GetWrapperType("double"));
    }
}
