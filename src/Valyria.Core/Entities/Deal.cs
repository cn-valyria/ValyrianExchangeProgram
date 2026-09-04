using System;

namespace Valyria.Core.Entities;

public class Deal
{
    public int Id { get; set; }
    public int Account1Id { get; set; }
    public int Account2Id { get; set; }
    public DealDirection Direction { get; set; }
    public DealStatus Status { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? CancelledOn { get; set; }
    public string CancelledBy { get; set; }
    public string CancellationReason { get; set; }

    public AccountSimplified Account1 { get; set; }
    public AccountSimplified Account2 { get; set; }
}
