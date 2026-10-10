using Mapster;
using Valyria.Core.DTOs.CN;
using Valyria.Infrastructure.Services.CnFileImport.Models;

namespace Valyria.UnitTests.DataLoader;

public class MappingProfileTests
{
    public MappingProfileTests() => Mappings.EnsureConfigured();

    private static CnNationRecord NationRecord(
        string governmentType = "Democracy", string religion = "None", string team = "Aqua",
        string warStatus = "Peace Mode", string activity = "Active This Week") => new()
    {
        NationId = 100, RulerName = "Bob", NationName = "Buyland", AllianceId = "1",
        GovernmentType = governmentType, Religion = religion, Team = team, WarStatus = warStatus,
        Activity = activity, Cruise = 7, Strength = 1234.5m
    };

    [Theory]
    [InlineData("Federal Government", GovernmentType.FederalGovernment)]
    [InlineData("Revolutionary Gov", GovernmentType.RevolutionaryGov)]
    [InlineData("Something New", GovernmentType.Anarchy)]
    public void Nation_ParsesGovernmentType_FallingBackToAnarchy(string value, GovernmentType expected)
    {
        Assert.Equal(expected, NationRecord(governmentType: value).Adapt<Nation>().GovernmentType);
    }

    [Theory]
    [InlineData("Bahai Faith", Religion.BahaiFaith)]
    [InlineData("Norse", Religion.Norse)]
    [InlineData("Pastafarian", Religion.Mixed)]
    public void Nation_ParsesReligion_FallingBackToMixed(string value, Religion expected)
    {
        Assert.Equal(expected, NationRecord(religion: value).Adapt<Nation>().Religion);
    }

    [Theory]
    [InlineData("Maroon", Team.Maroon)]
    [InlineData("Teal", Team.None)]
    public void Nation_ParsesTeam_FallingBackToNone(string value, Team expected)
    {
        Assert.Equal(expected, NationRecord(team: value).Adapt<Nation>().Team);
    }

    [Theory]
    [InlineData("War Mode", NationalWarStatus.War)]
    [InlineData("Peace Mode", NationalWarStatus.Peace)]
    public void Nation_ParsesWarStatus(string value, NationalWarStatus expected)
    {
        Assert.Equal(expected, NationRecord(warStatus: value).Adapt<Nation>().WarStatus);
    }

    [Fact]
    public void Nation_RejectsUnknownWarStatus()
    {
        var exception = Assert.ThrowsAny<Exception>(() => NationRecord(warStatus: "Chill Mode").Adapt<Nation>());

        Assert.Contains("Chill Mode could not be parsed", exception.ToString());
    }

    [Theory]
    [InlineData("Active In The Last 3 Days", RecentActivity.ActiveInTheLast3Days)]
    [InlineData("Active Three Weeks Ago", RecentActivity.ActiveThreeWeeksAgo)]
    [InlineData("Unknown", RecentActivity.ActiveMoreThanThreeWeeksAgo)]
    public void Nation_ParsesRecentActivity_FallingBackToLeastActive(string value, RecentActivity expected)
    {
        Assert.Equal(expected, NationRecord(activity: value).Adapt<Nation>().RecentActivity);
    }

    [Fact]
    public void Nation_CopiesMatchingFields_AndMapsCruiseToCruiseMissiles()
    {
        var nation = NationRecord().Adapt<Nation>();

        Assert.Equal(100, nation.NationId);
        Assert.Equal("Bob", nation.RulerName);
        Assert.Equal(1, nation.AllianceId);
        Assert.Equal(7, nation.CruiseMissiles);
        Assert.Equal(1234.5m, nation.Strength);
    }

    [Theory]
    [InlineData("Active", WarStatus.Active)]
    [InlineData("Peace", WarStatus.Peace)]
    [InlineData("Whatever", WarStatus.Expired)]
    public void War_MapsSidesAndStatus(string status, WarStatus expected)
    {
        var record = new CnWarRecord
        {
            WarId = 5000, DeclaringId = 100, DeclaringAllianceId = 1, DeclaringTeam = "Aqua",
            ReceivingId = 300, ReceivingAllianceId = 2, ReceivingTeam = "Nope", WarStatus = status, Destruction = 1.5m
        };

        var war = record.Adapt<War>();

        Assert.Equal(5000, war.Id);
        Assert.Equal(100, war.AttackingNationId);
        Assert.Equal(1, war.AttackingAllianceId);
        Assert.Equal(Team.Aqua, war.AttackingTeam);
        Assert.Equal(300, war.DefendingNationId);
        Assert.Equal(2, war.DefendingAllianceId);
        Assert.Equal(Team.None, war.DefendingTeam);
        Assert.Equal(expected, war.WarStatus);
        Assert.Equal(1.5m, war.Destruction);
    }

    [Theory]
    [InlineData("Approved", AidStatus.Approved)]
    [InlineData("Pending", AidStatus.Pending)]
    [InlineData("Cancled", AidStatus.Cancled)]
    [InlineData("Gone", AidStatus.Expired)]
    public void Aid_MapsSidesAndStatus(string status, AidStatus expected)
    {
        var record = new CnAidRecord
        {
            AidId = 900, DeclaringId = 100, DeclaringAllianceId = 1, DeclaringTeam = "Aqua",
            ReceivingId = 200, ReceivingAllianceId = 3, ReceivingTeam = "Red", Status = status, Money = 3_000_000
        };

        var aid = record.Adapt<Aid>();

        Assert.Equal(900, aid.Id);
        Assert.Equal(100, aid.SendingNationId);
        Assert.Equal(1, aid.SendingAllianceId);
        Assert.Equal(Team.Aqua, aid.SendingTeam);
        Assert.Equal(200, aid.ReceivingNationId);
        Assert.Equal(3, aid.ReceivingAllianceId);
        Assert.Equal(Team.Red, aid.ReceivingTeam);
        Assert.Equal(expected, aid.Status);
        Assert.Equal(3_000_000, aid.Money);
    }

    [Fact]
    public void Alliance_RoundsStatsToWholeNumbers_AndScoreToTwoDecimals()
    {
        var record = new CnAllianceRecord
        {
            Id = 1, Name = "Valyria", Strength = 1234.6m, AverageStrength = 99.4m, Score = 12.3456m,
            Infrastructure = 400000.6m, Technology = 75000.4m, Land = 150000.7m
        };

        var alliance = record.Adapt<Alliance>();

        Assert.Equal(1, alliance.Id);
        Assert.Equal("Valyria", alliance.Name);
        Assert.Equal(1235, alliance.Strength);
        Assert.Equal(99, alliance.AverageStrength);
        Assert.Equal(12.35m, alliance.Score);
        Assert.Equal(400001, alliance.Infrastructure);
        Assert.Equal(75000, alliance.Technology);
        Assert.Equal(150001, alliance.Land);
    }
}
