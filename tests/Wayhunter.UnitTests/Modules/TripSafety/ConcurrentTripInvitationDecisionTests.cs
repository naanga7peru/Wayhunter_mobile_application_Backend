
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class ConcurrentTripInvitationDecisionTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DecisionAt =
        CreatedAt.AddMinutes(10);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return DecisionAt;
        }
    }

    // Forces two requests to read the same Pending
    // invitation before either request saves a decision.
    //
    // This reproduces competing requests reliably,
    // instead of relying on thread scheduling.

    private sealed class ConcurrentReadRepository
        : ITripInvitationDecisionRepository
    {
        private readonly ITripInvitationDecisionRepository
            _inner;

        private readonly TaskCompletionSource<bool>
            _bothReadsCompleted =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _readCount;

        public ConcurrentReadRepository(
            ITripInvitationDecisionRepository inner)
        {
            _inner = inner
                ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<TripInvitation?> GetByIdAsync(
            Guid invitationId,
            CancellationToken cancellationToken = default)
        {
            var invitation = await _inner.GetByIdAsync(
                invitationId,
                cancellationToken);

            var readNumber =
                Interlocked.Increment(ref _readCount);

            if (readNumber == 2)
            {
                _bothReadsCompleted.TrySetResult(true);
            }

            if (readNumber <= 2)
            {
                await _bothReadsCompleted.Task.WaitAsync(
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
            }

            return invitation;
        }

        public Task<TripMember?> GetMemberByInvitationIdAsync(
            Guid invitationId,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetMemberByInvitationIdAsync(
                invitationId,
                cancellationToken);
        }

        public Task SaveDecisionAsync(
            TripInvitation invitation,
            TripMember? memberToAdd,
            CancellationToken cancellationToken = default)
        {
            return _inner.SaveDecisionAsync(
                invitation,
                memberToAdd,
                cancellationToken);
        }
    }

    private static TripInvitation CreateInvitation()
    {
        return TripInvitation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-owner",
            "user-friend",
            CreatedAt);
    }

    private static TripInvitationDecisionService CreateService(
        ITripInvitationDecisionRepository repository)
    {
        return new TripInvitationDecisionService(
            repository,
            new FixedTimeProvider(),
            new TripInviteBlockReaderStub());
    }

    [Fact]
    public async Task Concurrent_Accepts_Should_Return_Same_Member()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var concurrentRepository =
            new ConcurrentReadRepository(repository);

        var service = CreateService(concurrentRepository);

        var firstTask = service.AcceptAsync(
            invitation.Id,
            "user-friend");

        var secondTask = service.AcceptAsync(
            invitation.Id,
            "user-friend");

        var members = await Task.WhenAll(
            firstTask,
            secondTask);

        Assert.Equal(2, members.Length);

        Assert.Equal(
            members[0].Id,
            members[1].Id);

        Assert.Equal(
            members[0].InvitationId,
            members[1].InvitationId);

        var persistedMember =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.NotNull(persistedMember);

        Assert.Equal(
            persistedMember.Id,
            members[0].Id);

        var storedInvitation =
            await repository.GetByIdAsync(
                invitation.Id);

        Assert.NotNull(storedInvitation);

        Assert.Equal(
            TripInvitationStatus.Accepted,
            storedInvitation.Status);
    }

    [Fact]
    public async Task Concurrent_Rejects_Should_Be_Idempotent()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(
            new ConcurrentReadRepository(repository));

        var firstTask = service.RejectAsync(
            invitation.Id,
            "user-friend");

        var secondTask = service.RejectAsync(
            invitation.Id,
            "user-friend");

        var results = await Task.WhenAll(
            firstTask,
            secondTask);

        Assert.All(results, result =>
        {
            Assert.Equal(
                TripInvitationStatus.Rejected,
                result.Status);

            Assert.Equal(
                DecisionAt,
                result.RespondedAtUtc);
        });

        var member =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.Null(member);
    }

    [Fact]
    public async Task Concurrent_Cancels_Should_Be_Idempotent()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(
            new ConcurrentReadRepository(repository));

        var firstTask = service.CancelAsync(
            invitation.Id,
            "user-owner");

        var secondTask = service.CancelAsync(
            invitation.Id,
            "user-owner");

        var results = await Task.WhenAll(
            firstTask,
            secondTask);

        Assert.All(results, result =>
        {
            Assert.Equal(
                TripInvitationStatus.Cancelled,
                result.Status);
        });

        Assert.Null(
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id));
    }

    [Fact]
    public async Task Concurrent_Accept_And_Reject_Only_One_Wins()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(
            new ConcurrentReadRepository(repository));

        var acceptTask = service.AcceptAsync(
            invitation.Id,
            "user-friend");

        var rejectTask = service.RejectAsync(
            invitation.Id,
            "user-friend");

        var errors = await Task.WhenAll(
            Record.ExceptionAsync(async () =>
            {
                await acceptTask;
            }),
            Record.ExceptionAsync(async () =>
            {
                await rejectTask;
            }));

        Assert.Equal(
            1,
            errors.Count(error => error is null));

        Assert.Equal(
            1,
            errors.Count(error =>
                error is InvalidOperationException));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        var member =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        if (stored.Status == TripInvitationStatus.Accepted)
        {
            Assert.NotNull(member);
            Assert.Null(errors[0]);
        }
        else
        {
            Assert.Equal(
                TripInvitationStatus.Rejected,
                stored.Status);

            Assert.Null(member);
            Assert.Null(errors[1]);
        }
    }

    [Fact]
    public async Task Concurrent_Accept_And_Cancel_Only_One_Wins()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(
            new ConcurrentReadRepository(repository));

        var acceptTask = service.AcceptAsync(
            invitation.Id,
            "user-friend");

        var cancelTask = service.CancelAsync(
            invitation.Id,
            "user-owner");

        var errors = await Task.WhenAll(
            Record.ExceptionAsync(async () =>
            {
                await acceptTask;
            }),
            Record.ExceptionAsync(async () =>
            {
                await cancelTask;
            }));

        Assert.Equal(
            1,
            errors.Count(error => error is null));

        Assert.Equal(
            1,
            errors.Count(error =>
                error is InvalidOperationException));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        var member =
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id);

        Assert.NotNull(stored);

        if (stored.Status == TripInvitationStatus.Accepted)
        {
            Assert.NotNull(member);
            Assert.Null(errors[0]);
        }
        else
        {
            Assert.Equal(
                TripInvitationStatus.Cancelled,
                stored.Status);

            Assert.Null(member);
            Assert.Null(errors[1]);
        }
    }

    [Fact]
    public async Task Concurrent_Reject_And_Cancel_Only_One_Wins()
    {
        var repository =
            new InMemoryTripInvitationDecisionRepository();

        var invitation = CreateInvitation();

        await repository.AddAsync(invitation);

        var service = CreateService(
            new ConcurrentReadRepository(repository));

        var rejectTask = service.RejectAsync(
            invitation.Id,
            "user-friend");

        var cancelTask = service.CancelAsync(
            invitation.Id,
            "user-owner");

        var errors = await Task.WhenAll(
            Record.ExceptionAsync(async () =>
            {
                await rejectTask;
            }),
            Record.ExceptionAsync(async () =>
            {
                await cancelTask;
            }));

        Assert.Equal(
            1,
            errors.Count(error => error is null));

        Assert.Equal(
            1,
            errors.Count(error =>
                error is InvalidOperationException));

        var stored = await repository.GetByIdAsync(
            invitation.Id);

        Assert.NotNull(stored);

        Assert.True(
            stored.Status == TripInvitationStatus.Rejected ||
            stored.Status == TripInvitationStatus.Cancelled);

        Assert.Null(
            await repository.GetMemberByInvitationIdAsync(
                invitation.Id));
    }
}
