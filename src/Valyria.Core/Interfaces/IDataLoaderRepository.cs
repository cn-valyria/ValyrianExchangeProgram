namespace Valyria.Core.Interfaces;

public interface IDataLoaderRepository
{
    Task<int> LoadTodaysTransactionsAsync();
    Task LoadTodaysListsAsync();
}
