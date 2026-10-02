using System.Net;
using FreightLink.Api.Common.Domain;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Entities.Enums;
using Xunit;

namespace FreightLink.Api.Tests.Unit;

/// <summary>Unit tests for <see cref="AgencyStatusTransitionRules"/> (issue #56).</summary>
public class AgencyStatusTransitionRulesTests
{
    [Theory]
    [InlineData(AgencyStatus.Pending, AgencyStatus.Verified)]
    [InlineData(AgencyStatus.Pending, AgencyStatus.Suspended)]
    [InlineData(AgencyStatus.Verified, AgencyStatus.Active)]
    [InlineData(AgencyStatus.Verified, AgencyStatus.Suspended)]
    [InlineData(AgencyStatus.Active, AgencyStatus.Suspended)]
    [InlineData(AgencyStatus.Suspended, AgencyStatus.Active)]
    [InlineData(AgencyStatus.Suspended, AgencyStatus.Verified)]
    [InlineData(AgencyStatus.Suspended, AgencyStatus.Pending)]
    public void ValidateTransition_AllowedTransition_DoesNotThrow(AgencyStatus from, AgencyStatus to)
    {
        Assert.True(AgencyStatusTransitionRules.CanTransition(from, to));
        AgencyStatusTransitionRules.ValidateTransition(from, to);
    }

    [Theory]
    [InlineData(AgencyStatus.Pending, AgencyStatus.Active)]
    [InlineData(AgencyStatus.Verified, AgencyStatus.Pending)]
    [InlineData(AgencyStatus.Active, AgencyStatus.Verified)]
    [InlineData(AgencyStatus.Active, AgencyStatus.Pending)]
    public void ValidateTransition_DisallowedTransition_ThrowsBadRequest(AgencyStatus from, AgencyStatus to)
    {
        Assert.False(AgencyStatusTransitionRules.CanTransition(from, to));

        var ex = Assert.Throws<ApiException>(() => AgencyStatusTransitionRules.ValidateTransition(from, to));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, ex.Code);
    }

    [Theory]
    [InlineData(AgencyStatus.Pending)]
    [InlineData(AgencyStatus.Verified)]
    [InlineData(AgencyStatus.Active)]
    [InlineData(AgencyStatus.Suspended)]
    public void ValidateTransition_SameStatus_ThrowsBadRequest(AgencyStatus status)
    {
        var ex = Assert.Throws<ApiException>(() => AgencyStatusTransitionRules.ValidateTransition(status, status));

        Assert.Equal(ErrorCode.INVALID_AGENCY_STATUS_TRANSITION, ex.Code);
    }

    [Fact]
    public void NoStatus_IsTerminal_SoSuspendedAgenciesCanAlwaysBeRestored()
    {
        foreach (var status in Enum.GetValues<AgencyStatus>())
        {
            Assert.NotEmpty(AgencyStatusTransitionRules.GetAllowedNextStatuses(status));
        }
    }
}
