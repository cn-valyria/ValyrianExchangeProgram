using Valyria.Core.DTOs.CN;
using Valyria.Infrastructure.Repositories;

namespace Valyria.IntegrationTests;

public class DataLoaderRepositoryTests(MySqlFixture fixture) : RepositoryTestBase(fixture)
{
    private const string NationFile = "CyberNations_SE_Nation_Stats_1092026510001";
    private const string AllianceFile = "CyberNations_SE_Alliance_Stats_1092026515001";
    private const string AidFile = "CyberNations_SE_Aid_Stats_1092026520001";
    private const string WarFile = "CyberNations_SE_War_Stats_1092026525001";

    // The CN repository works against cybernations_db, the VEP one against vep_db (see the data loader's Program.cs)
    private DataLoaderRepository CnRepository => new(Fixture.CnConnectionString);
    private DataLoaderRepository VepRepository => new(Fixture.VepConnectionString);

    private static Nation Nation(int id, string name, int? allianceId = 1) => new()
    {
        NationId = id, NationName = name, RulerName = $"{name} Ruler", AllianceId = allianceId, AllianceDate = new DateTime(2020, 1, 1),
        AllianceStatus = "Member", GovernmentType = GovernmentType.Democracy, Religion = Religion.Norse, Team = Team.Aqua,
        Created = new DateTime(2019, 1, 1), Technology = 100, Infrastructure = 200, BaseLand = 300,
        WarStatus = NationalWarStatus.Peace, Resource1 = "Gold", Resource2 = "Iron", Strength = 1234.5m, Defcon = 5,
        RecentActivity = RecentActivity.ActiveInTheLast3Days,
        ConnectedResource1 = "", ConnectedResource2 = "", ConnectedResource3 = "", ConnectedResource4 = "",
        ConnectedResource5 = "", ConnectedResource6 = "", ConnectedResource7 = "", ConnectedResource8 = "",
        ConnectedResource9 = "", ConnectedResource10 = ""
    };

    private static Aid Aid(int id, DateTime date, AidStatus status = AidStatus.Pending) => new()
    {
        Id = id, SendingNationId = 100, SendingAllianceId = 1, SendingTeam = Team.Aqua, ReceivingNationId = 200,
        ReceivingAllianceId = 1, ReceivingTeam = Team.Red, Status = status, Money = 3_000_000, Date = date, Reason = "VEP"
    };

