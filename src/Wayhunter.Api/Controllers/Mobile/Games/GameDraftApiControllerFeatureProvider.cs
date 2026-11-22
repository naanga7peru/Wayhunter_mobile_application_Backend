using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Wayhunter.Api.Controllers.Mobile.Games;

/// <summary>
/// Removes the draft HTTP controller from MVC discovery unless the host
/// explicitly enables it in Development. This prevents endpoint mapping,
/// rather than merely rejecting requests after a route has been published.
/// </summary>
public sealed class GameDraftApiControllerFeatureProvider(bool enabled)
    : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(
        IEnumerable<ApplicationPart> parts,
        ControllerFeature feature)
    {
        if (enabled)
            return;

        for (var index = feature.Controllers.Count - 1;
             index >= 0;
             index--)
        {
            if (feature.Controllers[index].AsType() ==
                typeof(GameDraftsController))
            {
                feature.Controllers.RemoveAt(index);
            }
        }
    }
}
