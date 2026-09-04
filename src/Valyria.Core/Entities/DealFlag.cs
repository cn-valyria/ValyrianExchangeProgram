using System;

namespace Valyria.Core.Entities;

public class DealFlag
{
    public int Id { get; set; }
    public int DealId { get; set; }
    public DealFlagType FlagType { get; set; }
    public int FlaggedAccountId { get; set; }
    public int InnocentAccountId { get; set; }
    public DateTime FlaggedOn { get; set; }
    public DateTime? ResolvedOn { get; set; }
    public string ResolvedBy { get; set; }
    public string AdminNotes { get; set; }

    public AccountSimplified FlaggedAccount { get; set; }
    public AccountSimplified InnocentAccount { get; set; }
}
