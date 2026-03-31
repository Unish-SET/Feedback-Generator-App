using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedBackApp.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesAndUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DB-01: Non-clustered indexes on Answer FK columns used heavily in analytics queries.
            // SQL Server does not auto-create NCIs on FK columns.
            migrationBuilder.CreateIndex(
                name: "IX_Answers_QuestionId",
                table: "Answers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Answers_ResponseId",
                table: "Answers",
                column: "ResponseId");

            // DB-03: Filtered unique indexes on SurveyAccess to prevent race-condition duplicates.
            // Mirror the pattern already used on SurveyResponses (SurveyId+UserId WHERE UserId IS NOT NULL).
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Answers_QuestionId",
                table: "Answers");

            migrationBuilder.DropIndex(
                name: "IX_Answers_ResponseId",
                table: "Answers");

            migrationBuilder.DropIndex(
                name: "IX_SurveyAccesses_SurveyId_UserId",
                table: "SurveyAccesses");

            migrationBuilder.DropIndex(
                name: "IX_SurveyAccesses_SurveyId_Email",
                table: "SurveyAccesses");
        }
    }
}
