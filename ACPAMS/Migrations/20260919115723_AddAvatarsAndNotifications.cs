using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACPAMS.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_attempt_logs");

            migrationBuilder.DropColumn(
                name: "expected_output",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "title",
                table: "exams");

            migrationBuilder.DropColumn(
                name: "final_adjusted_score",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "prompt_version",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "review_notes",
                table: "ai_evaluations");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                table: "ai_evaluations");

            migrationBuilder.AddColumn<byte[]>(
                name: "avatar_image",
                table: "students",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "avatar_image",
                table: "instructors",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    is_read = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    target_route = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    target_entity_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.notification_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropColumn(
                name: "avatar_image",
                table: "students");

            migrationBuilder.DropColumn(
                name: "avatar_image",
                table: "instructors");

            migrationBuilder.AddColumn<string>(
                name: "expected_output",
                table: "questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "exams",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "final_adjusted_score",
                table: "ai_evaluations",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt_version",
                table: "ai_evaluations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_notes",
                table: "ai_evaluations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reviewed_by",
                table: "ai_evaluations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exam_attempt_logs",
                columns: table => new
                {
                    log_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    attempt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    action_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_attempt_logs", x => x.log_id);
                    table.ForeignKey(
                        name: "FK_exam_attempt_logs_student_exam_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalTable: "student_exam_attempts",
                        principalColumn: "attempt_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempt_logs_attempt_id",
                table: "exam_attempt_logs",
                column: "attempt_id");
        }
    }
}
