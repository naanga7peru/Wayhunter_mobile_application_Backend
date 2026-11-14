
namespace Wayhunter.Api.Contracts.Responses;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    string CorrelationId)
{
    public static ApiErrorResponse Create(
        string code,
        string message,
        string correlationId)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "Error code is required.",
                nameof(code));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException(
                "Error message is required.",
                nameof(message));

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(correlationId));

        return new ApiErrorResponse(
            code.Trim(),
            message.Trim(),
            correlationId.Trim());
    }
}
