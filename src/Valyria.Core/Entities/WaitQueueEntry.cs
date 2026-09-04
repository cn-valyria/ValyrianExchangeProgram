using System;

namespace Valyria.Core.Entities;

public class WaitQueueEntry
{
    public int AccountId { get; set; }
    public int SlotsNeeded { get; set; }
    public DateTime EnteredQueueOn { get; set; }
    public DateTime? NextSlotOpensOn { get; set; }
    public decimal PriorityScore { get; set; }

    public AccountSimplified Account { get; set; }
}
