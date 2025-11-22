using AutoMapper;
using Valyria.Core.Contracts;
using Valyria.Core.Entities;
using Valyria.Core.DTOs;

namespace Valyria.Api;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<AccountTransactionsResponse, AccountTransactionSearchResults>();
        CreateMap<DataCollection<TransactionDetail>, TransactionCollection>()
            .ForMember(dest => dest.TotalCount, opt => opt.MapFrom(src => src.TotalCount))
            .ForMember(dest => dest.Results, opt => opt.MapFrom((src, dest, i, context) => src.Results.Select(context.Mapper.Map<Transaction>)));

        CreateMap<TransactionDetail, Transaction>()
            .ForMember(dest => dest.SentBy, opt => opt.MapFrom(src => MapToNationSimplified(src.SentByNationId, src.SentByNationName, src.SentByRulerName, src.SentByAllianceName)))
            .ForMember(dest => dest.ReceivedBy, opt => opt.MapFrom(src => MapToNationSimplified(src.ReceivedByNationId, src.ReceivedByNationName, src.ReceivedByRulerName, src.ReceivedByAllianceName)))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (Valyria.Core.Enums.AidStatus) src.Status))
            .ForMember(dest => dest.Code, opt => opt.MapFrom(src => MapToTransactionCode(src)))
            .ForMember(dest => dest.EndsOn, opt => opt.MapFrom(src => src.StartsOn.HasValue ? src.StartsOn.Value.AddDays(10).Date : (DateTime?) null));

        CreateMap<AidListDetail, AidList>();
        CreateMap<ListRecipientDetail, ListRecipient>()
            .ForMember(dest => dest.Nation, opt => opt.MapFrom(src => MapToNationSimplified(src.NationId, src.NationName, src.RulerName, src.AllianceName)))
            .ForMember(dest => dest.RecentActivity, opt => opt.MapFrom(src => (Valyria.Core.Enums.RecentActivity) src.RecentActivity));
    }

    private NationSimplified MapToNationSimplified(int? nationId, string nationName, string rulerName, string allianceName)
        => nationId.HasValue ? new NationSimplified(nationId.Value, nationName, rulerName, allianceName) : null;

    private TransactionCode MapToTransactionCode(TransactionDetail transaction)
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