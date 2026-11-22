using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameProofMediaClaimRulesTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private static GameProofMediaClaimSnapshot Reserved() => new(
        Guid.NewGuid(), "trusted-provider", "canonical-media-1", Guid.NewGuid(),
        null, Guid.NewGuid(), Guid.NewGuid(), 1, "player-1",
        GameProofMediaClaimOrigin.NewOperation, GameProofMediaClaimState.Reserved,
        null, null, Start, Start);

    private static GameProofMediaClaimSnapshot Consumed(GameProofMediaClaimSnapshot claim) =>
        claim with { ClaimState = GameProofMediaClaimState.ProviderConsumed,
            ProviderConsumedAtUtc = Start.AddMinutes(1), UpdatedAtUtc = Start.AddMinutes(1) };

    private static GameProofMediaClaimSnapshot EvidenceCommitted(GameProofMediaClaimSnapshot claim) =>
        claim with { ClaimState = GameProofMediaClaimState.EvidenceCommitted,
            EvidenceId = Guid.NewGuid(), EvidenceCommittedAtUtc = Start.AddMinutes(2),
            UpdatedAtUtc = Start.AddMinutes(2) };

    [Fact]
    public void NewReserved_IsValid() => GameProofMediaClaimRules.Validate(Reserved());

    [Fact]
    public void NewConsumed_IsValid() => GameProofMediaClaimRules.Validate(Consumed(Reserved()));

    [Fact]
    public void NewEvidenceCommitted_IsValid() =>
        GameProofMediaClaimRules.Validate(EvidenceCommitted(Consumed(Reserved())));

    [Fact]
    public void LegacyIndexed_WithRealEvidence_IsValid()
    {
        var claim = Reserved() with {
            ClaimOrigin = GameProofMediaClaimOrigin.VerifiedLegacyEvidence,
            ClaimState = GameProofMediaClaimState.LegacyIndexed,
            SubmissionOperationId = null, EvidenceId = Guid.NewGuid() };
        GameProofMediaClaimRules.Validate(claim);
    }

    [Fact]
    public void LegacyIndexed_WithOperation_Rejects()
    {
        var claim = Reserved() with {
            ClaimOrigin = GameProofMediaClaimOrigin.VerifiedLegacyEvidence,
            ClaimState = GameProofMediaClaimState.LegacyIndexed,
            EvidenceId = Guid.NewGuid() };
        Assert.Throws<ArgumentException>(() => GameProofMediaClaimRules.Validate(claim));
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("media")]
    [InlineData("player")]
    public void MissingIdentity_Rejects(string field)
    {
        var claim = Reserved();
        claim = field switch {
            "provider" => claim with { MediaProviderNamespace = "  " },
            "media" => claim with { CanonicalMediaId = " " },
            _ => claim with { AuthenticatedPlayerUserId = "" }
        };
        Assert.Throws<ArgumentException>(() => GameProofMediaClaimRules.Validate(claim));
    }

    [Fact]
    public void OversizedCanonicalMedia_Rejects()
    {
        var claim = Reserved() with { CanonicalMediaId = new string('x', 257) };
        Assert.Throws<ArgumentException>(() => GameProofMediaClaimRules.Validate(claim));
    }

    [Fact]
    public void ConsumedWithoutReceiptTimestamp_Rejects()
    {
        var claim = Reserved() with { ClaimState = GameProofMediaClaimState.ProviderConsumed };
        Assert.Throws<ArgumentException>(() => GameProofMediaClaimRules.Validate(claim));
    }

    [Fact]
    public void EvidenceCommittedWithoutConsumedTimestamp_Rejects()
    {
        var claim = Reserved() with {
            ClaimState = GameProofMediaClaimState.EvidenceCommitted,
            EvidenceId = Guid.NewGuid(), EvidenceCommittedAtUtc = Start.AddMinutes(2),
            UpdatedAtUtc = Start.AddMinutes(2) };
        Assert.Throws<ArgumentException>(() => GameProofMediaClaimRules.Validate(claim));
    }

    [Fact]
    public void ValidTransitions_AreConsistent()
    {
        var a = Reserved();
        var b = Consumed(a);
        var c = EvidenceCommitted(b);
        GameProofMediaClaimRules.EnsureAllowedTransition(a, b);
        GameProofMediaClaimRules.EnsureAllowedTransition(b, c);
        GameProofMediaClaimRules.EnsureAllowedTransition(c, c);
    }

    [Fact]
    public void DefinitelyUnused_TombstoneTransition_IsConsistent()
    {
        var a = Reserved();
        var tombstone = a with {
            ClaimState = GameProofMediaClaimState.CancelledUnusedTombstone,
            UpdatedAtUtc = Start.AddMinutes(1) };
        GameProofMediaClaimRules.EnsureAllowedTransition(a, tombstone);
        Assert.Throws<InvalidOperationException>(() =>
            GameProofMediaClaimRules.EnsureAllowedTransition(tombstone, a));
    }

    [Fact]
    public void ConsumedToReserved_Rejects()
    {
        var a = Reserved();
        Assert.Throws<InvalidOperationException>(() =>
            GameProofMediaClaimRules.EnsureAllowedTransition(Consumed(a), a));
    }

    [Fact]
    public void ConsumedToTombstone_Rejects()
    {
        var a = Reserved();
        var b = Consumed(a);
        Assert.Throws<InvalidOperationException>(() =>
            GameProofMediaClaimRules.EnsureAllowedTransition(b, a with {
                ClaimState = GameProofMediaClaimState.CancelledUnusedTombstone }));
    }

    [Fact]
    public void MediaKeyCannotChange_EvenWithSameClaimId()
    {
        var a = Reserved();
        var other = a with { CanonicalMediaId = "other-media" };
        Assert.Throws<InvalidOperationException>(() =>
            GameProofMediaClaimRules.EnsureAllowedTransition(a, other));
    }

    [Fact]
    public void PlayerCannotChange()
    {
        var a = Reserved();
        var other = a with { AuthenticatedPlayerUserId = "attacker" };
        Assert.Throws<InvalidOperationException>(() =>
            GameProofMediaClaimRules.EnsureAllowedTransition(a, other));
    }

    [Fact]
    public void SameStateCannotRewriteTimestamp()
    {
        var a = Reserved();
        var other = a with { UpdatedAtUtc = Start.AddSeconds(1) };
        Assert.Throws<InvalidOperationException>(() =>
            GameProofMediaClaimRules.EnsureAllowedTransition(a, other));
    }

    [Fact]
    public void ConsumedReceiptTimestampCannotBeRewrittenOnFinalization()
    {
        var a = Consumed(Reserved());
        var b = EvidenceCommitted(a) with { ProviderConsumedAtUtc = Start.AddSeconds(30) };
        Assert.Throws<InvalidOperationException>(() =>
            GameProofMediaClaimRules.EnsureAllowedTransition(a, b));
    }

    [Fact]
    public void NonUtcOrBackdatedTimestamp_Rejects()
    {
        var a = Reserved() with { UpdatedAtUtc = Start.AddMinutes(-1) };
        Assert.Throws<ArgumentException>(() => GameProofMediaClaimRules.Validate(a));
        var b = Reserved() with { UpdatedAtUtc = Start.ToOffset(TimeSpan.FromHours(1)) };
        Assert.Throws<ArgumentException>(() => GameProofMediaClaimRules.Validate(b));
    }
}
