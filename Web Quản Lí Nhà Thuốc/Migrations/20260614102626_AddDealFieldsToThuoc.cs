using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Quản_Lí_Nhà_Thuốc.Migrations
{
    /// <inheritdoc />
    public partial class AddDealFieldsToThuoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiscountPercent",
                table: "Thuocs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeal",
                table: "Thuocs",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Thuocs");

            migrationBuilder.DropColumn(
                name: "IsDeal",
                table: "Thuocs");
        }
    }
}
