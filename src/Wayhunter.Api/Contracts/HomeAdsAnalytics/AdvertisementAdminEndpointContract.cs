
using System;
using Wayhunter.Api.Contracts;

namespace Wayhunter.Api.Contracts.HomeAdsAnalytics;

// M7 Admin Advertisement operations.
public enum AdvertisementAdminOperation
{
    Create = 1,
    Update = 2,
    ChangeStatus = 3,
    UploadImage = 4
}

// Proposed HTTP endpoint metadata.
// This is NOT an authorization implementation.
public sealed record AdvertisementAdminEndpointDefinition(
    string HttpMethod,
    string Route,
    IdentityEndpointAccess RequiredAccess);

// M7-MS02-T03:
// Defines the proposed Admin advertisement endpoint
// access requirements for future M1 JWT/RBAC integration.
public static class AdvertisementAdminEndpointContract
{
    public const string AdminBaseRoute =
        "api/admin/advertisements";

    public static AdvertisementAdminEndpointDefinition
        GetDefinition(AdvertisementAdminOperation operation)
    {
        return operation switch
        {
            AdvertisementAdminOperation.Create =>
                new AdvertisementAdminEndpointDefinition(
                    "POST",
                    AdminBaseRoute,
                    IdentityEndpointAccess.Administrator),

            AdvertisementAdminOperation.Update =>
                new AdvertisementAdminEndpointDefinition(
                    "PUT",
                    AdminBaseRoute + "/{advertisementId:guid}",
                    IdentityEndpointAccess.Administrator),

            AdvertisementAdminOperation.ChangeStatus =>
                new AdvertisementAdminEndpointDefinition(
                    "PATCH",
                    AdminBaseRoute + "/{advertisementId:guid}/status",
                    IdentityEndpointAccess.Administrator),

            AdvertisementAdminOperation.UploadImage =>
                new AdvertisementAdminEndpointDefinition(
                    "POST",
                    AdminBaseRoute + "/{advertisementId:guid}/image",
                    IdentityEndpointAccess.Administrator),

            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unsupported Admin advertisement operation.")
        };
    }
}
