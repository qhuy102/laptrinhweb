using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Quản_Lí_Nhà_Thuốc.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CustomerLat",
                table: "HoaDons",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CustomerLng",
                table: "HoaDons",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiaChiGiaoHang",
                table: "HoaDons",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerLat",
                table: "HoaDons");

            migrationBuilder.DropColumn(
                name: "CustomerLng",
                table: "HoaDons");

            migrationBuilder.DropColumn(
                name: "DiaChiGiaoHang",
                table: "HoaDons");
        }
    }
}
