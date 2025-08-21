using System.Globalization;
using RemoteMvvmTool.Generators;
using Xunit;

namespace RemoteMvvmTool.Tests.GeneratorHelpersTests;

public class ToSnakeTests
{
    [Fact]
    public void ToSnake_ConvertsConsecutiveCaps()
    {
        Assert.Equal("http_server", GeneratorHelpers.ToSnake("HTTPServer"));
    }

    [Fact]
    public void ToSnake_UsesInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal("indigo", GeneratorHelpers.ToSnake("Indigo"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ToSnake_ExistingUnderscorePreserved()
    {
        Assert.Equal("already_snake", GeneratorHelpers.ToSnake("Already_Snake"));
    }
}
