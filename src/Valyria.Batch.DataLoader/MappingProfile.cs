using System;
using AutoMapper;
using Valyria.Core.DTOs.CN;
using Valyria.Infrastructure.Services.CnFileImport.Models;

namespace Valyria.Batch.DataLoader;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CnNationRecord, Nation>()
            .ForMember(dest => dest.GovernmentType, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<GovernmentType>(src.GovernmentType.Replace(" ", ""), out var govType))
                    return govType;

                return GovernmentType.Anarchy;
            }))
            .ForMember(dest => dest.Religion, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<Religion>(src.Religion.Replace(" ", ""), out var religion))
                    return religion;

                return Religion.Mixed;
            }))
            .ForMember(dest => dest.Team, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<Team>(src.Team, out var team))
                    return team;

                return Team.None;
            }))
            .ForMember(dest => dest.WarStatus, opt => opt.MapFrom((src, dest) =>
            {
                return src.WarStatus switch
                {
                    "War Mode" => NationalWarStatus.War,
                    "Peace Mode" => NationalWarStatus.Peace,
                    _ => throw new ArgumentOutOfRangeException(nameof(src.WarStatus), $"{src.WarStatus} could not be parsed")
                };
            }))
            .ForMember(dest => dest.CruiseMissiles, opt => opt.MapFrom(src => src.Cruise))
            .ForMember(dest => dest.RecentActivity, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<RecentActivity>(src.Activity.Replace(" ", ""), out var recentActivity))
                    return recentActivity;

                return RecentActivity.ActiveMoreThanThreeWeeksAgo;
            }));

        CreateMap<CnWarRecord, War>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.WarId))
            .ForMember(dest => dest.AttackingNationId, opt => opt.MapFrom(src => src.DeclaringId))
            .ForMember(dest => dest.AttackingAllianceId, opt => opt.MapFrom(src => src.DeclaringAllianceId))
            .ForMember(dest => dest.AttackingTeam, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<Team>(src.DeclaringTeam, out var team))
                    return team;

                return Team.None;
            }))
            .ForMember(dest => dest.DefendingNationId, opt => opt.MapFrom(src => src.ReceivingId))
            .ForMember(dest => dest.DefendingAllianceId, opt => opt.MapFrom(src => src.ReceivingAllianceId))
            .ForMember(dest => dest.DefendingTeam, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<Team>(src.ReceivingTeam, out var team))
                    return team;

                return Team.None;
            }))
            .ForMember(dest => dest.WarStatus, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<WarStatus>(src.WarStatus, out var warStatus))
                    return warStatus;

                return WarStatus.Expired;
            }));

        CreateMap<CnAidRecord, Aid>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.AidId))
            .ForMember(dest => dest.SendingNationId, opt => opt.MapFrom(src => src.DeclaringId))
            .ForMember(dest => dest.SendingAllianceId, opt => opt.MapFrom(src => src.DeclaringAllianceId))
            .ForMember(dest => dest.SendingTeam, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<Team>(src.DeclaringTeam, out var team))
                    return team;

                return Team.None;
            }))
            .ForMember(dest => dest.ReceivingNationId, opt => opt.MapFrom(src => src.ReceivingId))
            .ForMember(dest => dest.ReceivingTeam, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<Team>(src.ReceivingTeam, out var team))
                    return team;

                return Team.None;
            }))
            .ForMember(dest => dest.Status, opt => opt.MapFrom((src, dest) =>
            {
                if (Enum.TryParse<AidStatus>(src.Status, out var status))
                    return status;

                return AidStatus.Expired;
            }));

        CreateMap<CnAllianceRecord, Alliance>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Strength, opt => opt.MapFrom(src => (int)decimal.Round(src.Strength)))
            .ForMember(dest => dest.AverageStrength, opt => opt.MapFrom(src => (int)decimal.Round(src.AverageStrength)))
            .ForMember(dest => dest.Score, opt => opt.MapFrom(src => decimal.Round(src.Score, 2)))
            .ForMember(dest => dest.Infrastructure, opt => opt.MapFrom(src => (int)decimal.Round(src.Infrastructure)))
            .ForMember(dest => dest.Technology, opt => opt.MapFrom(src => (int)decimal.Round(src.Technology)))
            .ForMember(dest => dest.Land, opt => opt.MapFrom(src => (int)decimal.Round(src.Land)));
    }
}
