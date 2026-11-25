using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace MoissonMontreal.Desktop.Services;

/// <summary>
/// Adds the signed-in user's access token to every request sent to the API.
/// </summary>
public sealed class AuthorizationHeaderHandler : DelegatingHandler
{
    /// <summary>
    /// Provides the access token.
    /// </summary>
    private readonly IAuthService _authService;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="authService">Service that provides the signed-in user's access token.</param>
    public AuthorizationHeaderHandler(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Adds the <c>Authorization: Bearer</c> header, then sends the request to the API.
    /// </summary>
    /// <param name="request">The request about to be sent.</param>
    /// <param name="cancellationToken">Lets the caller stop the request early.</param>
    /// <returns>The API's response.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await _authService.GetAccessTokenAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or MsalException)
        {
            // No valid token (nobody signed in, or the session expired): the request is sent without one.
            // The API answers 401 on protected routes, and /health keeps working since it needs no token.
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
