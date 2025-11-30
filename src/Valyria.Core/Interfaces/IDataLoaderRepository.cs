using Valyria.Core.DTOs.CN;

namespace Valyria.Core.Interfaces;

public interface IDataLoaderRepository
{
    // CN file import methods
    Task<bool> IsFileAlreadyImported(string fileName);
    Task<int> ImportAlliancesAsync(IReadOnlyCollection<Alliance> data, string fileName);
    Task<int> ImportNationsAsync(IReadOnlyCollection<Nation> data, string fileName);
    Task<int> ImportAidAsync(IReadOnlyCollection<Aid> data, string fileName);
    Task<int> ImportWarsAsync(IReadOnlyCollection<War> data, string fileName);

    // VEP data loader methods
    Task<int> LoadTodaysTransactionsAsync();
    Task LoadTodaysListsAsync();
    Task RemoveAccountsWithFullSlotsFromListsAsync();
}
