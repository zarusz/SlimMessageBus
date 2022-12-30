#nullable disable

namespace SlimMessageBus.Host.Outbox.DbContext.Test.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IntTest_Customer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Firstname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Lastname = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntTest_Customer", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "IntTest_Customer");
    }
}
