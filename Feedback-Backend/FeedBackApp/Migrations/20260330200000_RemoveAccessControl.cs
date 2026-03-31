using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedBackApp.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop unique indexes on SurveyAccesses before dropping the table
            migrationBuilder.DropIndex(
                name: "IX_SurveyAccesses_SurveyId_UserId",
                table: "SurveyAccesses");

            migrationBuilder.DropIndex(
                name: "IX_SurveyAccesses_SurveyId_Email",
                table: "SurveyAccesses");

            // Drop foreign keys on SurveyAccesses
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyAccesses_Surveys_SurveyId",
                table: "SurveyAccesses");

            migrationBuilder.DropForeignKey(
                name: "FK_SurveyAccesses_Users_UserId",
                table: "SurveyAccesses");

            // Drop the SurveyAccesses table entirely
            migrationBuilder.DropTable(name: "SurveyAccesses");

            // Remove access control columns from Surveys
            migrationBuilder.DropColumn(name: "IsPublic",     table: "Surveys");
            migrationBuilder.DropColumn(name: "RequireLogin", table: "Surveys");
            migrationBuilder.DropColumn(name: "IsRestricted", table: "Surveys");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore access control columns on Surveys
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Surveys",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireLogin",
                table: "Surveys",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                table: "Surveys",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Recreate SurveyAccesses table
            migrationBuilder.CreateTable(
                name: "SurveyAccesses",
                columns: table => new
                {
                    Id        = table.Column<int>(type: "int", nullable: false)
                                    .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId  = table.Column<int>(type: "int", nullable: false),
                    UserId    = table.Column<int>(type: "int", nullable: true),
                    Email     = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyAccesses_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SurveyAccesses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyAccesses_SurveyId_UserId",
                table: "SurveyAccesses",
                columns: new[] { "SurveyId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyAccesses_SurveyId_Email",
                table: "SurveyAccesses",
                columns: new[] { "SurveyId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");
        }
    }
}
