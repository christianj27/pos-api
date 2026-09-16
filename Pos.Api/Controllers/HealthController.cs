using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    /// <summary>App (release) version, sourced from &lt;Version&gt; in Pos.Api.csproj.</summary>
    private static readonly string AppVersion =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "0.0.0";

    [HttpGet]
    public IActionResult Health() =>
        Ok(new { status = "healthy", version = AppVersion, timestamp = DateTime.UtcNow });
}
