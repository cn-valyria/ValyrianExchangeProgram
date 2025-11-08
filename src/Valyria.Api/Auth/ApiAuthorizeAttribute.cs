using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Valyria.Api.Auth;

public class ApiAuthorizeAttribute : ServiceFilterAttribute
{
    public ApiAuthorizeAttribute() : base(typeof(ApiAuthorizeFilter))
    {
    }
}

public class ApiAuthorizeFilter : IAuthorizationFilter
{
    private readonly ITokenProvider _tokenProvider;

    public ApiAuthorizeFilter(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();
        var token = authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader;

        if (string.IsNullOrEmpty(token) || !_tokenProvider.ValidateToken(token))
        {
            context.Result = new UnauthorizedObjectResult("Authentication failed. Please provide a valid authentication header.");
        }
    }
}
