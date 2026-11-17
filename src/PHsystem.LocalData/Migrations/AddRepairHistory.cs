using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PHsystem.LocalData.Migrations;

[DbContext(typeof(SnapshotDb))]
[Migration("202611170001_AddRepairHistory")]
internal sealed class AddRepairHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("RepairHistory", table => new
        {
            Id = table.Column<Guid>(type: "TEXT", nullable: false),
            CapturedAt = table.Column<long>(type: "INTEGER", nullable: false),
            Action = table.Column<int>(type: "INTEGER", nullable: false),
            Outcome = table.Column<int>(type: "INTEGER", nullable: false),
        }, constraints: table => table.PrimaryKey("PK_RepairHistory", row => row.Id));
        migrationBuilder.CreateIndex("IX_RepairHistory_CapturedAt", "RepairHistory", "CapturedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("RepairHistory");
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => SnapshotSchema.Build(modelBuilder, includeRepairHistory: true);
}
