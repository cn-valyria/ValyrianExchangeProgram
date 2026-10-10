using System.Collections.Generic;

namespace Valyria.Core.DTOs;

public class AuthorizedAccount
{
    public int AccountId { get; set; }
    public int NationId { get; set; }
    public string RulerName { get; set; }
    /// <summary>
    /// VEP role code (account.va), e.g. 'B' for buyer
    /// </summary>
    public char Role { get; set; }
    /// <summary>
    /// Admin roles, e.g. "Super Admin"
    /// </summary>
    public IEnumerable<AccountRole> Roles { get; set; }
}

public class AccountRole
{
    public int Id { get; set; }
    public string Name { get; set; }
}