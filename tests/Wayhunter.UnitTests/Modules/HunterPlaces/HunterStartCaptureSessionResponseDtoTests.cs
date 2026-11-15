
using System;
using System.Text.Json;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterStartCaptureSessionResponseDtoTests
{
    [Fact]
    public void ResponseDto_ShouldStoreSessionAndSubmissionIds()
    {
        var sessionId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var response = new HunterStartCaptureSessionResponseDto
        {
            CaptureSessionId = sessionId,
            HunterSubmissionId = submissionId
        };

        Assert.Equal(sessionId, response.CaptureSessionId);
        Assert.Equal(submissionId, response.HunterSubmissionId);
    }

    [Fact]
    public void ResponseDto_ShouldSupportJsonRoundTrip()
    {
        var response = new HunterStartCaptureSessionResponseDto
        {
            CaptureSessionId = Guid.NewGuid(),
            HunterSubmissionId = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(response);

        var result =
            JsonSerializer.Deserialize<HunterStartCaptureSessionResponseDto>(
                json);

        Assert.NotNull(result);

        Assert.Equal(
            response.CaptureSessionId,
            result.CaptureSessionId);

        Assert.Equal(
            response.HunterSubmissionId,
            result.HunterSubmissionId);
    }
}
