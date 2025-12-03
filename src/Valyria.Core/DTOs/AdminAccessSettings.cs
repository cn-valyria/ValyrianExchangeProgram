using System;

namespace Valyria.Core.DTOs;

public class AdminAccessSetting
{
    public int AccountId { get; set; }
    public string RulerName { get; set; }
    public string NationName { get; set; }
    public char VepRole { get; set; }
    public int AdminRoleId { get; set; }
    public string AdminRoleName { get; set; }
}

public class AdminAccessCreateRequest
{
    public int AccountId { get; set; }
    public int AdminRoleId { get; set; }
}

public class AdminAccessDeleteRequest
{
    public int AccountId { get; set; }
    public int AdminRoleId { get; set; }
}
