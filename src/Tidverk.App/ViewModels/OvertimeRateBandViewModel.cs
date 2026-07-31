using CommunityToolkit.Mvvm.ComponentModel;
using Tidverk.App.Services;
using Tidverk.Core;

namespace Tidverk.App.ViewModels;

public sealed class OvertimeRateBandViewModel : ObservableObject {
    private string name = string.Empty;
    private OvertimeDayCategory dayCategory;
    private string start = "17:00";
    private string end = "18:00";
    private decimal premiumPercent = 50m;

    public string Name { get => name; set => SetProperty(ref name, value); }

    public OvertimeDayCategory DayCategory { get => dayCategory; set => SetProperty(ref dayCategory, value); }

    public string Start { get => start; set => SetProperty(ref start, value); }

    public string End { get => end; set => SetProperty(ref end, value); }

    public decimal PremiumPercent { get => premiumPercent; set => SetProperty(ref premiumPercent, value); }

    public OvertimeRateBand ToDomain() => new(Name, DayCategory, TimeInput.Parse(Start), TimeInput.Parse(End), PremiumPercent);

    public static OvertimeRateBandViewModel FromDomain(OvertimeRateBand band) => new() {
        Name = band.Name,
        DayCategory = band.DayCategory,
        Start = band.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
        End = band.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
        PremiumPercent = band.PremiumPercent
    };
}
