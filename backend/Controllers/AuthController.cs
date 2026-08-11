using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Auth;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Authentication endpoints: registration, shared login, refresh-token rotation, logout, and the
/// current-user lookup. Deliberately thin — every action just delegates to <see cref="IAuthService"/>.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    /// <summary>Creates the controller with its injected auth service.</summary>
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Registers a new Shipper (self-service, public).</summary>
    /// <param name="request">Shipper registration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with a success message and the new user's id/email.</returns>
    [HttpPost("register/shipper")]
    public async Task<ActionResult<RegisterResponseDto>> RegisterShipper([FromBody] RegisterShipperRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterShipperAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Registers a new Agency org + its first Agency Staff user, atomically (self-service, public).</summary>
    /// <param name="request">Agency registration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 with a success message and the new user's id/email.</returns>
    [HttpPost("register/agency")]
    public async Task<ActionResult<RegisterResponseDto>> RegisterAgency([FromBody] RegisterAgencyRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAgencyAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Shared login for every role. Returns only the token pair, never the user profile.</summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with <see cref="TokenResponseDto"/>.</returns>
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, Request.Headers.UserAgent.ToString(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Exchanges a refresh token for a new access/refresh pair, revoking the old one (rotation).</summary>
    /// <param name="request">The raw refresh token to rotate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with a new <see cref="TokenResponseDto"/>.</returns>
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponseDto>> Refresh([FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(request, Request.Headers.UserAgent.ToString(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Revokes a refresh token belonging to the authenticated caller.</summary>
    /// <param name="request">The raw refresh token to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(GetCurrentUserId(), request, cancellationToken);
        return NoContent();
    }

    /// <summary>Returns the authenticated caller's safe profile, read from the access token.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 with <see cref="CurrentUserResponseDto"/>.</returns>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponseDto>> Me(CancellationToken cancellationToken)
    {
        var result = await _authService.GetCurrentUserAsync(GetCurrentUserId(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Extracts the authenticated user's id from the <c>NameIdentifier</c> claim on the access token.</summary>
    /// <returns>The caller's user id.</returns>
    /// <exception cref="ApiException">
    /// 401 if the claim is absent or not a well-formed GUID. Not currently reachable with tokens
    /// minted by <see cref="Services.TokenService.GenerateAccessToken"/> (which always sets this
    /// claim to a valid GUID) — this guards against a future JWT validation/claim-shape change or a
    /// differently-configured issuer being accepted, so that case fails as a clean 401 instead of
    /// an unhandled exception surfacing as a generic 500.
    /// </exception>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid user id.");
        }

        return userId;
    }
}
