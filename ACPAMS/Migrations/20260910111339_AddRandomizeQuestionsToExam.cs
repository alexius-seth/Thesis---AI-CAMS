using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACPAMS.Migrations
{
    /// <inheritdoc />
    public partial class AddRandomizeQuestionsToExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<bool>(
                name: "randomize_questions",
                table: "exams",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "randomize_questions",
                table: "exams");
        }
    }
}
