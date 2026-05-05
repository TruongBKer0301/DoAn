using LapTopBD.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LapTopBD.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260505000000_RemoveDistrictColumns")]
    public partial class RemoveDistrictColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'order' AND COLUMN_NAME = 'district')
    ALTER TABLE [dbo].[order] DROP COLUMN [district];

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'users' AND COLUMN_NAME = 'district')
    ALTER TABLE [dbo].[users] DROP COLUMN [district];");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "district",
                table: "users",
                type: "nvarchar(255)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "district",
                table: "order",
                type: "nvarchar(255)",
                nullable: false,
                defaultValue: string.Empty);
        }
    }
}