using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tidverk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TidverkDbContext))]
[Migration("202608010002_OvertimeRateRules")]
public sealed class OvertimeRateRules : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<decimal>(
            name: "OvertimeDailyThresholdHours",
            table: "Settings",
            type: "TEXT",
            nullable: false,
            defaultValue: 8m);
        migrationBuilder.AddColumn<string>(
            name: "OvertimeRateBandsJson",
            table: "Settings",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "OvertimeDailyThresholdHours", table: "Settings");
        migrationBuilder.DropColumn(name: "OvertimeRateBandsJson", table: "Settings");
    }
}
