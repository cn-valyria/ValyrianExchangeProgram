namespace Valyria.Core.DTOs;

/// <summary>
/// Outcome of the data loader's daily deal processing run
/// </summary>
/// <param name="ActivityTracked">Aid records newly linked to deals</param>
/// <param name="AccountsHeld">Accounts put on hold because their nation was deleted</param>
/// <param name="DealsCreated">Deals created by match_deals_run</param>
/// <param name="NewPartnerDiscordIds">Discord IDs of accounts that are part of a newly created deal</param>
public record DealProcessingResult(int ActivityTracked, int AccountsHeld, int DealsCreated, List<long> NewPartnerDiscordIds);
