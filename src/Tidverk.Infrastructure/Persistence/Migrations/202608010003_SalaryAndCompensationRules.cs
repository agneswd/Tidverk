using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tidverk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TidverkDbContext))]
[Migration("202608010003_SalaryAndCompensationRules")]
public sealed class SalaryAndCompensationRules : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<int>(
            name: "SalaryType",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<decimal>(
            name: "MonthlySalary",
            table: "Settings",
            type: "TEXT",
            nullable: false,
            defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(
            name: "EmploymentPercent",
            table: "Settings",
            type: "TEXT",
            nullable: false,
            defaultValue: 100m);
        migrationBuilder.AddColumn<int>(
            name: "OvertimeThresholdMode",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<int>(
            name: "OvertimeDefaultRateType",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<int>(
            name: "ScheduledMinutesOverride",
            table: "WorkEntries",
            type: "INTEGER",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "SalaryType", table: "Settings");
        migrationBuilder.DropColumn(name: "MonthlySalary", table: "Settings");
        migrationBuilder.DropColumn(name: "EmploymentPercent", table: "Settings");
        migrationBuilder.DropColumn(name: "OvertimeThresholdMode", table: "Settings");
        migrationBuilder.DropColumn(name: "OvertimeDefaultRateType", table: "Settings");
        migrationBuilder.DropColumn(name: "ScheduledMinutesOverride", table: "WorkEntries");
    }
}
