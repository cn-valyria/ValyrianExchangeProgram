using System;

namespace Valyria.Core.DTOs;

public class DealDetail
{
    public int Id { get; set; }
    public SlotDetail MySlot { get; set; }
    public SlotDetail PartnerSlot { get; set; }
    public DateTime? LastActivityDate { get; set; }
    public int DaysSinceActivity { get; set; }
    public bool NeedsAction { get; set; }
    public string ActionNeeded { get; set; }
}
