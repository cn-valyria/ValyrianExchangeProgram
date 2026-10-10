using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Valyria.UnitTests;

internal static class TestHelpers
{
    /// <summary>
    /// Gives a controller an HttpContext, signed in with the given claims (anonymous if none)
    /// </summary>
    public static T WithUser<T>(this T controller, params Claim[] claims) where T : ControllerBase
    {
        var identity = claims.Length == 0 ? new ClaimsIdentity() : new ClaimsIdentity(claims, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    public static T AssertOk<T>(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    /// <summary>
    /// Asserts the controller's catch-all 500 response, and that it carries the inner exception's message
    /// </summary>
    public static void AssertServerError(IActionResult result, string expectedDetails)
    {
        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.Equal(expectedDetails, Prop(error.Value, "details"));
    }

    /// <summary>
    /// Reads a property off the anonymous objects controllers return as error bodies
    /// </summary>
    public static object? Prop(object? value, string name) =>
        value?.GetType().GetProperty(name)?.GetValue(value);
}
