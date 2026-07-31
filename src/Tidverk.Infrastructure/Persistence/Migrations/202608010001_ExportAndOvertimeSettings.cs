using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tidverk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TidverkDbContext))]
[Migration("202608010001_ExportAndOvertimeSettings")]
public sealed class ExportAndOvertimeSettings : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<int>(
            name: "ExportLanguagePreference",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<int>(
            name: "OvertimeCompensationMode",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<decimal>(
            name: "OvertimePremiumPercent",
            table: "Settings",
            type: "TEXT",
            nullable: false,
            defaultValue: 50m);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "ExportLanguagePreference", table: "Settings");
        migrationBuilder.DropColumn(name: "OvertimeCompensationMode", table: "Settings");
        migrationBuilder.DropColumn(name: "OvertimePremiumPercent", table: "Settings");
    }
}
