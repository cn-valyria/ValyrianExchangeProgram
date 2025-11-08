using System.Collections.Generic;

namespace Valyria.Core.DTOs;

public class AuthorizedAccount
{
    public int AccountId { get; set; }
    public IEnumerable<AccountRole> Roles { get; set; }
}

public class AccountRole
{
    public int Id { get; set; }
    public string Name { get; set; }
}