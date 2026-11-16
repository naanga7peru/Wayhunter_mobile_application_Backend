
using System;

namespace Wayhunter.Application.Modules.TravelPlanning.Exceptions;

public sealed class TripPlanConcurrencyException : Exception
{
    public Guid TripPlanId { get; }

    public TripPlanConcurrencyException(Guid tripPlanId)
        : base(
            $"Trip plan '{tripPlanId}' was modified by another request. Reload the trip and try again.")
    {
        TripPlanId = tripPlanId;
    }
}
