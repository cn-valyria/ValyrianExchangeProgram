using System;

namespace Valyria.Core.DTOs.CN;

public class War
{
    public int Id { get; set; }
    public int AttackingNationId { get; set; }
    public int AttackingAllianceId { get; set; }
    public Team AttackingTeam { get; set; }
    public int DefendingNationId { get; set; }
    public int DefendingAllianceId { get; set; }
    public Team DefendingTeam { get; set; }
    public WarStatus WarStatus { get; set; }
    public DateTime BeginDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; }
    public decimal Destruction { get; set; }
    public int AttackPercent { get; set; }
    public int DefendPercent { get; set; }
    public DateTime UpdatedOn { get; set; }
    public string UpdatedBy { get; set; }

}