    [Fact]
    public async Task IsFileAlreadyImported_RejectsUnknownFileNames()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => CnRepository.IsFileAlreadyImported("CyberNations_SE_Spy_Stats_1"));
    }

    [Theory]
    [InlineData(NationFile)]
    [InlineData(AllianceFile)]
    [InlineData(AidFile)]
    [InlineData(WarFile)]
    public async Task IsFileAlreadyImported_IsFalse_BeforeAnyImport(string fileName)
    {
        Assert.False(await CnRepository.IsFileAlreadyImported(fileName));
    }

    [Fact]
    public async Task ImportNations_InsertsUpdatesAndRemovesNations()
    {
        Assert.Equal(2, await CnRepository.ImportNationsAsync([Nation(100, "Bobland"), Nation(200, "Sueland")], NationFile));

        Assert.True(await CnRepository.IsFileAlreadyImported(NationFile));
        Assert.False(await CnRepository.IsFileAlreadyImported("CyberNations_SE_Nation_Stats_1092026510002"));
        var stored = await Data.QueryAsync<(int, string, int, int)>(
            "select id, nation_name, government_type, religion from cybernations_db.nation order by id");
        Assert.Equal([(100, "Bobland", (int)GovernmentType.Democracy, (int)Religion.Norse), (200, "Sueland", (int)GovernmentType.Democracy, (int)Religion.Norse)], stored);

        // The next day: Bob renamed his nation and Sue deleted hers
        await CnRepository.ImportNationsAsync([Nation(100, "New Bobland")], "CyberNations_SE_Nation_Stats_10102026510001");

        Assert.Equal(["New Bobland"], await Data.QueryAsync<string>("select nation_name from cybernations_db.nation"));
    }

    [Fact]
    public async Task ImportAlliances_StoresTheAlliances()
    {
        var alliance = new Alliance
        {
            Id = 1, Name = "Valyria", Updated = new DateTime(2026, 10, 9), TotalNations = 50, ActiveNations = 45,
            PercentActive = 90, Strength = 1_000_000, AverageStrength = 20_000, Score = 12.35m, Land = 150_000,
            Infrastructure = 400_000, Technology = 75_000, War = 2, Peace = 48, Soldiers = 1, Tanks = 2, Cruise = 3,
            Nukes = 4, Aircraft = 5, Navy = 6, Anarchy = 7
        };

        Assert.Equal(1, await CnRepository.ImportAlliancesAsync([alliance], AllianceFile));

        Assert.True(await CnRepository.IsFileAlreadyImported(AllianceFile));
        Assert.Equal((1, "Valyria", 50, 7), (await Data.QueryAsync<(int, string, int, int)>(
            "select id, name, total_nations, anarchy from cybernations_db.alliance")).Single());
    }

    [Fact]
    public async Task ImportWars_StoresTheWars()
    {
        var war = new War
        {
            Id = 5000, AttackingNationId = 100, AttackingAllianceId = 1, AttackingTeam = Team.Aqua, DefendingNationId = 300,
            DefendingAllianceId = 2, DefendingTeam = Team.Black, WarStatus = WarStatus.Active,
            BeginDate = new DateTime(2026, 10, 1), EndDate = new DateTime(2026, 10, 8), Reason = "Raid",
            Destruction = 12.5m, AttackPercent = 60, DefendPercent = 40
        };

        Assert.Equal(1, await CnRepository.ImportWarsAsync([war], WarFile));

        Assert.True(await CnRepository.IsFileAlreadyImported(WarFile));
        Assert.Equal((5000, 100, 300, (int)WarStatus.Active), (await Data.QueryAsync<(int, int, int, int)>(
            "select id, attacking_nation_id, defending_nation_id, war_status from cybernations_db.war")).Single());
    }

    [Fact]
    public async Task ImportAid_StoresTheAid_AndExpiresAidOlderThanTenDays()
    {
        var today = await Data.ScalarAsync<DateTime>("select curdate()");

        Assert.Equal(2, await CnRepository.ImportAidAsync([Aid(1, today), Aid(2, today.AddDays(-11))], AidFile));

        Assert.True(await CnRepository.IsFileAlreadyImported(AidFile));
        var statuses = await Data.QueryAsync<(int, int)>("select id, status from cybernations_db.aid order by id");
        Assert.Equal([(1, (int)AidStatus.Pending), (2, (int)AidStatus.Expired)], statuses);
    }

    [Fact]
    public async Task ImportAid_InsertsLargeFilesInBatches()
    {
        var today = await Data.ScalarAsync<DateTime>("select curdate()");
        var aid = Enumerable.Range(1, 2_500).Select(id => Aid(id, today)).ToList();

        Assert.Equal(2_500, await CnRepository.ImportAidAsync(aid, AidFile));

        Assert.Equal(2_500, await Data.ScalarAsync<int>("select count(*) from cybernations_db.aid"));
    }

    [Fact]
    public async Task ImportAid_RollsBackTheStagingTable_WhenARowIsRejected()
    {
        var today = await Data.ScalarAsync<DateTime>("select curdate()");
        var badAid = Aid(2, today);
        badAid.Reason = new string('x', 100); // longer than the reason column

        await Assert.ThrowsAnyAsync<Exception>(() => CnRepository.ImportAidAsync([Aid(1, today), badAid], AidFile));

        Assert.Equal(0, await Data.ScalarAsync<int>("select count(*) from cybernations_db.todays_file_aid"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MatchDeals_MatchesOpenSlots_AndReportsWhoToNotify(bool checkInactivity)
    {
        await Data.MemberAsync(100, "Bob", 'B', discordId: 111);
        await Data.MemberAsync(200, "Sue", 'S', discordId: 222);

        Assert.Equal(0, (await VepRepository.UpdateDealStateAsync(checkInactivity)).ActivityTracked);
        var result = await VepRepository.MatchDealsAsync();

        Assert.True(result.DealsCreated > 0);
        Assert.Equal([111L, 222L], result.NewPartnerDiscordIds.Order());
        Assert.Equal(result.DealsCreated, await Data.ScalarAsync<int>("select count(*) from deal where status = 'active'"));
        Assert.Equal(result.DealsCreated, await Data.ScalarAsync<int>("select count(*) from deal_state"));
    }

    [Fact]
    public async Task UpdateDealState_PutsAccountsWithDeletedNationsOnHold_AndCancelsTheirDeals()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B', discordId: 111);
        var sue = await Data.MemberAsync(200, "Sue", 'S', discordId: 222);
        var dealId = await Data.DealAsync((await Data.SlotIdsAsync(bob))[0], (await Data.SlotIdsAsync(sue))[0]);

        // CN deleted Sue's nation for inactivity
        await Data.ExecuteAsync("delete from cybernations_db.nation where id = 200");

        var result = await VepRepository.UpdateDealStateAsync(checkInactivity: true);

        Assert.Equal(1, result.AccountsHeld);
        Assert.Equal(0, (await VepRepository.MatchDealsAsync()).DealsCreated);
        Assert.Equal([('B', bob), ('H', sue)], await Data.QueryAsync<(char, int)>("select va, id from account order by id"));
        Assert.Equal(("cancelled", "Nation deleted"), (await Data.QueryAsync<(string, string)>(
            "select status, cancellation_reason from deal where id = @dealId", new { dealId })).Single());
        Assert.Equal(0, await Data.ScalarAsync<int>(
            "select count(*) from slot where account_id = @sue and tech_sent_cycles + tech_received_cycles + cash_sent_cycles + cash_received_cycles > 0",
            new { sue }));

        // Already on hold, so the next run leaves it alone
        Assert.Equal(0, (await VepRepository.UpdateDealStateAsync(checkInactivity: true)).AccountsHeld);
    }

    [Fact]
    public async Task UpdateDealState_HoldsNoAccounts_WhenTheNationTableIsEmpty()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B');
        var sue = await Data.MemberAsync(200, "Sue", 'S');
        var dealId = await Data.DealAsync((await Data.SlotIdsAsync(bob))[0], (await Data.SlotIdsAsync(sue))[0]);

        // A failed nation import shouldn't wipe out the whole program
        await Data.ExecuteAsync("delete from cybernations_db.nation");

        var result = await VepRepository.UpdateDealStateAsync(checkInactivity: false);

        Assert.Equal(0, result.AccountsHeld);
        Assert.Equal(["B", "S"], await Data.QueryAsync<string>("select va from account order by id"));
        Assert.Equal("active", await Data.ScalarAsync<string>("select status from deal where id = @dealId", new { dealId }));
    }

    [Fact]
    public async Task MatchDeals_LeavesOutAccountsWithoutADiscordId_AndOnlyReportsNewDeals()
    {
        await Data.MemberAsync(100, "Bob", 'B', discordId: 111);
        await Data.MemberAsync(200, "Sue", 'S', discordId: 0);
        var first = await VepRepository.MatchDealsAsync();
        var dealsAfterFirstRun = await Data.ScalarAsync<int>("select count(*) from deal");

        var second = await VepRepository.MatchDealsAsync();

        Assert.Equal([111L], first.NewPartnerDiscordIds);
        Assert.Equal(dealsAfterFirstRun + second.DealsCreated, await Data.ScalarAsync<int>("select count(*) from deal"));
        if (second.DealsCreated == 0)
            Assert.Empty(second.NewPartnerDiscordIds);
    }

    [Fact]
    public async Task MatchDeals_NeverRematchesASlotPairThatAlreadyHadADeal()
    {
        var bob = await Data.MemberAsync(100, "Bob", 'B', discordId: 111);
        var sue = await Data.MemberAsync(200, "Sue", 'S', discordId: 222);
        var bobSlots = await Data.SlotIdsAsync(bob);
        var sueSlots = await Data.SlotIdsAsync(sue);

        // Their first slots were matched before, and the deal was cancelled. Those slots are
        // still each other's best candidate, which used to hit deal.unique_slot_pair
        var cancelledDealId = await Data.DealAsync(bobSlots[0], sueSlots[0], status: "cancelled");

        var result = await VepRepository.MatchDealsAsync();

        Assert.Equal(1, result.DealsCreated);
        Assert.Equal([111L, 222L], result.NewPartnerDiscordIds.Order());
        var (slot1, slot2) = (await Data.QueryAsync<(int, int)>(
            "select slot_1_id, slot_2_id from deal where id <> @cancelledDealId", new { cancelledDealId })).Single();
        Assert.NotEqual((Math.Min(bobSlots[0], sueSlots[0]), Math.Max(bobSlots[0], sueSlots[0])), (slot1, slot2));
    }
}
