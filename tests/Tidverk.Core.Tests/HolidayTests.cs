using Tidverk.Core;
using Xunit;

namespace Tidverk.Core.Tests;

public sealed class HolidayTests {
    [Fact]
    public void Named_holidays_keep_their_name_when_they_fall_on_a_sunday() {
        SwedishHolidayService service = new();

        Assert.Equal("Easter Sunday", service.GetHolidayName(new DateOnly(2026, 4, 5)));
        Assert.Equal("Sunday", service.GetHolidayName(new DateOnly(2026, 7, 5)));
        Assert.Null(service.GetHolidayName(new DateOnly(2026, 7, 6)));
    }

    [Fact]
    public void Holiday_name_and_public_holiday_flag_agree() {
        SwedishHolidayService service = new();

        for (DateOnly date = new(2026, 1, 1); date.Year == 2026; date = date.AddDays(1)) {
            Assert.Equal(service.IsPublicHoliday(date), service.GetHolidayName(date) is not null);
        }
    }

    [Fact]
    public void Repeated_lookups_return_the_same_cached_result() {
        SwedishHolidayService service = new();

        IReadOnlyCollection<SwedishHoliday> first = service.GetHolidays(2026);
        IReadOnlyCollection<SwedishHoliday> second = service.GetHolidays(2026);

        Assert.Same(first, second);
        Assert.Equal(first.Select(holiday => holiday.Date), first.Select(holiday => holiday.Date).Order());
        Assert.Equal(first.Count, first.Select(holiday => holiday.Date).Distinct().Count());
    }

    [Theory]
    [InlineData(2024, 6, 22)]
    [InlineData(2025, 6, 21)]
    [InlineData(2026, 6, 20)]
    [InlineData(2027, 6, 26)]
    public void Midsummer_day_is_the_saturday_between_june_20_and_26(int year, int month, int day) {
        SwedishHolidayService service = new();

        Assert.Equal("Midsummer Day", service.GetHolidayName(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2024, 11, 2)]
    [InlineData(2025, 11, 1)]
    [InlineData(2026, 10, 31)]
    public void All_saints_day_is_the_saturday_between_october_31_and_november_6(int year, int month, int day) {
        SwedishHolidayService service = new();

        Assert.Equal("All Saints' Day", service.GetHolidayName(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 4, 2, 19, 0, true)]
    [InlineData(2026, 4, 7, 7, 0, false)]
    [InlineData(2026, 6, 18, 19, 0, true)]
    [InlineData(2026, 12, 23, 18, 59, false)]
    [InlineData(2026, 12, 23, 19, 0, true)]
    public void Major_holiday_periods_include_shift_time_boundaries(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        bool expected) {
        SwedishHolidayService service = new();

        Assert.Equal(expected, service.IsMajorHolidayPeriod(new DateOnly(year, month, day), new TimeOnly(hour, minute)));
    }
}
