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
    /// Get a page of all active deals, optionally filtered by nation, ruler, or alliance name (admin view)
    /// </summary>
    Task<AdminDealPage> GetActiveDealsAsync(string? search, int limit, int offset);

    /// <summary>
    /// Search all aid sent or received by VEP accounts, with whether each aid matched a deal (admin view)
    /// </summary>
    Task<AidActivityPage> SearchAidActivityAsync(AidActivityFilters filters, int limit, int offset);

    /// <summary>
    /// Get all accounts in the wait queue (admin view)
    /// </summary>
    Task<List<WaitQueueEntry>> GetWaitQueueAsync();

    /// <summary>
    /// Get all flagged deals (admin view)
    /// </summary>
    Task<List<DealFlagDetail>> GetFlaggedDealsAsync();

    /// <summary>
    /// The aid behind a flag: every leg of its deal, and what was in the flagged nation's aid slots (admin view).
    /// Returns null if the flag does not exist.
    /// </summary>
    Task<DealFlagEvidence> GetDealFlagEvidenceAsync(int flagId);

    /// <summary>
    /// Resolve a deal flag (admin action)
    /// </summary>
    Task ResolveDealFlagAsync(int flagId, string resolvedBy, string adminNotes);

    /// <summary>
    /// Put a deal cancelled by this flag back into effect with a fresh state, and resolve the deal's open flags (admin action)
    /// </summary>
    Task<DealRestoreResult> RestoreFlaggedDealAsync(int flagId, string restoredBy, string? adminNotes);

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
    // Set by the controller from the signed-in user, never by the client
    public string? CreatedBy { get; set; }
}

public class DealFlagDetail
{
    public int Id { get; set; }
    public int DealId { get; set; }
    public DealFlagType FlagType { get; set; }
    /// <summary>
    /// Null for flags raised before V10
    /// </summary>
    public DealFlagReason? FlagReason { get; set; }
    public AccountSimplified FlaggedAccount { get; set; }
    public AccountSimplified InnocentAccount { get; set; }
    public System.DateTime FlaggedOn { get; set; }
    public System.DateTime? ResolvedOn { get; set; }
    public string ResolvedBy { get; set; }
    public string AdminNotes { get; set; }

    public string FlaggedAccountRole { get; set; }
    public string InnocentAccountRole { get; set; }
    /// <summary>
    /// Open flags against the flagged account, this one included. It isn't matched until they're all resolved
    /// </summary>
    public int FlaggedAccountOpenFlagCount { get; set; }
    /// <summary>
    /// Each side's slot in the deal, with its account
    /// </summary>
    public SlotDetail FlaggedSlot { get; set; }
    public SlotDetail InnocentSlot { get; set; }

    public DealStatus DealStatus { get; set; }
    public System.DateTime DealCreatedOn { get; set; }
    public System.DateTime? DealCancelledOn { get; set; }
    public string? DealCancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>
    /// The deal's current state. Null once the deal is cancelled, or until the data loader picks up a restored deal
    /// </summary>
    public DealActivityState? State { get; set; }
    public DealBlockedReason? BlockedReason { get; set; }
    public System.DateTime? StateSince { get; set; }
    public int? NextSenderAccountId { get; set; }
    public int? ResponsibleAccountId { get; set; }
    public int? DaysResponsible { get; set; }
    public System.DateTime? CancelsOn { get; set; }
    public System.DateTime? InFlightUntil { get; set; }
    /// <summary>
    /// When out-of-sequence aid blocking the deal leaves the pair's slots
    /// </summary>
    public System.DateTime? BlockedUntil { get; set; }

    /// <summary>
    /// Legs that haven't failed, including any in flight
    /// </summary>
    public int LegsCompleted { get; set; }
    /// <summary>
    /// When the flagged account last sent a leg that hasn't failed
    /// </summary>
    public System.DateTime? FlaggedLastSentOn { get; set; }

    /// <summary>
    /// Other active deals holding either slot, or between the same two accounts. Any of these, or either
    /// account being on hold, stops the deal being restored
    /// </summary>
    public int? FlaggedSlotOtherDealId { get; set; }
    public int? InnocentSlotOtherDealId { get; set; }
    public int? PairOtherDealId { get; set; }
    public bool FlaggedAccountOnHold { get; set; }
    public bool InnocentAccountOnHold { get; set; }

    /// <summary>
    /// Whether the two slots' cycles are still exact inverses. A role change since the deal was cancelled resets them
    /// </summary>
    public bool SlotsStillMatch =>
        FlaggedSlot != null && InnocentSlot != null
        && FlaggedSlot.TechSentCycles == InnocentSlot.TechReceivedCycles
        && FlaggedSlot.TechReceivedCycles == InnocentSlot.TechSentCycles
        && FlaggedSlot.CashSentCycles == InnocentSlot.CashReceivedCycles
        && FlaggedSlot.CashReceivedCycles == InnocentSlot.CashSentCycles;

    /// <summary>
    /// Whether this flag cancelled its deal, and the deal can be put back into effect
    /// </summary>
    public bool CanRestoreDeal =>
        DealStatus == DealStatus.Cancelled
        && FlagType != DealFlagType.OneDayInactive
        && DealCancelledBy == "system"
        && FlaggedSlotOtherDealId == null
        && InnocentSlotOtherDealId == null
        && PairOtherDealId == null
        && !FlaggedAccountOnHold
        && !InnocentAccountOnHold
        && SlotsStillMatch;
}

public enum DealRestoreResult
{
    Restored,
    /// <summary>The flag doesn't exist or is already resolved</summary>
    FlagNotFound,
    /// <summary>The flag didn't cancel its deal, or the deal's slots or accounts are no longer free</summary>
    NotRestorable
}
