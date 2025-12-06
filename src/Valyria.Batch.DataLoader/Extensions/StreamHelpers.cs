namespace Valyria.Batch.DataLoader.Extensions;

public static class StreamHelpers
{
    public static async Task<Stream> CopyAsync(this Stream stream)
    {
        var newStream = new MemoryStream();
        await stream.CopyToAsync(newStream);
        stream.Position = 0;
        newStream.Position = 0;

        return newStream;
    }
}
