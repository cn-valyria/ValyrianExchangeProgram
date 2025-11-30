using System;
using Mapster;
using Valyria.Core.DTOs.CN;
using Valyria.Infrastructure.Services.CnFileImport.Models;

namespace Valyria.Batch.DataLoader;

public static class MappingProfile
{
    public static void ConfigureMappings()
    {
        TypeAdapterConfig<CnNationRecord, Nation>.NewConfig()
            .Map(dest => dest.GovernmentType, src => ParseGovernmentType(src.GovernmentType))
            .Map(dest => dest.Religion, src => ParseReligion(src.Religion))
            .Map(dest => dest.Team, src => ParseTeam(src.Team))
            .Map(dest => dest.WarStatus, src => ParseNationalWarStatus(src.WarStatus))
            .Map(dest => dest.CruiseMissiles, src => src.Cruise)
            .Map(dest => dest.RecentActivity, src => ParseRecentActivity(src.Activity));

        TypeAdapterConfig<CnWarRecord, War>.NewConfig()
            .Map(dest => dest.Id, src => src.WarId)
            .Map(dest => dest.AttackingNationId, src => src.DeclaringId)
            .Map(dest => dest.AttackingAllianceId, src => src.DeclaringAllianceId)
            .Map(dest => dest.AttackingTeam, src => ParseTeam(src.DeclaringTeam))
            .Map(dest => dest.DefendingNationId, src => src.ReceivingId)
            .Map(dest => dest.DefendingAllianceId, src => src.ReceivingAllianceId)
            .Map(dest => dest.DefendingTeam, src => ParseTeam(src.ReceivingTeam))
            .Map(dest => dest.WarStatus, src => ParseWarStatus(src.WarStatus));

        TypeAdapterConfig<CnAidRecord, Aid>.NewConfig()
            .Map(dest => dest.Id, src => src.AidId)
            .Map(dest => dest.SendingNationId, src => src.DeclaringId)
            .Map(dest => dest.SendingAllianceId, src => src.DeclaringAllianceId)
            .Map(dest => dest.SendingTeam, src => ParseTeam(src.DeclaringTeam))
            .Map(dest => dest.ReceivingNationId, src => src.ReceivingId)
            .Map(dest => dest.ReceivingTeam, src => ParseTeam(src.ReceivingTeam))
            .Map(dest => dest.Status, src => ParseAidStatus(src.Status));

        TypeAdapterConfig<CnAllianceRecord, Alliance>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Strength, src => (int)decimal.Round(src.Strength))
            .Map(dest => dest.AverageStrength, src => (int)decimal.Round(src.AverageStrength))
            .Map(dest => dest.Score, src => decimal.Round(src.Score, 2))
            .Map(dest => dest.Infrastructure, src => (int)decimal.Round(src.Infrastructure))
            .Map(dest => dest.Technology, src => (int)decimal.Round(src.Technology))
            .Map(dest => dest.Land, src => (int)decimal.Round(src.Land));
    }

    private static GovernmentType ParseGovernmentType(string governmentType)
    {
        if (Enum.TryParse<GovernmentType>(governmentType.Replace(" ", ""), out var govType))
            return govType;

        return GovernmentType.Anarchy;
    }

    private static Religion ParseReligion(string religion)
    {
        if (Enum.TryParse<Religion>(religion.Replace(" ", ""), out var religionEnum))
            return religionEnum;

        return Religion.Mixed;
    }

    private static Team ParseTeam(string team)
    {
        if (Enum.TryParse<Team>(team, out var teamEnum))
            return teamEnum;

        return Team.None;
    }

    private static NationalWarStatus ParseNationalWarStatus(string warStatus)
    {
        return warStatus switch
        {
            "War Mode" => NationalWarStatus.War,
            "Peace Mode" => NationalWarStatus.Peace,
            _ => throw new ArgumentOutOfRangeException(nameof(warStatus), $"{warStatus} could not be parsed")
        };
    }

    private static RecentActivity ParseRecentActivity(string activity)
    {
        if (Enum.TryParse<RecentActivity>(activity.Replace(" ", ""), out var recentActivity))
            return recentActivity;

        return RecentActivity.ActiveMoreThanThreeWeeksAgo;
    }

    private static WarStatus ParseWarStatus(string warStatus)
    {
        if (Enum.TryParse<WarStatus>(warStatus, out var warStatusEnum))
            return warStatusEnum;

        return WarStatus.Expired;
    }

    private static AidStatus ParseAidStatus(string status)
    {
        if (Enum.TryParse<AidStatus>(status, out var statusEnum))
            return statusEnum;

        return AidStatus.Expired;
    }
}
