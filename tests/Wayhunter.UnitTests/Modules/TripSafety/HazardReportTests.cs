
using System;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class HazardReportTests
{
    private static readonly DateTimeOffset ReportTime =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static HazardReport CreateReport()
    {
        return HazardReport.Report(
            Guid.NewGuid(),
            "reporter-001",
            ReportTime,
            ReportTime.AddMinutes(-2));
    }

    [Fact]
    public void New_Report_Should_Be_Unverified()
    {
        var hazard = CreateReport();

        Assert.Equal(HazardStatus.Reported, hazard.Status);
        Assert.Equal("reporter-001", hazard.ReporterUserId);
        Assert.Null(hazard.VerifiedAtUtc);
        Assert.Null(hazard.ResolvedAtUtc);
    }

    [Fact]
    public void Report_Should_Expire_After_24_Hours()
    {
        var hazard = CreateReport();

        Assert.Equal(
            ReportTime.AddHours(24),
            hazard.ExpiresAtUtc);
    }

    [Fact]
    public void Report_Should_Store_Photo_Capture_Time()
    {
        var hazard = CreateReport();

        Assert.Equal(
            ReportTime.AddMinutes(-2),
            hazard.PhotoCapturedAtUtc);
    }

    [Fact]
    public void Report_Should_Reject_Empty_Id()
    {
        Assert.Throws<ArgumentException>(() =>
            HazardReport.Report(
                Guid.Empty,
                "reporter-001",
                ReportTime,
                ReportTime));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Report_Should_Reject_Empty_Reporter(
        string reporter)
    {
        Assert.Throws<ArgumentException>(() =>
            HazardReport.Report(
                Guid.NewGuid(),
                reporter,
                ReportTime,
                ReportTime));
    }

    [Fact]
    public void Report_Should_Reject_Invalid_Server_Time()
    {
        Assert.Throws<ArgumentException>(() =>
            HazardReport.Report(
                Guid.NewGuid(),
                "reporter-001",
                default,
                ReportTime));
    }

    [Fact]
    public void Report_Should_Reject_Invalid_Photo_Time()
    {
        Assert.Throws<ArgumentException>(() =>
            HazardReport.Report(
                Guid.NewGuid(),
                "reporter-001",
                ReportTime,
                default));
    }

    [Fact]
    public void Verify_Should_Update_Status()
    {
        var hazard = CreateReport();
        var time = ReportTime.AddMinutes(5);

        hazard.Verify("admin-001", time);

        Assert.Equal(HazardStatus.Verified, hazard.Status);
        Assert.Equal("admin-001", hazard.VerifiedByUserId);
        Assert.Equal(time, hazard.VerifiedAtUtc);
    }

    [Fact]
    public void Reporter_Cannot_Verify_Own_Hazard()
    {
        var hazard = CreateReport();

        Assert.Throws<InvalidOperationException>(() =>
            hazard.Verify(
                "reporter-001",
                ReportTime.AddMinutes(1)));

        Assert.Equal(HazardStatus.Reported, hazard.Status);
    }

    [Fact]
    public void Duplicate_Verification_Should_Fail()
    {
        var hazard = CreateReport();

        hazard.Verify(
            "admin-001",
            ReportTime.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            hazard.Verify(
                "admin-002",
                ReportTime.AddMinutes(2)));

        Assert.Equal("admin-001", hazard.VerifiedByUserId);
    }

    [Fact]
    public void Verification_After_Expiry_Should_Fail()
    {
        var hazard = CreateReport();

        Assert.Throws<InvalidOperationException>(() =>
            hazard.Verify(
                "admin-001",
                ReportTime.AddHours(24)));

        Assert.Equal(HazardStatus.Reported, hazard.Status);
    }

    [Fact]
    public void Verify_Before_Report_Time_Should_Fail()
    {
        var hazard = CreateReport();

        Assert.Throws<InvalidOperationException>(() =>
            hazard.Verify(
                "admin-001",
                ReportTime.AddMinutes(-1)));
    }

    [Fact]
    public void Resolve_Reported_Hazard_Should_Succeed()
    {
        var hazard = CreateReport();
        var time = ReportTime.AddMinutes(10);

        hazard.Resolve(
            "admin-001",
            "Road is safe",
            time);

        Assert.Equal(HazardStatus.Resolved, hazard.Status);
        Assert.Equal(time, hazard.ResolvedAtUtc);
        Assert.Equal("admin-001", hazard.ResolvedByUserId);
        Assert.Equal("Road is safe", hazard.ResolutionReason);
    }

    [Fact]
    public void Resolve_Verified_Hazard_Should_Succeed()
    {
        var hazard = CreateReport();

        hazard.Verify(
            "admin-001",
            ReportTime.AddMinutes(5));

        hazard.Resolve(
            "admin-002",
            "Hazard cleared",
            ReportTime.AddMinutes(10));

        Assert.Equal(HazardStatus.Resolved, hazard.Status);
    }

    [Fact]
    public void Resolve_Without_Reason_Should_Fail()
    {
        var hazard = CreateReport();

        Assert.Throws<ArgumentException>(() =>
            hazard.Resolve(
                "admin-001",
                " ",
                ReportTime.AddMinutes(1)));
    }

    [Fact]
    public void Resolve_After_Expiry_Should_Fail()
    {
        var hazard = CreateReport();

        Assert.Throws<InvalidOperationException>(() =>
            hazard.Resolve(
                "admin-001",
                "Cleared",
                ReportTime.AddHours(25)));
    }

    [Fact]
    public void Expire_Before_24_Hours_Should_Fail()
    {
        var hazard = CreateReport();

        Assert.Throws<InvalidOperationException>(() =>
            hazard.Expire(
                ReportTime.AddHours(23)));

        Assert.Equal(HazardStatus.Reported, hazard.Status);
    }

    [Fact]
    public void Expire_At_24_Hours_Should_Succeed()
    {
        var hazard = CreateReport();
        var expiry = ReportTime.AddHours(24);

        hazard.Expire(expiry);

        Assert.Equal(HazardStatus.Expired, hazard.Status);
        Assert.Equal(expiry, hazard.ExpiredAtUtc);
    }

    [Fact]
    public void Duplicate_Expiry_Should_Not_Change_Time()
    {
        var hazard = CreateReport();

        hazard.Expire(ReportTime.AddHours(24));
        hazard.Expire(ReportTime.AddHours(25));

        Assert.Equal(
            ReportTime.AddHours(24),
            hazard.ExpiredAtUtc);
    }

    [Fact]
    public void Resolved_Hazard_Should_Remain_Resolved()
    {
        var hazard = CreateReport();

        hazard.Resolve(
            "admin-001",
            "Cleared",
            ReportTime.AddMinutes(5));

        hazard.Expire(ReportTime.AddHours(24));

        Assert.Equal(HazardStatus.Resolved, hazard.Status);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(60, true)]
    [InlineData(1440, false)]
    public void Warning_Activity_Should_Respect_Time(
        int minutes,
        bool expected)
    {
        var hazard = CreateReport();

        Assert.Equal(
            expected,
            hazard.IsWarningActive(
                ReportTime.AddMinutes(minutes)));
    }

    [Fact]
    public void Resolved_Hazard_Should_Not_Show_Warning()
    {
        var hazard = CreateReport();

        hazard.Resolve(
            "admin-001",
            "Resolved",
            ReportTime.AddMinutes(5));

        Assert.False(
            hazard.IsWarningActive(
                ReportTime.AddMinutes(6)));
    }

    [Fact]
    public void Verified_Hazard_Should_Show_Active_Warning()
    {
        var hazard = CreateReport();

        hazard.Verify(
            "admin-001",
            ReportTime.AddMinutes(5));

        Assert.True(
            hazard.IsWarningActive(
                ReportTime.AddMinutes(10)));
    }
}
