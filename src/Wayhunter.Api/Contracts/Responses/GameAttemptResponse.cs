namespace Wayhunter.Api.Contracts.Responses;

/// <summary>Public start/resume result; trusted account identifiers remain server-side.</summary>
public sealed record GameAttemptResponse(
    Guid AttemptId,
    Guid GameId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineUtc,
    int CurrentStepNumber,
    bool ResumedExistingAttempt);
