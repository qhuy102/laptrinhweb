using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Quản_Lí_Nhà_Thuốc.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChongChiDinh",
                table: "Thuocs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CongDung",
                table: "Thuocs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DonViCoBan",
                table: "Thuocs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HoatChat",
                table: "Thuocs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LieuLuong",
                table: "Thuocs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViTriKe",
                table: "Thuocs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThoiGian = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsSuspicious = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonThuocs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenBenhNhan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BacSiKeDon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChanDoan = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NgayKeDon = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HinhAnhDonThuoc = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonThuocs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonViQuyDois",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaThuoc = table.Column<int>(type: "int", nullable: false),
                    TenDonVi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TyLeQuyDoi = table.Column<int>(type: "int", nullable: false),
                    HeSoGia = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonViQuyDois", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonViQuyDois_Thuocs_MaThuoc",
                        column: x => x.MaThuoc,
                        principalTable: "Thuocs",
                        principalColumn: "MaThuoc",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoThuocs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaThuoc = table.Column<int>(type: "int", nullable: false),
                    SoLo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false),
                    NgaySanXuat = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HanSuDung = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoThuocs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoThuocs_Thuocs_MaThuoc",
                        column: x => x.MaThuoc,
                        principalTable: "Thuocs",
                        principalColumn: "MaThuoc",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietDonThuocs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonThuocId = table.Column<int>(type: "int", nullable: false),
                    MaThuoc = table.Column<int>(type: "int", nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: false),
                    LieuDung = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietDonThuocs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChiTietDonThuocs_DonThuocs_DonThuocId",
                        column: x => x.DonThuocId,
                        principalTable: "DonThuocs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChiTietDonThuocs_Thuocs_MaThuoc",
                        column: x => x.MaThuoc,
                        principalTable: "Thuocs",
                        principalColumn: "MaThuoc",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDonThuocs_DonThuocId",
                table: "ChiTietDonThuocs",
                column: "DonThuocId");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietDonThuocs_MaThuoc",
                table: "ChiTietDonThuocs",
                column: "MaThuoc");

            migrationBuilder.CreateIndex(
                name: "IX_DonViQuyDois_MaThuoc",
                table: "DonViQuyDois",
                column: "MaThuoc");

            migrationBuilder.CreateIndex(
                name: "IX_LoThuocs_MaThuoc",
                table: "LoThuocs",
                column: "MaThuoc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ChiTietDonThuocs");

            migrationBuilder.DropTable(
                name: "DonViQuyDois");

            migrationBuilder.DropTable(
                name: "LoThuocs");

            migrationBuilder.DropTable(
                name: "DonThuocs");

            migrationBuilder.DropColumn(
                name: "ChongChiDinh",
                table: "Thuocs");

            migrationBuilder.DropColumn(
                name: "CongDung",
                table: "Thuocs");

            migrationBuilder.DropColumn(
                name: "DonViCoBan",
                table: "Thuocs");

            migrationBuilder.DropColumn(
                name: "HoatChat",
                table: "Thuocs");

            migrationBuilder.DropColumn(
                name: "LieuLuong",
                table: "Thuocs");

            migrationBuilder.DropColumn(
                name: "ViTriKe",
                table: "Thuocs");
        }
    }
}
