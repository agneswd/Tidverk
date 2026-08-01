using CommunityToolkit.Mvvm.ComponentModel;
using Tidverk.Core;

namespace Tidverk.App.ViewModels;

/// <summary>An editable row in the overtime rate-band list; times stay as text until the settings are saved.</summary>
public sealed partial class OvertimeRateBandViewModel : ObservableObject {
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private OvertimeDayCategory dayCategory;

    [ObservableProperty]
    private string start = "17:00";

    [ObservableProperty]
    private string end = "18:00";

    [ObservableProperty]
    private decimal premiumPercent = 50m;

    public static OvertimeRateBandViewModel FromDomain(OvertimeRateBand band) {
        ArgumentNullException.ThrowIfNull(band);
        return new() {
            Name = band.Name,
            DayCategory = band.DayCategory,
            Start = TimeInput.Format(band.StartTime),
            End = TimeInput.Format(band.EndTime),
            PremiumPercent = band.PremiumPercent
        };
    }

    public OvertimeRateBand ToDomain() => new(Name, DayCategory, TimeInput.Parse(Start), TimeInput.Parse(End), PremiumPercent);
}
