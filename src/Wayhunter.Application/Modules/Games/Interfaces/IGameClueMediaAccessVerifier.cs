
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IGameClueMediaAccessVerifier
{
    // Return true only when:
    // 1. The media asset exists.
    // 2. It belongs to the authenticated creator.
    // 3. It is an allowed game clue image.
    // 4. It is not private player proof media.
    // 5. It is ready and authorized for use.
    //
    // Return false for unknown or unauthorized assets.

    Task<bool> IsUsableCreatorClueMediaAsync(
        string mediaAssetId,
        string creatorUserId,
        CancellationToken cancellationToken = default);
}
