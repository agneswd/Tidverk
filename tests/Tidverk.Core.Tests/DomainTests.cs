using Tidverk.Core;
using Xunit;

namespace Tidverk.Core.Tests;

public sealed class DomainTests {
    [Theory]
    [InlineData(8, 0, 16, 0, 0, 480)]
    [InlineData(8, 0, 16, 30, 30, 480)]
    [InlineData(8, 15, 16, 45, 45, 465)]
    [InlineData(8, 0, 16, 30, 60, 450)]
    public void Worked_entry_calculates_normal_lunch_quarter_and_half_hours(
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        int lunchMinutes,
        int expectedWorkedMinutes) {
        var entry = WorkEntry.CreateWorked(
            new DateOnly(2026, 6, 1),
            new TimeOnly(startHour, startMinute),
            new TimeOnly(endHour, endMinute),
            lunchMinutes);

        Assert.Equal(expectedWorkedMinutes, entry.WorkedMinutes.Value);
        Assert.Equal(expectedWorkedMinutes / 60m, entry.WorkedHours);
    }

    [Fact]
    public void Minutes_reject_negative_values() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Minutes(-1));
    }

    [Theory]
    [InlineData(22, 0, 6, 0, 30, "overnight")]
    [InlineData(8, 0, 8, 0, 0, "later than")]
    [InlineData(8, 0, 16, 0, -1, "negative")]
    [InlineData(8, 0, 8, 15, 30, "exceed")]
    public void Try_create_worked_rejects_invalid_ordering_and_lunch(
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        int lunchMinutes,
        string expectedError) {
        var created = WorkEntry.TryCreateWorked(
            new DateOnly(2026, 6, 1),
            $"{startHour}:{startMinute:00}",
            $"{endHour}:{endMinute:00}",
            lunchMinutes,
            out var entry,
            out var errors);

        Assert.False(created);
        Assert.Null(entry);
        Assert.True(string.Join(" ", errors).Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Off_entry_is_complete_without_worked_values() {
        var entry = WorkEntry.CreateOff(new DateOnly(2026, 6, 1), "Holiday");

        Assert.Equal(WorkEntryStatus.Off, entry.Status);
        Assert.True(entry.IsComplete);
        Assert.Equal(Minutes.Zero, entry.WorkedMinutes);
        Assert.Equal("Holiday", entry.Notes);
        Assert.Empty(entry.Validate());
    }

    [Fact]
    public void Reset_returns_an_incomplete_entry_for_the_same_date() {
        var entry = WorkEntry.CreateWorked(new DateOnly(2026, 6, 1), new TimeOnly(8, 0), new TimeOnly(16, 30), 30);

        var reset = entry.Reset();

        Assert.Equal(entry.Date, reset.Date);
        Assert.Equal(WorkEntryStatus.Incomplete, reset.Status);
        Assert.False(reset.IsComplete);
        Assert.Equal(Minutes.Zero, reset.WorkedMinutes);
        Assert.Empty(reset.Validate());
    }

    [Fact]
    public void Time_input_normalizes_bare_colon_dot_and_digit_forms() {
        Assert.Equal("08:00", TimeInput.Normalize("8"));
        Assert.Equal("08:30", TimeInput.Normalize("830"));
        Assert.Equal("08:30", TimeInput.Normalize("8.30"));
        Assert.Equal("16:05", TimeInput.Normalize("1605"));
        Assert.False(TimeInput.TryNormalize("25:00", out _));
    }

    [Fact]
    public void Worked_minutes_never_become_negative() {
        Assert.Equal(0, MinuteMath.Worked(new TimeOnly(8, 0), new TimeOnly(8, 15), new Minutes(30)).Value);
    }
}
