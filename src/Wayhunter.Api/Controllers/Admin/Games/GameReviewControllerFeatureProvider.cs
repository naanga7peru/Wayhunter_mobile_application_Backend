using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Wayhunter.Api.Controllers.Admin.Games;

public sealed class GameReviewControllerFeatureProvider(bool enabled)
    : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (enabled) return;
        for (var index = feature.Controllers.Count - 1; index >= 0; index--)
            if (feature.Controllers[index].AsType() == typeof(GameReviewController))
                feature.Controllers.RemoveAt(index);
    }
}
