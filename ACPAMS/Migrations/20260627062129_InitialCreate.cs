using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACPAMS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_models",
                columns: table => new
                {
                    model_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    model_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    model_version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_models", x => x.model_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    attachment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    entity_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    file_path = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    file_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.attachment_id);
                    table.ForeignKey(
                        name: "FK_attachments_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    log_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    entity_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.log_id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instructors",
                columns: table => new
                {
                    instructor_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    middle_initial = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instructors", x => x.instructor_id);
                    table.ForeignKey(
                        name: "FK_instructors_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    student_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    first_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    middle_initial = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    program = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    year_level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_students", x => x.student_id);
                    table.ForeignKey(
                        name: "FK_students_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exams",
                columns: table => new
                {
                    exam_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    instructor_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    course_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    exam_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    exam_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    duration_minutes = table.Column<int>(type: "int", nullable: true),
                    total_points = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    instructions = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exams", x => x.exam_id);
                    table.ForeignKey(
                        name: "FK_exams_instructors_instructor_id",
                        column: x => x.instructor_id,
                        principalTable: "instructors",
                        principalColumn: "instructor_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_assessments",
                columns: table => new
                {
                    assessment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    student_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    exam_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    generated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    strengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    weaknesses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ai_summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    raw_ai_response = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    model_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_assessments", x => x.assessment_id);
                    table.ForeignKey(
                        name: "FK_ai_assessments_ai_models_model_id",
                        column: x => x.model_id,
                        principalTable: "ai_models",
                        principalColumn: "model_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_assessments_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_assessments_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    exam_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    question_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    correct_answer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    points = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    requires_rubric = table.Column<bool>(type: "bit", nullable: false),
                    grading_method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    programming_language = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    starter_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    expected_output = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    order_index = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.question_id);
                    table.ForeignKey(
                        name: "FK_questions_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_exam_attempts",
                columns: table => new
                {
                    attempt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    student_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    exam_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    exam_code_used = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    total_score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ip_address = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    device_info = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_exam_attempts", x => x.attempt_id);
                    table.ForeignKey(
                        name: "FK_student_exam_attempts_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "exam_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_exam_attempts_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "student_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rubrics",
                columns: table => new
                {
                    rubric_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    question_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    total_weight = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ai_instructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_by = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubrics", x => x.rubric_id);
                    table.ForeignKey(
                        name: "FK_rubrics_instructors_created_by",
                        column: x => x.created_by,
                        principalTable: "instructors",
                        principalColumn: "instructor_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rubrics_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateTable(
                name: "student_answers",
                columns: table => new
                {
                    answer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    attempt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    question_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    answer_text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    source_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    language_used = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    execution_output = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    execution_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    obtained_points = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    graded_by = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    graded_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_manual_grade = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_answers", x => x.answer_id);
                    table.ForeignKey(
                        name: "FK_student_answers_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_answers_student_exam_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalTable: "student_exam_attempts",
                        principalColumn: "attempt_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                columns: table => new
                {
                    criteria_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    rubric_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    criteria_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    max_score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    weight_percentage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    order_index = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_criteria", x => x.criteria_id);
                    table.ForeignKey(
                        name: "FK_rubric_criteria_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "rubric_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_evaluations",
                columns: table => new
                {
                    evaluation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    answer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    model_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    rubric_used_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    evaluated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    confidence_score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    justification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ai_response_raw = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    prompt_version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    final_adjusted_score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    reviewed_by = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    review_notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_evaluations", x => x.evaluation_id);
                    table.ForeignKey(
                        name: "FK_ai_evaluations_ai_models_model_id",
                        column: x => x.model_id,
                        principalTable: "ai_models",
                        principalColumn: "model_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_evaluations_rubrics_rubric_used_id",
                        column: x => x.rubric_used_id,
                        principalTable: "rubrics",
                        principalColumn: "rubric_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_evaluations_student_answers_answer_id",
                        column: x => x.answer_id,
                        principalTable: "student_answers",
                        principalColumn: "answer_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_jobs",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    answer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    model_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    priority = table.Column<int>(type: "int", nullable: true),
                    attempt_count = table.Column<int>(type: "int", nullable: false),
                    last_error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_jobs", x => x.job_id);
                    table.ForeignKey(
                        name: "FK_ai_jobs_ai_models_model_id",
                        column: x => x.model_id,
                        principalTable: "ai_models",
                        principalColumn: "model_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_jobs_student_answers_answer_id",
                        column: x => x.answer_id,
                        principalTable: "student_answers",
                        principalColumn: "answer_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rubric_levels",
                columns: table => new
                {
                    level_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    criteria_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    level_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    score_percentage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    descriptor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    order_index = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_levels", x => x.level_id);
                    table.ForeignKey(
                        name: "FK_rubric_levels_rubric_criteria_criteria_id",
                        column: x => x.criteria_id,
                        principalTable: "rubric_criteria",
                        principalColumn: "criteria_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_rubric_evaluations",
                columns: table => new
                {
                    rubric_eval_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    evaluation_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    criteria_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    level_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    assigned_score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    confidence_score = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_rubric_evaluations", x => x.rubric_eval_id);
                    table.ForeignKey(
                        name: "FK_ai_rubric_evaluations_ai_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "ai_evaluations",
                        principalColumn: "evaluation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_rubric_evaluations_rubric_criteria_criteria_id",
                        column: x => x.criteria_id,
                        principalTable: "rubric_criteria",
                        principalColumn: "criteria_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_rubric_evaluations_rubric_levels_level_id",
                        column: x => x.level_id,
                        principalTable: "rubric_levels",
                        principalColumn: "level_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_assessments_exam_id",
                table: "ai_assessments",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_assessments_model_id",
                table: "ai_assessments",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_assessments_student_id",
                table: "ai_assessments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_evaluations_answer_id",
                table: "ai_evaluations",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_evaluations_model_id",
                table: "ai_evaluations",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_evaluations_rubric_used_id",
                table: "ai_evaluations",
                column: "rubric_used_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_jobs_answer_id",
                table: "ai_jobs",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_jobs_model_id",
                table: "ai_jobs",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_rubric_evaluations_criteria_id",
                table: "ai_rubric_evaluations",
                column: "criteria_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_rubric_evaluations_evaluation_id",
                table: "ai_rubric_evaluations",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_rubric_evaluations_level_id",
                table: "ai_rubric_evaluations",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_uploaded_by",
                table: "attachments",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempt_logs_attempt_id",
                table: "exam_attempt_logs",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_exams_exam_code",
                table: "exams",
                column: "exam_code",
                unique: true,
                filter: "[exam_code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_exams_instructor_id",
                table: "exams",
                column: "instructor_id");

            migrationBuilder.CreateIndex(
                name: "IX_instructors_user_id",
                table: "instructors",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_questions_exam_id",
                table: "questions",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_rubric_criteria_rubric_id",
                table: "rubric_criteria",
                column: "rubric_id");

            migrationBuilder.CreateIndex(
                name: "IX_rubric_levels_criteria_id",
                table: "rubric_levels",
                column: "criteria_id");

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_created_by",
                table: "rubrics",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_question_id",
                table: "rubrics",
                column: "question_id",
                unique: true,
                filter: "[question_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_student_answers_attempt_id",
                table: "student_answers",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_answers_question_id",
                table: "student_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_exam_attempts_exam_id",
                table: "student_exam_attempts",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_exam_attempts_student_id",
                table: "student_exam_attempts",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_students_user_id",
                table: "students",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_assessments");

            migrationBuilder.DropTable(
                name: "ai_jobs");

            migrationBuilder.DropTable(
                name: "ai_rubric_evaluations");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "exam_attempt_logs");

            migrationBuilder.DropTable(
                name: "ai_evaluations");

            migrationBuilder.DropTable(
                name: "rubric_levels");

            migrationBuilder.DropTable(
                name: "ai_models");

            migrationBuilder.DropTable(
                name: "student_answers");

            migrationBuilder.DropTable(
                name: "rubric_criteria");

            migrationBuilder.DropTable(
                name: "student_exam_attempts");

            migrationBuilder.DropTable(
                name: "rubrics");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "exams");

            migrationBuilder.DropTable(
                name: "instructors");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
