using System;
using Mapster;
using Valyria.Core.DTOs;
using Valyria.Core.Entities;

namespace Valyria.Api;

public static class MappingProfile
{
    public static void ConfigureMappings()
    {
        TypeAdapterConfig<AidListDetail, AidList>.NewConfig();

        TypeAdapterConfig<ListRecipientDetail, ListRecipient>.NewConfig()
            .Map(dest => dest.Nation, src => MapToNationSimplified(src.NationId, src.NationName, src.RulerName, src.AllianceName))
            .Map(dest => dest.RecentActivity, src => (Valyria.Core.Enums.RecentActivity)src.RecentActivity);

        TypeAdapterConfig<SimplifiedAccount, AccountSimplified>.NewConfig();
    }

    private static NationSimplified? MapToNationSimplified(int? nationId, string nationName, string rulerName, string allianceName)
        => nationId.HasValue ? new NationSimplified(nationId.Value, nationName, rulerName, allianceName) : null;
}
