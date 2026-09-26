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
/// Invoice management endpoints: manual creation, line items drafting, issuing, voiding, and role-based retrieval.
/// </summary>
[ApiController]
[Route("api/invoices")]
[Route("api/v1/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private const string AgentRoles = nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Agent);
    private const string ShipperRoles = nameof(UserRole.Shipper);
    private const string DeliveryEventRoles = nameof(UserRole.AgencyStaff) + "," + nameof(UserRole.Agent);

    private readonly IInvoiceService _invoiceService;

    /// <summary>Initializes a new instance of <see cref="InvoicesController"/>.</summary>
    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    /// <summary>Creates a new invoice in 'Draft' or 'Issued' status with manual line items (Agent only).</summary>
    /// <param name="request">Invoice creation details including line items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created with the created invoice.</returns>
    [HttpPost]
    [Authorize(Roles = AgentRoles)]
    public async Task<ActionResult<InvoiceResponseDto>> Create([FromBody] CreateInvoiceDto request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.CreateAsync(GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Lists candidate recipients for invoice creation (Agent only).</summary>
    [HttpGet("recipients")]
    [Authorize(Roles = AgentRoles)]
    public async Task<ActionResult<List<InvoiceRecipientDto>>> GetRecipients(CancellationToken cancellationToken)
    {
        var result = await _invoiceService.GetRecipientsAsync(GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Legacy/idempotent delivery hook: returns existing invoice if one exists for the trip, or creates one if not.
    /// </summary>
    /// <param name="tripId">The delivered trip's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>201 Created or 200 OK with the invoice details.</returns>
    [HttpPost("on-delivery/{tripId:guid}")]
    [HttpPost("delivery-event/{tripId:guid}")]
    [Authorize(Roles = DeliveryEventRoles)]
    public async Task<ActionResult<InvoiceResponseDto>> CreateOnDelivery(Guid tripId, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.CreateOnTripDeliveredAsync(tripId, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Fetches a single invoice by its ID with line items and audit trail.</summary>
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

    /// <summary>Updates editable fields on a draft invoice. Rejects modifications if status is Issued or Paid (Agent only).</summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="request">The updated invoice values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the updated invoice.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AgentRoles)]
    public async Task<ActionResult<InvoiceResponseDto>> Update(Guid id, [FromBody] UpdateInvoiceDto request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.UpdateAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Transitions status from 'Draft' to 'Issued', finalizing totals and locking edits (Agent only).</summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the issued invoice.</returns>
    [HttpPost("{id:guid}/issue")]
    [Authorize(Roles = AgentRoles)]
    public async Task<ActionResult<InvoiceResponseDto>> Issue(Guid id, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.IssueAsync(id, GetCurrentUserId(), GetCurrentUserRole(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Settles and pays an issued invoice (Shipper only).</summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="request">Optional payment parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the paid invoice.</returns>
    [HttpPost("{id:guid}/pay")]
    [Authorize(Roles = ShipperRoles)]
    public async Task<ActionResult<InvoiceResponseDto>> Pay(Guid id, [FromBody] PayInvoiceDto? request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.PayAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Transitions an invoice's status according to allowed domain state rules (Agent only).</summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="request">The target status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the transitioned invoice.</returns>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = AgentRoles)]
    public async Task<ActionResult<InvoiceResponseDto>> UpdateStatus(Guid id, [FromBody] UpdateInvoiceStatusDto request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.UpdateStatusAsync(id, GetCurrentUserId(), GetCurrentUserRole(), request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Voids/cancels an invoice. Strictly requires a non-empty voidReason string (Agent only).
    /// Supports DELETE and POST /void, PATCH /void for client compatibility.
    /// </summary>
    /// <param name="id">The invoice's ID.</param>
    /// <param name="request">Void request with mandatory reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>200 OK with the voided invoice.</returns>
    [HttpDelete("{id:guid}")]
    [HttpPost("{id:guid}/void")]
    [HttpPatch("{id:guid}/void")]
    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Roles = AgentRoles)]
    public async Task<ActionResult<InvoiceResponseDto>> Void(Guid id, [FromBody] VoidInvoiceDto? request, CancellationToken cancellationToken)
    {
        var reason = request?.VoidReason;
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "A non-empty void reason is required.");
        }

        var result = await _invoiceService.VoidAsync(id, GetCurrentUserId(), GetCurrentUserRole(), reason, cancellationToken);
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
