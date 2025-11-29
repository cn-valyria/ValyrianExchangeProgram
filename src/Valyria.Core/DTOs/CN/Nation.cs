namespace Valyria.Core.DTOs.CN;

public class Nation
{
    public int NationId { get; set; }
    public string RulerName { get; set; }
    public string NationName { get; set; }
    public int? AllianceId { get; set; }
    public DateTime? AllianceDate { get; set; }
    public string AllianceStatus { get; set; }
    public GovernmentType GovernmentType { get; set; }
    public Religion Religion { get; set; }
    public Team Team { get; set; }
    public DateTime Created { get; set; }
    public decimal Technology { get; set; }
    public decimal Infrastructure { get; set; }
    public decimal BaseLand { get; set; }
    public NationalWarStatus WarStatus { get; set; }
    public string Resource1 { get; set; }
    public string Resource2 { get; set; }
    public int Votes { get; set; }
    public decimal Strength { get; set; }
    public int Defcon { get; set; }
    public int BaseSoldiers { get; set; }
    public int Tanks { get; set; }
    public int CruiseMissiles { get; set; }
    public int Nukes { get; set; }
    public RecentActivity RecentActivity { get; set; }
    public string ConnectedResource1 { get; set; }
    public string ConnectedResource2 { get; set; }
    public string ConnectedResource3 { get; set; }
    public string ConnectedResource4 { get; set; }
    public string ConnectedResource5 { get; set; }
    public string ConnectedResource6 { get; set; }
    public string ConnectedResource7 { get; set; }
    public string ConnectedResource8 { get; set; }
    public string ConnectedResource9 { get; set; }
    public string ConnectedResource10 { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string UpdatedBy { get; set; }
}
