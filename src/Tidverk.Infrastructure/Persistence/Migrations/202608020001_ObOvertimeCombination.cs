using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tidverk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TidverkDbContext))]
[Migration("202608020001_ObOvertimeCombination")]
public sealed class ObOvertimeCombination : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<int>(
            name: "ObOvertimeCombination",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "ObOvertimeCombination", table: "Settings");
    }
}
