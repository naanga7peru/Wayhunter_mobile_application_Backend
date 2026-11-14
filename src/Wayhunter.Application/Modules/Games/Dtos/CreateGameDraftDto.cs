
using System.Collections.Generic;

namespace Wayhunter.Application.Modules.Games.Dtos;

public sealed record CreateGameDraftDto(
    string Title,
    string? Description,
    int DurationMinutes,
    IReadOnlyList<GameDraftStageDto> Stages
);
