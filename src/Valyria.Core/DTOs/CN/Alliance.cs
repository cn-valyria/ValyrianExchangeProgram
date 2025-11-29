using System;

namespace Valyria.Core.DTOs.CN;

public class Alliance
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime Updated { get; set; }
    public int TotalNations { get; set; }
    public int ActiveNations { get; set; }
    public int PercentActive { get; set; }
    public int Strength { get; set; }
    public int AverageStrength { get; set; }
    public decimal Score { get; set; }
    public int Land { get; set; }
    public int Infrastructure { get; set; }
    public int Technology { get; set; }
    public int War { get; set; }
    public int Peace { get; set; }
    public int Soldiers { get; set; }
    public int Tanks { get; set; }
    public int Cruise { get; set; }
    public int Nukes { get; set; }
    public int Aircraft { get; set; }
    public int Navy { get; set; }
    public int Anarchy { get; set; }
    public DateTime UpdatedOn { get; set; }
    public string UpdatedBy { get; set; }
}
