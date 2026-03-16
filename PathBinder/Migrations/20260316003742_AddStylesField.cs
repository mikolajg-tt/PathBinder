using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PathBinder.Migrations
{
    /// <inheritdoc />
    public partial class AddStylesField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Styles",
                table: "Files",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Styles",
                table: "Files");
        }
    }
}
