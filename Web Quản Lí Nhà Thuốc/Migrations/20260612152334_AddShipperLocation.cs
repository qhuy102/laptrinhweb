using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Quản_Lí_Nhà_Thuốc.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.AddColumn<string>(
            //     name: "NhomDieuTri",
            //     table: "Thuocs",
            //     type: "nvarchar(100)",
            //     maxLength: 100,
            //     nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ShipperLat",
                table: "HoaDons",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ShipperLng",
                table: "HoaDons",
                type: "float",
                nullable: true);

            // migrationBuilder.CreateTable(
            //     name: "LichUongThuocs",
            //     columns: table => new
            //     {
            //         Id = table.Column<int>(type: "int", nullable: false)
            //             .Annotation("SqlServer:Identity", "1, 1"),
            //         UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
            //         TenThuoc = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
            //         LieuDung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            //         GioUong = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
            //         GhiChu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            //         DaUongHomNay = table.Column<bool>(type: "bit", nullable: false),
            //         NgayCapNhat = table.Column<DateTime>(type: "datetime2", nullable: false)
            //     },
            //     constraints: table =>
            //     {
            //         table.PrimaryKey("PK_LichUongThuocs", x => x.Id);
            //     });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LichUongThuocs");

            migrationBuilder.DropColumn(
                name: "NhomDieuTri",
                table: "Thuocs");

            migrationBuilder.DropColumn(
                name: "ShipperLat",
                table: "HoaDons");

            migrationBuilder.DropColumn(
                name: "ShipperLng",
                table: "HoaDons");
        }
    }
}
