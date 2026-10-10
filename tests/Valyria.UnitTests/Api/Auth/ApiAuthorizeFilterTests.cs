using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Valyria.Api.Auth;

namespace Valyria.UnitTests.Api.Auth;

public class ApiAuthorizeFilterTests
{
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();

    private AuthorizationFilterContext Authorize(string? authorizationHeader)
    {
        var httpContext = new DefaultHttpContext();
        if (authorizationHeader != null)
            httpContext.Request.Headers.Authorization = authorizationHeader;

        var context = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());

        new ApiAuthorizeFilter(_tokenProvider).OnAuthorization(context);
        return context;
    }

    [Fact]
    public void Attribute_ResolvesTheFilterFromServices()
    {
        Assert.Equal(typeof(ApiAuthorizeFilter), new ApiAuthorizeAttribute().ServiceType);
    }

    [Fact]
    public void MissingHeader_IsUnauthorized()
    {
        Assert.IsType<UnauthorizedObjectResult>(Authorize(null).Result);
        _tokenProvider.DidNotReceive().ValidateToken(Arg.Any<string>());
    }

    [Fact]
    public void InvalidToken_IsUnauthorized()
    {
        _tokenProvider.ValidateToken("bad").Returns(false);

        Assert.IsType<UnauthorizedObjectResult>(Authorize("Bearer bad").Result);
    }

    [Theory]
    [InlineData("Bearer good")]
    [InlineData("good")]
    public void ValidToken_WithOrWithoutBearerPrefix_IsAllowedThrough(string header)
    {
        _tokenProvider.ValidateToken("good").Returns(true);

        Assert.Null(Authorize(header).Result);
    }
}
