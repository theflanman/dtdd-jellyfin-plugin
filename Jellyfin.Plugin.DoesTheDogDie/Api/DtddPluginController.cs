using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using DoesTheDogDie;
using DoesTheDogDie.Api;
using Jellyfin.Plugin.DoesTheDogDie.Configuration;
using Jellyfin.Plugin.DoesTheDogDie.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.DoesTheDogDie.Api;

/// <summary>
/// REST API controller for the DoesTheDogDie plugin.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("Plugins/DoesTheDogDie")]
[Produces(MediaTypeNames.Application.Json)]
public class DtddPluginController : ControllerBase
{
    private readonly Func<IDtddClient> _clientFactory;
    private readonly Func<RateLimitStatus?> _budgetAccessor;
    private readonly IPluginConfigurationAccessor _configAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddPluginController"/> class.
    /// </summary>
    /// <param name="clientFactory">Supplies the current DtDD client.</param>
    /// <param name="budgetAccessor">Supplies the most recently observed budget.</param>
    /// <param name="configAccessor">Supplies the plugin configuration, for API key redaction.</param>
    public DtddPluginController(
        Func<IDtddClient> clientFactory,
        Func<RateLimitStatus?> budgetAccessor,
        IPluginConfigurationAccessor configAccessor)
    {
        _clientFactory = clientFactory;
        _budgetAccessor = budgetAccessor;
        _configAccessor = configAccessor;
    }

    /// <summary>
    /// Gets the topic categories and topics used by the configuration page.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The taxonomy.</returns>
    [HttpGet("Topics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TaxonomyResponse>> GetTopics(CancellationToken cancellationToken)
    {
        var client = _clientFactory();
        var categories = await client.GetTopicCategoriesAsync(cancellationToken).ConfigureAwait(false);
        var topics = await client.GetTopicsAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new TaxonomyResponse(categories.Value, topics.Value));
    }

    /// <summary>
    /// Tests the configured API key by making a small request.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The test result.</returns>
    [HttpPost("TestKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<KeyTestResponse>> TestKey(CancellationToken cancellationToken)
    {
        try
        {
            var client = _clientFactory();
            await client.GetTopicsAsync(cancellationToken).ConfigureAwait(false);
            return Ok(new KeyTestResponse(true, "API key accepted.", client.CurrentBudget));
        }
        catch (DtddApiException ex)
        {
            // The configured key is passed explicitly: DtDD can echo a rejected key back in its 401 body,
            // and a key that does not match the `ddd_` literal pattern would otherwise be handed straight
            // back to the caller.
            return Ok(new KeyTestResponse(false, LogSanitizer.Sanitize(ex.Message, _configAccessor.GetConfiguration()?.ApiKey), null));
        }
    }

    /// <summary>
    /// Gets the most recently observed rate-limit budget.
    /// </summary>
    /// <returns>The budget, or null if none has been observed.</returns>
    [HttpGet("Budget")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<RateLimitStatus?> GetBudget() => Ok(_budgetAccessor());
}
