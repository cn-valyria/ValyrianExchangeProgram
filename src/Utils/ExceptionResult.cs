using System;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace api.Utils;

public class ExceptionResult : ContentResult
{
    public ExceptionResult(Exception exception, bool includeStackTrace)
    {
        StatusCode = 500;
        ContentType = "application/json";
        Content = includeStackTrace
            ? JsonSerializer.Serialize(new { error = exception.ToString() })
            : JsonSerializer.Serialize(new { error = exception.Message });
    }
}
