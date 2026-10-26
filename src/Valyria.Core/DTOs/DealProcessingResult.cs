namespace Valyria.Core.DTOs;

/// <summary>
/// Outcome of the data loader's deal state update, which runs before matching
/// </summary>
/// <param name="ActivityTracked">Aid records newly linked to deals</param>
/// <param name="AccountsHeld">Accounts put on hold because their nation was deleted</param>
/// <param name="TeamBonusDealsCancelled">Deals cancelled because the team bonus slot they used ended</param>
public record DealStateResult(int ActivityTracked, int AccountsHeld, int TeamBonusDealsCancelled);

/// <summary>
/// Outcome of the data loader's deal matching run
/// </summary>
/// <param name="DealsCreated">Deals created by match_deals_run</param>
/// <param name="NewPartnerDiscordIds">Discord IDs of accounts that are part of a newly created deal</param>
public record DealMatchingResult(int DealsCreated, List<long> NewPartnerDiscordIds);
