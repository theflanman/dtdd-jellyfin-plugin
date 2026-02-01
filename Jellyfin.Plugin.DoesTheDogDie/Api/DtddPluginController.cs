using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.DoesTheDogDie.Api;

/// <summary>
/// REST API controller for the DoesTheDogDie plugin.
/// </summary>
[ApiController]
[Route("Plugins/DoesTheDogDie")]
[Produces(MediaTypeNames.Application.Json)]
public class DtddPluginController : ControllerBase
{
    private readonly TriggerCacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddPluginController"/> class.
    /// </summary>
    /// <param name="cacheService">The trigger cache service.</param>
    public DtddPluginController(TriggerCacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <summary>
    /// Gets the cached trigger categories and topics.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The trigger cache.</returns>
    [HttpGet("Topics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TriggerCache>> GetTopics(CancellationToken cancellationToken)
    {
        var cache = await _cacheService.GetOrRefreshCacheAsync(forceRefresh: false, cancellationToken)
            .ConfigureAwait(false);
        return Ok(cache);
    }

    /// <summary>
    /// Refreshes the trigger cache from the DTDD API.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The refreshed trigger cache.</returns>
    [HttpPost("Topics/Refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TriggerCache>> RefreshTopics(CancellationToken cancellationToken)
    {
        var cache = await _cacheService.RefreshCacheAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(cache);
    }
}
