using System.Globalization;

namespace Tidverk.Core;

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

    public static Minutes operator -(Minutes left, Minutes right) => new(left.Value - right.Value);

    public static implicit operator int(Minutes minutes) => minutes.Value;
}

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
    public static int NonNegative(int value) => Math.Max(0, value);

    public static Minutes Worked(TimeOnly? start, TimeOnly? end, Minutes lunch) {
        if (start is null || end is null) {
            return Minutes.Zero;
        }

        var elapsed = (int)(end.Value.ToTimeSpan() - start.Value.ToTimeSpan()).TotalMinutes;
        return new(NonNegative(elapsed - lunch.Value));
    }
}

public static class TimeInput {
    private static readonly string[] Formats = ["H\\:m", "H\\:mm", "HH\\:m", "HH\\:mm"];

    public static bool TryNormalize(string? input, out string normalized) {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) {
            return false;
        }

        var candidate = input.Trim().Replace('.', ':');
        if (candidate.All(char.IsDigit)) {
            candidate = candidate.Length switch {
                1 or 2 => $"{candidate}:00",
                3 => $"{candidate[..1]}:{candidate[1..]}",
                4 => $"{candidate[..2]}:{candidate[2..]}",
                _ => candidate
            };
        }

        if (!TimeOnly.TryParseExact(candidate, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) {
            return false;
        }

        normalized = time.ToString("HH:mm", CultureInfo.InvariantCulture);
        return true;
    }

    public static TimeOnly Parse(string input) {
        if (!TryNormalize(input, out var normalized) ||
            !TimeOnly.TryParseExact(normalized, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) {
            throw new FormatException($"'{input}' is not a valid time. Use HH:mm.");
        }

        return time;
    }

    public static string Normalize(string input) {
        return Parse(input).ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
