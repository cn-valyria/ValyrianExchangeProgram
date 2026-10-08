using System.Collections.Generic;
using System.Threading.Tasks;
using Valyria.Core.DTOs;
using Valyria.Core.Entities;

namespace Valyria.Core.Interfaces;

public interface IDealsRepository
{
    /// <summary>
    /// Get all active deals for a specific account
    /// </summary>
    Task<AccountDealsSummary> GetAccountDealsAsync(int accountId);

    /// <summary>
    /// Get a page of every deal (active and cancelled) an account has been part of, newest first
    /// </summary>
    Task<DealHistoryPage> GetAccountDealHistoryAsync(int accountId, int limit, int offset);

    /// <summary>
    /// Get the aid activity for a deal from one participant's perspective.
    /// Returns null if the account is not part of the deal.
    /// </summary>
    Task<List<DealActivityDetail>> GetDealActivityAsync(int dealId, int accountId);

    /// <summary>
    /// Get details for a specific deal by ID
    /// </summary>
    Task<Deal> GetDealByIdAsync(int dealId);

    /// <summary>
    /// Create a new deal (manual or auto-assigned)
    /// </summary>
    Task<int> CreateDealAsync(CreateDealRequest request);

    /// <summary>
    /// Cancel an existing deal
    /// </summary>
    Task CancelDealAsync(int dealId, string reason, string cancelledBy);

    /// <summary>
    /// Get all accounts in the wait queue (admin view)
    /// </summary>
    Task<List<WaitQueueEntry>> GetWaitQueueAsync();

    /// <summary>
    /// Get all flagged deals (admin view)
    /// </summary>
    Task<List<DealFlagDetail>> GetFlaggedDealsAsync();

    /// <summary>
    /// Resolve a deal flag (admin action)
    /// </summary>
    Task ResolveDealFlagAsync(int flagId, string resolvedBy, string adminNotes);

    /// <summary>
    /// Get all slots for a specific account (admin view)
    /// </summary>
    Task<AccountSlotsResponse> GetAccountSlotsAsync(int accountId);

    /// <summary>
    /// Update the cycle values of a slot (admin action). Returns false if the slot does not exist.
    /// </summary>
    Task<bool> UpdateSlotAsync(SlotUpdateRequest request, string updatedBy);

    /// <summary>
    /// Get all slots for a specific account that are not part of an active deal
    /// </summary>
    Task<List<SlotDetail>> GetAvailableSlotsAsync(int accountId);
}

public class CreateDealRequest
{
    public int Slot1Id { get; set; }
    public int Slot2Id { get; set; }
    public string CreatedBy { get; set; }
}

public class DealFlagDetail
{
    public int Id { get; set; }
    public int DealId { get; set; }
    public DealFlagType FlagType { get; set; }
    public AccountSimplified FlaggedAccount { get; set; }
    public AccountSimplified InnocentAccount { get; set; }
    public System.DateTime FlaggedOn { get; set; }
    public System.DateTime? ResolvedOn { get; set; }
    public string ResolvedBy { get; set; }
    public string AdminNotes { get; set; }
}
