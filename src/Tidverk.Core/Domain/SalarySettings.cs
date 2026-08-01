namespace Tidverk.Core;

public enum SalaryType {
    Hourly,
    Monthly
}

/// <summary>The base salary used for ordinary pay and salary-based compensation rules.</summary>
public sealed record SalarySettings {
    public SalarySettings(
        SalaryType type,
        HourlySalary hourlySalary,
        decimal monthlySalary = 0m,
        decimal employmentPercent = 100m) {
        if (monthlySalary < 0m) {
            throw new ArgumentOutOfRangeException(nameof(monthlySalary), "Monthly salary cannot be negative.");
        }

        if (employmentPercent is <= 0m or > 100m) {
            throw new ArgumentOutOfRangeException(nameof(employmentPercent), "Employment percentage must be above 0% and no more than 100%.");
        }

        if (type == SalaryType.Monthly && monthlySalary <= 0m) {
            throw new ArgumentOutOfRangeException(nameof(monthlySalary), "Monthly salary must be above zero for monthly-paid employment.");
        }

        Type = type;
        HourlySalary = hourlySalary;
        MonthlySalary = monthlySalary;
        EmploymentPercent = employmentPercent;
    }

    public SalaryType Type { get; }

    /// <summary>The ordinary hourly rate, also used by percentage-based compensation rules.</summary>
    public HourlySalary HourlySalary { get; }

    /// <summary>The employee's contracted monthly salary at their employment percentage.</summary>
    public decimal MonthlySalary { get; }

    public decimal EmploymentPercent { get; }

    public decimal FullTimeMonthlySalary => Type == SalaryType.Monthly
        ? MonthlySalary * 100m / EmploymentPercent
        : 0m;

    public decimal BaseMonthlyPay => Type == SalaryType.Monthly ? MonthlySalary : 0m;

    public static SalarySettings Hourly(HourlySalary hourlySalary) => new(SalaryType.Hourly, hourlySalary);
}
