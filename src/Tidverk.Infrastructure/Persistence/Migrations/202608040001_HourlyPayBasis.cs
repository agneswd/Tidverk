using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Tidverk.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TidverkDbContext))]
[Migration("202608040001_HourlyPayBasis")]
public sealed class HourlyPayBasisMigration : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<int>(
            name: "HourlyPayBasis",
            table: "Settings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "HourlyPayBasis", table: "Settings");
    }
}
