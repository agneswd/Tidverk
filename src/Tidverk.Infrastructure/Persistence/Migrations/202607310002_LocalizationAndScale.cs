using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tidverk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TidverkDbContext))]
[Migration("202607310002_LocalizationAndScale")]
public sealed class LocalizationAndScale : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<int>(
            name: "CurrencyPreference",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
        migrationBuilder.AddColumn<int>(
            name: "InterfaceScalePercent",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 100);
        migrationBuilder.AddColumn<int>(
            name: "LanguagePreference",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "CurrencyPreference", table: "Settings");
        migrationBuilder.DropColumn(name: "InterfaceScalePercent", table: "Settings");
        migrationBuilder.DropColumn(name: "LanguagePreference", table: "Settings");
    }
}
