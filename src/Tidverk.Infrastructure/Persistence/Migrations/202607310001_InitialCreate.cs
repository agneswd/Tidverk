using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tidverk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TidverkDbContext))]
[Migration("202607310001_InitialCreate")]
public sealed class InitialCreate : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.CreateTable(
            name: "Months",
            columns: table => new {
                Year = table.Column<int>(type: "INTEGER", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                OpeningBalanceMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                ExpectedMinutesOverride = table.Column<int>(type: "INTEGER", nullable: true),
                OpeningBalanceWasEdited = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Months", item => new { item.Year, item.Month }));

        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Projects", item => item.Id));

        migrationBuilder.CreateTable(
            name: "Settings",
            columns: table => new {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                EmployeeName = table.Column<string>(type: "TEXT", nullable: false),
                EmployerName = table.Column<string>(type: "TEXT", nullable: false),
                DefaultProject = table.Column<string>(type: "TEXT", nullable: false),
                HourlyRate = table.Column<decimal>(type: "TEXT", nullable: false),
                ExpectedHoursPerWorkday = table.Column<decimal>(type: "TEXT", nullable: false),
                ExpectedWorkingWeekdays = table.Column<string>(type: "TEXT", nullable: false),
                ExcludePublicHolidays = table.Column<bool>(type: "INTEGER", nullable: false),
                DefaultStartTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                DefaultEndTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                DefaultLunchMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                ThemePreference = table.Column<int>(type: "INTEGER", nullable: false),
                MonthViewPreference = table.Column<int>(type: "INTEGER", nullable: false),
                TaxMode = table.Column<int>(type: "INTEGER", nullable: false),
                TaxYear = table.Column<int>(type: "INTEGER", nullable: false),
                TaxTableNumber = table.Column<int>(type: "INTEGER", nullable: false),
                TaxColumn = table.Column<int>(type: "INTEGER", nullable: false),
                ManualTaxValue = table.Column<decimal>(type: "TEXT", nullable: true),
                OpeningBalanceMinutes = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Settings", item => item.Id));

        migrationBuilder.CreateTable(
            name: "WorkEntries",
            columns: table => new {
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                LunchMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                ProjectName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WorkEntries", item => item.Date));

        migrationBuilder.CreateIndex(name: "IX_Projects_Name", table: "Projects", column: "Name", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropTable("Months");
        migrationBuilder.DropTable("Projects");
        migrationBuilder.DropTable("Settings");
        migrationBuilder.DropTable("WorkEntries");
    }
}
