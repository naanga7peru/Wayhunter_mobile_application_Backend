
using System;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Domain.Modules.Games.Entities;

public sealed class GameProgress
{
    public Guid AttemptId { get; }
    public int TotalSteps { get; }
    public int CurrentStepNumber { get; private set; }
    public int RejectedEvidenceCount { get; private set; }

    public GameProgressStatus Status { get; private set; }

    public GameProgress(Guid attemptId, int totalSteps)
    {
        if (attemptId == Guid.Empty)
            throw new ArgumentException("Attempt ID is required.");

        if (totalSteps <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(totalSteps),
                "Game must have at least one step.");

        AttemptId = attemptId;
        TotalSteps = totalSteps;
        CurrentStepNumber = 1;
        RejectedEvidenceCount = 0;
        Status = GameProgressStatus.NotStarted;
    }

    public void Start()
    {
        if (Status != GameProgressStatus.NotStarted)
            throw new InvalidOperationException(
                "Game attempt already started.");

        Status = GameProgressStatus.InProgress;
    }

    public void SubmitEvidence(int stepNumber)
    {
        if (Status != GameProgressStatus.InProgress)
            throw new InvalidOperationException(
                "Attempt is not ready for evidence.");

        if (stepNumber != CurrentStepNumber)
            throw new InvalidOperationException(
                "Cannot skip or repeat game steps.");

        Status = GameProgressStatus.WaitingForVerification;
    }

    // Called only after authorized verification.
    public void ApproveEvidence()
    {
        if (Status != GameProgressStatus.WaitingForVerification)
            throw new InvalidOperationException(
                "No evidence is waiting for verification.");

        if (CurrentStepNumber == TotalSteps)
        {
            Status = GameProgressStatus.Completed;
            return;
        }

        CurrentStepNumber++;
        Status = GameProgressStatus.InProgress;
    }

    public void RejectEvidence()
    {
        if (Status != GameProgressStatus.WaitingForVerification)
            throw new InvalidOperationException(
                "No evidence is waiting for verification.");

        RejectedEvidenceCount++;

        if (RejectedEvidenceCount >= 3)
        {
            Status = GameProgressStatus.Rejected;
            return;
        }

        Status = GameProgressStatus.InProgress;
    }
}
