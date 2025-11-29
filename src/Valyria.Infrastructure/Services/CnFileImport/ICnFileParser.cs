namespace Valyria.Infrastructure.Services.CnFileImport;

public interface ICnFileParser
{
    IAsyncEnumerable<T> ParseAsync<T>(Stream dataStream);
}
