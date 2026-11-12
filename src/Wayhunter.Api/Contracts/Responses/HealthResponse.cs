namespace Wayhunter.Api.Contracts.Responses;

public sealed record HealthResponse(string Service, string Status, DateTimeOffset TimestampUtc);
