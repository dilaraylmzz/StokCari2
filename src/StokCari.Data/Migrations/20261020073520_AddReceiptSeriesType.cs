using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StokCari.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptSeriesType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Daha önce tanımlanmış seriler türsüzdü; geçerli bir enum değeri olması için Sale varsayılır.
            // Yanlış türdeki seriler gerekirse elle güncellenmelidir.
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "receipt_series",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sale");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                table: "receipt_series");
        }
    }
}
