using Microsoft.AspNetCore.Authorization;

namespace Valyria.Api.Auth;

/// <summary>
/// Admin role names (vep_db.role.name)
/// </summary>
public static class AdminRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string ReadOnly = "Admin (Read-only)";
    /// <summary>
    /// Named for VEP 2.5's lists, which the mass messages replaced
    /// </summary>
    public const string Lists = "Admin - Lists";

    public static readonly string[] CanViewAdminPortal = [SuperAdmin, ReadOnly];
}

/// <summary>
/// Authorization policies, matching what the UI shows each admin role (middleware/auth.global.ts and the admin pages)
/// </summary>
public static class Policies
{
    /// <summary>
    /// Admin dashboard, accounts, deals and aid activity, read-only
    /// </summary>
    public const string ViewAdminPortal = nameof(ViewAdminPortal);

    /// <summary>
    /// Creating, editing and removing accounts, slots and deals, and resolving flags
    /// </summary>
    public const string ManageProgram = nameof(ManageProgram);

    /// <summary>
    /// Granting and revoking admin roles (the Settings page)
    /// </summary>
    public const string ManageAdminAccess = nameof(ManageAdminAccess);

    public const string ViewMassMessages = nameof(ViewMassMessages);

    /// <summary>
    /// Marking mass messages as sent
    /// </summary>
    public const string SendMassMessages = nameof(SendMassMessages);

    public static AuthorizationOptions AddValyriaPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(ViewAdminPortal, policy => policy.RequireRole(AdminRoles.CanViewAdminPortal));
        options.AddPolicy(ManageProgram, policy => policy.RequireRole(AdminRoles.SuperAdmin));
        options.AddPolicy(ManageAdminAccess, policy => policy.RequireRole(AdminRoles.SuperAdmin));
        options.AddPolicy(ViewMassMessages, policy => policy.RequireRole(AdminRoles.SuperAdmin, AdminRoles.Lists, AdminRoles.ReadOnly));
        options.AddPolicy(SendMassMessages, policy => policy.RequireRole(AdminRoles.SuperAdmin, AdminRoles.Lists));
        return options;
    }
}
