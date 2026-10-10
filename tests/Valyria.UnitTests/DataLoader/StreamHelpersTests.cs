extern alias DataLoader;

using System.Text;
using DataLoader::Valyria.Batch.DataLoader.Extensions;

namespace Valyria.UnitTests.DataLoader;

public class StreamHelpersTests
{
    [Fact]
    public async Task CopyAsync_ReturnsARewoundCopy_AndRewindsTheOriginal()
    {
        var original = new MemoryStream(Encoding.UTF8.GetBytes("nation|data"));

        var copy = await original.CopyAsync();

        Assert.NotSame(original, copy);
        Assert.Equal(0, original.Position);
        Assert.Equal(0, copy.Position);
        Assert.Equal("nation|data", await new StreamReader(copy).ReadToEndAsync());
        Assert.Equal("nation|data", await new StreamReader(original).ReadToEndAsync());
    }
}
