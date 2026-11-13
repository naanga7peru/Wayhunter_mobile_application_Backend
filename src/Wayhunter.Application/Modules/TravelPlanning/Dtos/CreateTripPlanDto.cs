using System;
using System.Collections.Generic;
using System.Text;


namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

public sealed record CreateTripPlanDto(
    string TripName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal BudgetLkr,
    string VehicleType,
    int Headcount
);

