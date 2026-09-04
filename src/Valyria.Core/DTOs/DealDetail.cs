using System;
using Valyria.Core.Entities;

namespace Valyria.Core.DTOs;

public class DealDetail
{
    public int Id { get; set; }
    public AccountSimplified Partner { get; set; }
    public DealDirection Direction { get; set; }
    public string MyRole { get; set; } // "Sender", "Receiver", "Both"
    public DateTime? LastActivityDate { get; set; }
    public int DaysSinceActivity { get; set; }
    public bool NeedsAction { get; set; }
    public string ActionNeeded { get; set; }
}
