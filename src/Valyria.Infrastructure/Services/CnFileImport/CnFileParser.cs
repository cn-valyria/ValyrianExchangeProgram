using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace Valyria.Infrastructure.Services.CnFileImport;

public class CnFileParser : ICnFileParser
{
    public async IAsyncEnumerable<T> ParseAsync<T>(Stream dataStream)
    {
        using var reader = new StreamReader(dataStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "|" });

        await foreach (var record in csv.GetRecordsAsync<T>())
            yield return record;
    }
}
