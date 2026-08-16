using System.Net;
using System.Security.Claims;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.DTOs.Invoices;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreightLink.Api.Controllers;

/// <summary>
/// Invoice management endpoints (Component D): creation, single/list retrieval, update, status transition, and cancellation.
/// </summary>
[ApiController]
[Route("api/v1/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private const string InvoiceCreationRoles = nameof(UserRole.Shipper) + "," + nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Admin);

    private readonly IInvoiceService _invoiceService;

    /// <summary>Initializes a new instance of <see cref="InvoicesController"/>.</summary>
    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    /// <summary>Creates a new invoice for a trip.</summary>
    /// <param name="request">Invoice details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created with the created invoice.</returns>
    [HttpPost]
    [Authorize(Roles = InvoiceCreationRoles)]
    public async Task<ActionResult<InvoiceResponseDto>> Create([FromBody] CreateInvoiceDto request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.CreateAsync(GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Fetches a single invoice by its ID.</summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the invoice details.</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.GetByIdAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Searches, filters, and paginates invoices based on user role and query parameters.</summary>
    /// <param name="query">Filter and pagination query options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with a paged list of invoice summaries.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedInvoiceResponseDto>> GetList([FromQuery] InvoiceListQueryDto query, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.GetListAsync(query, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates a draft invoice's amount, currency, or due date.</summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="request">The updated invoice values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the updated invoice.</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvoiceResponseDto>> Update(Guid id, [FromBody] UpdateInvoiceDto request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.UpdateAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Transitions an invoice's status according to allowed domain state rules.</summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="request">The target status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the transitioned invoice.</returns>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<InvoiceResponseDto>> UpdateStatus(Guid id, [FromBody] UpdateInvoiceStatusDto request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.UpdateStatusAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Voids/cancels an invoice (moves status to Void).</summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the voided invoice.</returns>
    [HttpPatch("{id:guid}/void")]
    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult<InvoiceResponseDto>> Void(Guid id, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.VoidAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid user id.");
        }

        return userId;
    }

    private UserRole GetCurrentUserRole()
    {
        var roleClaim = User.FindFirstValue(ClaimTypes.Role);
        if (!Enum.TryParse<UserRole>(roleClaim, out var role) || !Enum.IsDefined(role))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, ErrorCode.UNAUTHORIZED, "The access token does not contain a valid role.");
        }

        return role;
    }
}
