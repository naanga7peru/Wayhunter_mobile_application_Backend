using System;
using System.Collections.Generic;
using System.Text;


namespace Wayhunter.Application.Modules.TravelPlanning.Validators;

public static class TripPlanValidator
{
    public static IReadOnlyList<string> ValidateCreate(
        string tripName,
        DateOnly startDate,
        DateOnly endDate,
        decimal budgetLkr,
        string vehicleType,
        int headcount)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(tripName))
            errors.Add("Trip name is required.");

        if (endDate < startDate)
            errors.Add("End date cannot be before start date.");

        if (budgetLkr < 0)
            errors.Add("Budget cannot be negative.");

        if (string.IsNullOrWhiteSpace(vehicleType))
            errors.Add("Vehicle type is required.");

        if (headcount < 1)
            errors.Add("Headcount must be at least 1.");

        return errors;
    }
}
