namespace Tidverk.Core;

public readonly record struct SwedishHoliday(DateOnly Date, string Name);

public interface ISwedishHolidayService {
    IReadOnlyCollection<SwedishHoliday> GetHolidays(int year);

    bool IsPublicHoliday(DateOnly date);
}

public sealed class SwedishHolidayService : ISwedishHolidayService {
    public IReadOnlyCollection<SwedishHoliday> GetHolidays(int year) {
        var easterSunday = CalculateEasterSunday(year);
        var holidays = new List<SwedishHoliday>();

        Add(holidays, new(year, 1, 1), "New Year's Day");
        Add(holidays, new(year, 1, 6), "Epiphany");
        Add(holidays, easterSunday.AddDays(-2), "Good Friday");
        Add(holidays, easterSunday, "Easter Sunday");
        Add(holidays, easterSunday.AddDays(1), "Easter Monday");
        Add(holidays, new(year, 5, 1), "May Day");
        Add(holidays, easterSunday.AddDays(39), "Ascension Day");
        Add(holidays, easterSunday.AddDays(49), "Whit Sunday");
        Add(holidays, new(year, 6, 6), "National Day");
        Add(holidays, FindSaturday(year, 6, 20, 6, 26), "Midsummer Day");
        Add(holidays, FindSaturday(year, 10, 31, 11, 6), "All Saints' Day");
        Add(holidays, new(year, 12, 25), "Christmas Day");
        Add(holidays, new(year, 12, 26), "Boxing Day");

        var firstDay = new DateOnly(year, 1, 1);
        var daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
        for (var offset = 0; offset < daysInYear; offset++) {
            var day = firstDay.AddDays(offset);
            if (day.DayOfWeek == DayOfWeek.Sunday) {
                Add(holidays, day, "Sunday");
            }
        }

        return holidays.OrderBy(holiday => holiday.Date).ToArray();
    }

    public bool IsPublicHoliday(DateOnly date) => GetHolidays(date.Year).Any(holiday => holiday.Date == date);

    private static void Add(ICollection<SwedishHoliday> holidays, DateOnly date, string name) {
        if (!holidays.Any(holiday => holiday.Date == date)) {
            holidays.Add(new(date, name));
        }
    }

    private static DateOnly FindSaturday(int year, int startMonth, int startDay, int endMonth, int endDay) {
        var start = new DateOnly(year, startMonth, startDay);
        var end = new DateOnly(year, endMonth, endDay);
        for (var date = start; date <= end; date = date.AddDays(1)) {
            if (date.DayOfWeek == DayOfWeek.Saturday) {
                return date;
            }
        }

        throw new InvalidOperationException("The statutory Saturday interval contains no Saturday.");
    }

    private static DateOnly CalculateEasterSunday(int year) {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }
}
