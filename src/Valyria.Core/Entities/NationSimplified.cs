namespace Valyria.Core.Entities;

public class NationSimplified
{
    public int NationId { get; set; }
    public string NationName { get; set; }
    public string RulerName { get; set; }
    public string AllianceName { get; set; }

    public NationSimplified() { }

    public NationSimplified(int nationId, string nationName, string rulerName, string alliance)
    {
        NationId = nationId;
        NationName = nationName;
        RulerName = rulerName;
        AllianceName = alliance;
    }
}

public class AccountSimplified : NationSimplified
{
    public int AccountId { get; set; }
}