using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;

namespace Valyria.Core.Interfaces;

public interface IListsRepository
{
    Task<List<AidListDetail>> GetAidListsAsync();

    Task<List<ListRecipientDetail>> GetListRecipientsForAccountAsync(int accountId);
}