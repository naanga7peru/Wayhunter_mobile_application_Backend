
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class AiTripPlaceReviewEvaluatorTests
{
    private static AiTripItineraryRequest ValidRequest() =>
        new(
            "Jaffna",
            "Kandy",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            20000m,
            "Car",
            2,
            "Nature and history"
        );

    private static AiTripItineraryResponse ValidResponse(
        string? providerPlaceId = "place-001",
        string placeName = "Jaffna Fort") =>
        new(
            new[]
            {
                new AiTripItineraryDay(
                    new DateOnly(2026, 11, 20),
                    new[]
                    {
                        new AiTripItineraryStop(
                            placeName,
                            providerPlaceId,
                            new TimeOnly(10, 0),
                            500m)
                    })
            },
            500m,
            true
        );

    private static ExternalPlaceSuggestion ValidPlace() =>
        new(
            "place-001",
            "Jaffna Fort",
            9.66,
            80.02,
            "TestProvider"
        );

    private static IReadOnlyList<AiTripPlaceReview> Evaluate(
        AiTripItineraryResponse response,
        params ExternalPlaceSuggestion[] places)
    {
        return AiTripPlaceReviewEvaluator.Evaluate(
            ValidRequest(),
            response,
            places);
    }

    [Fact]
    public void MissingProviderId_ShouldRequireReview()
    {
        var result = Evaluate(
            ValidResponse(providerPlaceId: null));

        var review = Assert.Single(result);

        Assert.Equal(
            AiPlaceMatchStatus.MissingProviderId,
            review.Status);

        Assert.True(review.RequiresUserReview);
    }

    [Fact]
    public void UnknownProviderId_ShouldRequireReview()
    {
        var result = Evaluate(
            ValidResponse("unknown-001"),
            ValidPlace());

        var review = Assert.Single(result);

        Assert.Equal(
            AiPlaceMatchStatus.UnknownProviderId,
            review.Status);

        Assert.True(review.RequiresUserReview);
    }

    [Fact]
    public void NameMismatch_ShouldRequireReview()
    {
        var result = Evaluate(
            ValidResponse(
                "place-001",
                "Incorrect Place"),
            ValidPlace());

        var review = Assert.Single(result);

        Assert.Equal(
            AiPlaceMatchStatus.DetailsMismatch,
            review.Status);
    }

    [Fact]
    public void InvalidCoordinates_ShouldRequireReview()
    {
        var invalidPlace = ValidPlace() with
        {
            Latitude = 100
        };

        var result = Evaluate(
            ValidResponse(),
            invalidPlace);

        var review = Assert.Single(result);

        Assert.Equal(
            AiPlaceMatchStatus.DetailsMismatch,
            review.Status);
    }

    [Fact]
    public void MatchingExternalPlace_ShouldRemainUnverified()
    {
        var result = Evaluate(
            ValidResponse(),
            ValidPlace());

        var review = Assert.Single(result);

        Assert.Equal(
            AiPlaceMatchStatus.ExternalMatchUnverified,
            review.Status);

        Assert.True(review.RequiresUserReview);
        Assert.Equal("Jaffna Fort", review.PlaceName);
        Assert.Equal(0, review.StopIndex);
    }

    [Fact]
    public void MultipleStops_ShouldReturnSeparateReviews()
    {
        var response = ValidResponse() with
        {
            Days = new[]
            {
                new AiTripItineraryDay(
                    new DateOnly(2026, 11, 20),
                    new[]
                    {
                        new AiTripItineraryStop(
                            "Jaffna Fort",
                            "place-001",
                            new TimeOnly(10, 0),
                            500m),

                        new AiTripItineraryStop(
                            "Unknown Place",
                            null,
                            new TimeOnly(12, 0),
                            200m)
                    })
            },
            EstimatedTotalCostLkr = 700m
        };

        var result = Evaluate(
            response,
            ValidPlace());

        Assert.Equal(2, result.Count);

        Assert.Equal(
            AiPlaceMatchStatus.ExternalMatchUnverified,
            result[0].Status);

        Assert.Equal(
            AiPlaceMatchStatus.MissingProviderId,
            result[1].Status);

        Assert.Equal(0, result[0].StopIndex);
        Assert.Equal(1, result[1].StopIndex);
    }

    [Fact]
    public void NullProviderList_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AiTripPlaceReviewEvaluator.Evaluate(
                ValidRequest(),
                ValidResponse(),
                null!));
    }

    [Fact]
    public void InvalidAiResponse_ShouldThrow()
    {
        var response = ValidResponse() with
        {
            EstimatedTotalCostLkr = -100m
        };

        Assert.Throws<ArgumentException>(() =>
            Evaluate(response, ValidPlace()));
    }
}
