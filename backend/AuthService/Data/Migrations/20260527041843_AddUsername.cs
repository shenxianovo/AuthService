using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(39)",
                maxLength: 39,
                nullable: true);

            // Backfill existing users with deterministic placeholder usernames
            // ('user-' + first 8 hex chars of Id), avoiding unique constraint collisions.
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "Username" = 'user-' || substr(replace("Id"::text, '-', ''), 1, 8)
                WHERE "Username" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(39)",
                maxLength: 39,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(39)",
                oldMaxLength: 39,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");
        }
    }
}