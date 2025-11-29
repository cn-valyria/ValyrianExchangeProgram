using System;

namespace Valyria.Core.DTOs.CN;

public class Aid
{
    public int Id { get; set; }
    public int SendingNationId { get; set; }
    public int SendingAllianceId { get; set; }
    public Team SendingTeam { get; set; }
    public int ReceivingNationId { get; set; }
    public int ReceivingAllianceId { get; set; }
    public Team ReceivingTeam { get; set; }
    public AidStatus Status { get; set; }
    public int Money { get; set; }
    public int Technology { get; set; }
    public int Soldiers { get; set; }
    public DateTime Date { get; set; }
    public string Reason { get; set; }
    public DateTime UpdatedOn { get; set; }
    public string UpdatedBy { get; set; }
}
