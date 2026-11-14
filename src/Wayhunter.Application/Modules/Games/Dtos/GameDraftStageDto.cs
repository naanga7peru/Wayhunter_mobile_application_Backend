
namespace Wayhunter.Application.Modules.Games.Dtos;

public sealed record GameDraftStageDto(
    int StepNumber,
    string? TextHint,
    string? PhotoMediaId
);
