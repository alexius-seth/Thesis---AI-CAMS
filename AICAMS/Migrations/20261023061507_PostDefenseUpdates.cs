using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ACPAMS.Migrations
{
    /// <inheritdoc />
    public partial class PostDefenseUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('students') AND name = 'is_active'
                )
                BEGIN
                    ALTER TABLE students ADD is_active BIT NOT NULL DEFAULT 1;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('students') AND name = 'must_change_password'
                )
                BEGIN
                    ALTER TABLE students ADD must_change_password BIT NOT NULL DEFAULT 0;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('student_exam_attempts') AND name = 'extra_time_minutes'
                )
                BEGIN
                    ALTER TABLE student_exam_attempts ADD extra_time_minutes INT NOT NULL DEFAULT 0;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('exams') AND name = 'academic_year'
                )
                BEGIN
                    ALTER TABLE exams ADD academic_year NVARCHAR(20) NULL;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('exams') AND name = 'semester'
                )
                BEGIN
                    ALTER TABLE exams ADD semester NVARCHAR(20) NULL;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('exams') AND name = 'section'
                )
                BEGIN
                    ALTER TABLE exams ADD section NVARCHAR(50) NULL;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('exams') AND name = 'class_schedule'
                )
                BEGIN
                    ALTER TABLE exams ADD class_schedule NVARCHAR(50) NULL;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('exams') AND name = 'requires_approval'
                )
                BEGIN
                    ALTER TABLE exams ADD requires_approval BIT NOT NULL DEFAULT 0;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('audit_logs') AND name = 'dismissal_reason'
                )
                BEGIN
                    ALTER TABLE audit_logs ADD dismissal_reason NVARCHAR(255) NULL;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID('audit_logs') AND name = 'is_dismissed'
                )
                BEGIN
                    ALTER TABLE audit_logs ADD is_dismissed BIT NOT NULL DEFAULT 0;
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.tables 
                    WHERE name = 'exam_authorized_students'
                )
                BEGIN
                    CREATE TABLE dbo.exam_authorized_students (
                        authorization_id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
                        exam_id UNIQUEIDENTIFIER NOT NULL,
                        student_id NVARCHAR(50) NOT NULL,
                        status NVARCHAR(30) NOT NULL DEFAULT 'PendingApproval',
                        requested_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        valid_until DATETIME2 NULL,
                        approved_by_instructor_id NVARCHAR(50) NULL,
                        CONSTRAINT FK_exam_authorized_students_exams FOREIGN KEY (exam_id) REFERENCES dbo.exams(exam_id) ON DELETE CASCADE,
                        CONSTRAINT FK_exam_authorized_students_students FOREIGN KEY (student_id) REFERENCES dbo.students(student_id) ON DELETE NO ACTION
                    );

                    CREATE INDEX IX_exam_authorized_students_exam_id ON dbo.exam_authorized_students(exam_id);
                    CREATE INDEX IX_exam_authorized_students_student_id ON dbo.exam_authorized_students(student_id);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'exam_authorized_students')
                BEGIN
                    DROP TABLE dbo.exam_authorized_students;
                END
            ");
        }
    }
}

