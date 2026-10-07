using System;
using Valyria.Core.DTOs;

namespace Valyria.Core.Entities;

public class Deal
{
    public int Id { get; set; }
    public int Slot1Id { get; set; }
    public int Slot2Id { get; set; }
    public DealStatus Status { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? CancelledOn { get; set; }
    public string CancelledBy { get; set; }
    public string CancellationReason { get; set; }

    public SlotDetail Slot1 { get; set; }
    public SlotDetail Slot2 { get; set; }
}
