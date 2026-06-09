using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Quản_Lí_Nhà_Thuốc.Migrations
{
    /// <inheritdoc />
    public partial class AddDiemTichLuy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiemTichLuy",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiemTichLuy",
                table: "AspNetUsers");
        }
    }
}
