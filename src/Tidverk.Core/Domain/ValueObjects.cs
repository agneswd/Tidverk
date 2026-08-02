using System.Globalization;

namespace Tidverk.Core;

/// <summary>A non-negative duration in whole minutes.</summary>
public readonly record struct Minutes {
    public Minutes(int value) {
        if (value < 0) {
            throw new ArgumentOutOfRangeException(nameof(value), "Minutes cannot be negative.");
        }

        Value = value;
    }

    public int Value { get; }

    public decimal Hours => Value / 60m;

    public static Minutes Zero => new(0);

    public static Minutes From(int value) => new(value);

    public TimeSpan ToTimeSpan() => TimeSpan.FromMinutes(Value);

    public static Minutes operator +(Minutes left, Minutes right) => new(left.Value + right.Value);
}

/// <summary>A non-negative pay rate per worked hour, in the currency the user configured.</summary>
public readonly record struct HourlySalary {
    public HourlySalary(decimal amount) {
        if (amount < 0) {
            throw new ArgumentOutOfRangeException(nameof(amount), "Hourly salary cannot be negative.");
        }

        Amount = amount;
    }

    public decimal Amount { get; }
}

public static class MinuteMath {
    public const int MinutesPerDay = 24 * 60;

    /// <summary>
    /// Minutes between two clock times, never negative. An end at or before the start means the shift
    /// runs past midnight, so it is measured forward into the next day.
    /// </summary>
    public static int Elapsed(TimeOnly start, TimeOnly end) {
        int minutes = (int)(end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes;
        return minutes > 0 ? minutes : minutes + MinutesPerDay;
    }

    /// <summary>Minutes actually worked between two times, never negative.</summary>
    public static Minutes Worked(TimeOnly? start, TimeOnly? end, Minutes lunch) {
        if (start is null || end is null || start == end) {
            return Minutes.Zero;
        }

        return new(Math.Max(0, Elapsed(start.Value, end.Value) - lunch.Value));
    }
}

/// <summary>Parses the shorthand clock times the day editor accepts: "8", "830", "8.30", "08:30".</summary>
public static class TimeInput {
    private const string DisplayFormat = "HH\\:mm";
    private static readonly string[] AcceptedFormats = ["H\\:m", "H\\:mm"];

    public static bool TryNormalize(string? input, out string normalized) {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) {
            return false;
        }

        string candidate = input.Trim().Replace('.', ':');
        if (candidate.All(char.IsAsciiDigit)) {
            candidate = candidate.Length switch {
                1 or 2 => $"{candidate}:00",
                3 => $"{candidate[..1]}:{candidate[1..]}",
                4 => $"{candidate[..2]}:{candidate[2..]}",
                _ => candidate
            };
        }

        if (!TimeOnly.TryParseExact(candidate, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time)) {
            return false;
        }

        normalized = Format(time);
        return true;
    }

    public static TimeOnly Parse(string input) {
        if (!TryNormalize(input, out string normalized)) {
            throw new FormatException($"'{input}' is not a valid time. Use HH:mm.");
        }

        return TimeOnly.ParseExact(normalized, DisplayFormat, CultureInfo.InvariantCulture);
    }

    public static string Normalize(string input) => Format(Parse(input));

    public static string Format(TimeOnly time) => time.ToString(DisplayFormat, CultureInfo.InvariantCulture);
}
