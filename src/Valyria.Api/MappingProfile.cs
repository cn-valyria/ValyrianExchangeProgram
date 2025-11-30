using System;
using Mapster;
using Valyria.Core.Contracts;
using Valyria.Core.Entities;
using Valyria.Core.DTOs;

namespace Valyria.Api;

public static class MappingProfile
{
    public static void ConfigureMappings()
    {
        TypeAdapterConfig<AccountTransactionsResponse, AccountTransactionSearchResults>.NewConfig();

        TypeAdapterConfig<DataCollection<TransactionDetail>, TransactionCollection>.NewConfig()
            .Map(dest => dest.TotalCount, src => src.TotalCount)
            .Map(dest => dest.Results, src => src.Results.Select(x => x.Adapt<Transaction>()).ToList());

        TypeAdapterConfig<TransactionDetail, Transaction>.NewConfig()
            .Map(dest => dest.SentBy, src => MapToNationSimplified(src.SentByNationId, src.SentByNationName, src.SentByRulerName, src.SentByAllianceName))
            .Map(dest => dest.ReceivedBy, src => MapToNationSimplified(src.ReceivedByNationId, src.ReceivedByNationName, src.ReceivedByRulerName, src.ReceivedByAllianceName))
            .Map(dest => dest.Status, src => (Valyria.Core.Enums.AidStatus)src.Status)
            .Map(dest => dest.Code, src => MapToTransactionCode(src))
            .Map(dest => dest.EndsOn, src => src.StartsOn.HasValue ? src.StartsOn.Value.AddDays(10).Date : (DateTime?)null);

        TypeAdapterConfig<AidListDetail, AidList>.NewConfig();

        TypeAdapterConfig<ListRecipientDetail, ListRecipient>.NewConfig()
            .Map(dest => dest.Nation, src => MapToNationSimplified(src.NationId, src.NationName, src.RulerName, src.AllianceName))
            .Map(dest => dest.RecentActivity, src => (Valyria.Core.Enums.RecentActivity)src.RecentActivity);

        TypeAdapterConfig<AccountListPerformance, ListPerformance>.NewConfig()
            .Map(dest => dest.Nation, src => MapToNationSimplified(src.NationId, src.NationName, src.RulerName, src.AllianceName))
            .Map(dest => dest.DayStarted, src => DateOnly.FromDateTime(src.DayStarted));
    }

    private static NationSimplified? MapToNationSimplified(int? nationId, string nationName, string rulerName, string allianceName)
        => nationId.HasValue ? new NationSimplified(nationId.Value, nationName, rulerName, allianceName) : null;

    private static TransactionCode MapToTransactionCode(TransactionDetail transaction)
    {
        var code = transaction.Lu;

        // Simplest case is that the code is just 2 characters, which is how aid-based transactions should be
        if (code.Length == 2)
            return new TransactionCode(code[0], code[1]);

        // Throw early if the code is definitely not valid
        if (code.Length != 1)
            throw new ArgumentOutOfRangeException("Transaction code (aka lu) must be 1 or 2 characters");

        // If the code is just one character, then it means it is either the sending or receiving role, not both (or neither).
        // We should always be able to tell which it is by analyzing the credits. In short, the code would only be for the
        // sender if the credits are a credit, and vice versa (it's for the receiver if debt)
        char? sendingRole = transaction switch
        {
            // Buyers send cash
            { CashMovedCashCredit: var cc, Rate: var rate } when cc == rate && code is ("B" or "W") => code[0],

            // Sellers send tech
            { TechMovedCashCredit: var tc, Rate: var rate } when tc == rate && code is ("S" or "N" or "Q") => code[0],

            // If a buyer didn't send cash or a seller didn't send tech, then the code is not for the sending party
            _ => null
        };

        char? receivingRole = transaction switch
        {
            // Sellers receive cash
            { CashMovedCashCredit: var cc, Rate: var rate } when cc == rate && code is ("S" or "N" or "Q") => code[0],

            // Buyers receive tech
            { TechMovedCashCredit: var tc, Rate: var rate } when tc == rate && code is ("B" or "W") => code[0],

            // If a seller didn't receive cash or a buyer didn't receive tech, then the code is not for the receiving party
            _ => null
        };

        return new TransactionCode(sendingRole, receivingRole);
    }
}
