#nullable disable

namespace SlimMessageBus.Host.Outbox.DbContext.Test.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;
/// <inheritdoc />
public partial class AddUniqueIdToCustomer : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Lastname",
            table: "IntTest_Customer",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<string>(
            name: "Firstname",
            table: "IntTest_Customer",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AddColumn<string>(
            name: "UniqueId",
            table: "IntTest_Customer",
            type: "nvarchar(max)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UniqueId",
            table: "IntTest_Customer");

        migrationBuilder.AlterColumn<string>(
            name: "Lastname",
            table: "IntTest_Customer",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Firstname",
            table: "IntTest_Customer",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);
    }
}
