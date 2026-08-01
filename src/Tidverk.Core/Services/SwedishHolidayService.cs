using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace Tidverk.Core;

/// <summary>A Swedish public holiday. <paramref name="Name"/> is the invariant English name and doubles as its identifier.</summary>
public readonly record struct SwedishHoliday(DateOnly Date, string Name);

public interface ISwedishHolidayService {
    IReadOnlyCollection<SwedishHoliday> GetHolidays(int year);

    bool IsPublicHoliday(DateOnly date);

    bool IsMajorHolidayPeriod(DateOnly date, TimeOnly time);

    /// <summary>The invariant holiday name for this date, or null when it is an ordinary day.</summary>
    string? GetHolidayName(DateOnly date);
}

/// <summary>
/// Swedish public holidays as defined by lagen (1989:253) om allmänna helgdagar, which counts
/// every Sunday as a public holiday alongside the named ones. Christmas and Midsummer eves are
/// not statutory holidays and are deliberately absent.
/// </summary>
public sealed class SwedishHolidayService : ISwedishHolidayService {
    private readonly ConcurrentDictionary<int, HolidayYear> years = new();

    public IReadOnlyCollection<SwedishHoliday> GetHolidays(int year) => GetYear(year).Holidays;

    public bool IsPublicHoliday(DateOnly date) => GetYear(date.Year).NamesByDate.ContainsKey(date);

    public bool IsMajorHolidayPeriod(DateOnly date, TimeOnly time) {
        DateTime point = date.ToDateTime(time);
        return MajorHolidayPeriods(date.Year - 1)
            .Concat(MajorHolidayPeriods(date.Year))
            .Any(period => point >= period.Start && point < period.End);
    }

    public string? GetHolidayName(DateOnly date) => GetYear(date.Year).NamesByDate.GetValueOrDefault(date);

    private HolidayYear GetYear(int year) => years.GetOrAdd(year, static year => HolidayYear.Build(year));

    private IEnumerable<(DateTime Start, DateTime End)> MajorHolidayPeriods(int year) {
        DateOnly easter = HolidayYear.CalculateEasterSunday(year);
        DateOnly midsummerDay = HolidayYear.SaturdayOnOrAfter(new(year, 6, 20));
        yield return Period(easter.AddDays(-3), easter.AddDays(2));
        yield return Period(easter.AddDays(47), easter.AddDays(50));
        yield return Period(midsummerDay.AddDays(-2), midsummerDay.AddDays(2));
        yield return Period(new(year, 12, 23), NextWeekday(new(year, 12, 24)));
        yield return Period(new(year, 12, 30), NextWeekday(new(year, 12, 31)));

        static (DateTime Start, DateTime End) Period(DateOnly start, DateOnly end) =>
            (start.ToDateTime(new TimeOnly(19, 0)), end.ToDateTime(new TimeOnly(7, 0)));
    }

    private DateOnly NextWeekday(DateOnly date) {
        do {
            date = date.AddDays(1);
        } while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || IsPublicHoliday(date));

        return date;
    }

    /// <summary>One year of holidays. Cached because a single month view asks about dates hundreds of times.</summary>
    private sealed record HolidayYear(IReadOnlyCollection<SwedishHoliday> Holidays, FrozenDictionary<DateOnly, string> NamesByDate) {
        public static HolidayYear Build(int year) {
            DateOnly easterSunday = CalculateEasterSunday(year);
            Dictionary<DateOnly, string> named = [];
            Add(named, new(year, 1, 1), "New Year's Day");
            Add(named, new(year, 1, 6), "Epiphany");
            Add(named, easterSunday.AddDays(-2), "Good Friday");
            Add(named, easterSunday, "Easter Sunday");
            Add(named, easterSunday.AddDays(1), "Easter Monday");
            Add(named, new(year, 5, 1), "May Day");
            Add(named, easterSunday.AddDays(39), "Ascension Day");
            Add(named, easterSunday.AddDays(49), "Whit Sunday");
            Add(named, new(year, 6, 6), "National Day");
            Add(named, SaturdayOnOrAfter(new(year, 6, 20)), "Midsummer Day");
            Add(named, SaturdayOnOrAfter(new(year, 10, 31)), "All Saints' Day");
            Add(named, new(year, 12, 25), "Christmas Day");
            Add(named, new(year, 12, 26), "Boxing Day");
            AddSundays(named, year);

            SwedishHoliday[] holidays = named
                .OrderBy(holiday => holiday.Key)
                .Select(holiday => new SwedishHoliday(holiday.Key, holiday.Value))
                .ToArray();
            return new(holidays, named.ToFrozenDictionary());
        }

        /// <summary>Named holidays are added first so a named Sunday keeps its own name.</summary>
        private static void Add(Dictionary<DateOnly, string> holidays, DateOnly date, string name) => holidays.TryAdd(date, name);

        private static void AddSundays(Dictionary<DateOnly, string> holidays, int year) {
            DateOnly firstDay = new(year, 1, 1);
            int daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
            int firstSunday = (DayOfWeek.Sunday - firstDay.DayOfWeek + 7) % 7;
            for (int offset = firstSunday; offset < daysInYear; offset += 7) {
                Add(holidays, firstDay.AddDays(offset), "Sunday");
            }
        }

        internal static DateOnly SaturdayOnOrAfter(DateOnly start) =>
            start.AddDays((DayOfWeek.Saturday - start.DayOfWeek + 7) % 7);

        /// <summary>Anonymous Gregorian computus.</summary>
        internal static DateOnly CalculateEasterSunday(int year) {
            int goldenNumber = year % 19;
            int century = year / 100;
            int yearInCentury = year % 100;
            int centuryLeapDays = century / 4;
            int centuryRemainder = century % 4;
            int lunarCorrection = (century + 8) / 25;
            int lunarShift = (century - lunarCorrection + 1) / 3;
            int epact = (19 * goldenNumber + century - centuryLeapDays - lunarShift + 15) % 30;
            int yearLeapDays = yearInCentury / 4;
            int yearRemainder = yearInCentury % 4;
            int weekdayOffset = (32 + 2 * centuryRemainder + 2 * yearLeapDays - epact - yearRemainder) % 7;
            int correction = (goldenNumber + 11 * epact + 22 * weekdayOffset) / 451;
            int marchOffset = epact + weekdayOffset - 7 * correction + 114;
            return new DateOnly(year, marchOffset / 31, (marchOffset % 31) + 1);
        }
    }
}
