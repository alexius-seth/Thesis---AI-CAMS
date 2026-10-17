-- ====================================================================
-- AI-CAMS Idempotent Seed Data Script
-- Purpose: Populates test accounts, assessments, attempts, and anomaly logs.
-- Target Engine: Microsoft SQL Server 2025 Express
-- Host Instance: DESKTOP-QD67JHP\SQLEXPRESS | Database: aicamsdb
-- ====================================================================

USE [aicamsdb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

-- --------------------------------------------------------------------
-- 1. FACULTY / INSTRUCTOR ACCOUNTS
-- --------------------------------------------------------------------
-- Faculty Admin User
-- Default password: "Admin@1234" (BCrypt hash)
IF NOT EXISTS (SELECT 1 FROM [dbo].[users] WHERE [username] = 'faculty_admin')
BEGIN
    INSERT INTO [dbo].[users] ([user_id], [role], [username], [password_hash], [created_at], [is_active])
    VALUES (
        'E1111111-1111-1111-1111-111111111111',
        'instructor',
        'faculty_admin',
        '$2a$11$39OAczIIvY2AgXK3rHQ1MuMRBB0WkZA9qhKAShsprq1twB/8QbyYK',
        GETDATE(),
        1
    );
END

-- Faculty Admin Instructor Profile
IF NOT EXISTS (SELECT 1 FROM [dbo].[instructors] WHERE [instructor_id] = 'INST-ADMIN-01')
BEGIN
    INSERT INTO [dbo].[instructors] ([instructor_id], [user_id], [first_name], [last_name], [middle_initial], [department], [created_at])
    VALUES (
        'INST-ADMIN-01',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = 'faculty_admin'),
        'Faculty',
        'Administrator',
        'M',
        'Department of Computer Studies',
        GETDATE()
    );
END

-- --------------------------------------------------------------------
-- 2. SAMPLE STUDENT ACCOUNTS
-- --------------------------------------------------------------------

-- Student 1: Active Student (23-1111-111)
IF NOT EXISTS (SELECT 1 FROM [dbo].[users] WHERE [username] = '23-1111-111')
BEGIN
    INSERT INTO [dbo].[users] ([user_id], [role], [username], [password_hash], [created_at], [is_active])
    VALUES (
        'EF1EB201-7627-488B-88DB-47AD3171B82F',
        'student',
        '23-1111-111',
        '$2a$11$6NaDqREz7xKvvtfSUUNhbeORJPO3FnkYXF.NqqzCiryrjmARUpx2C',
        GETDATE(),
        1
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[students] WHERE [student_id] = '23-1111-111')
BEGIN
    INSERT INTO [dbo].[students] ([student_id], [user_id], [first_name], [last_name], [middle_initial], [program], [year_level], [created_at], [is_active], [must_change_password])
    VALUES (
        '23-1111-111',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-1111-111'),
        'Alexius',
        'Seth',
        'A',
        'BS Computer Science',
        '3rd Year',
        GETDATE(),
        1,
        0
    );
END

-- Student 2: First-Time Login Student (Must change password on first login) (23-2222-222)
IF NOT EXISTS (SELECT 1 FROM [dbo].[users] WHERE [username] = '23-2222-222')
BEGIN
    INSERT INTO [dbo].[users] ([user_id], [role], [username], [password_hash], [created_at], [is_active])
    VALUES (
        'E2222222-2222-2222-2222-222222222222',
        'student',
        '23-2222-222',
        '$2a$11$6NaDqREz7xKvvtfSUUNhbeORJPO3FnkYXF.NqqzCiryrjmARUpx2C',
        GETDATE(),
        1
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[students] WHERE [student_id] = '23-2222-222')
BEGIN
    INSERT INTO [dbo].[students] ([student_id], [user_id], [first_name], [last_name], [middle_initial], [program], [year_level], [created_at], [is_active], [must_change_password])
    VALUES (
        '23-2222-222',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-2222-222'),
        'Maria',
        'Santos',
        'L',
        'BS Information Technology',
        '2nd Year',
        GETDATE(),
        1,
        1
    );
END

-- Student 3: Inactive / Archived Student (23-3333-333)
IF NOT EXISTS (SELECT 1 FROM [dbo].[users] WHERE [username] = '23-3333-333')
BEGIN
    INSERT INTO [dbo].[users] ([user_id], [role], [username], [password_hash], [created_at], [is_active])
    VALUES (
        'E3333333-3333-3333-3333-333333333333',
        'student',
        '23-3333-333',
        '$2a$11$6NaDqREz7xKvvtfSUUNhbeORJPO3FnkYXF.NqqzCiryrjmARUpx2C',
        GETDATE(),
        0
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[students] WHERE [student_id] = '23-3333-333')
BEGIN
    INSERT INTO [dbo].[students] ([student_id], [user_id], [first_name], [last_name], [middle_initial], [program], [year_level], [created_at], [is_active], [must_change_password])
    VALUES (
        '23-3333-333',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-3333-333'),
        'Juan',
        'Dela Cruz',
        'P',
        'BS Computer Science',
        '4th Year',
        GETDATE(),
        0,
        0
    );
END

-- Student 4: Active Student (23-4444-444)
IF NOT EXISTS (SELECT 1 FROM [dbo].[users] WHERE [username] = '23-4444-444')
BEGIN
    INSERT INTO [dbo].[users] ([user_id], [role], [username], [password_hash], [created_at], [is_active])
    VALUES (
        '6E22C832-FC98-4639-BB60-F166AEA311B4',
        'student',
        '23-4444-444',
        '$2a$12$O8xR3iTkM1ablTIbpsLgUOs3Zfo4E2pbwlafuf.ovKbLYuyVLpLg6',
        GETDATE(),
        1
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[students] WHERE [student_id] = '23-4444-444')
BEGIN
    INSERT INTO [dbo].[students] ([student_id], [user_id], [first_name], [last_name], [middle_initial], [program], [year_level], [created_at], [is_active], [must_change_password])
    VALUES (
        '23-4444-444',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-4444-444'),
        'Rod',
        'Rigo',
        'B',
        'BS Computer Science',
        '3rd Year',
        GETDATE(),
        1,
        0
    );
END

-- --------------------------------------------------------------------
-- 3. MOCK ASSESSMENTS (1 Active, 1 Archived)
-- --------------------------------------------------------------------

-- Assessment 1: ACTIVE EXAM (CCS415-DEMO-LIVE)
IF NOT EXISTS (SELECT 1 FROM [dbo].[exams] WHERE [exam_code] = 'CCS415-DEMO-LIVE')
BEGIN
    INSERT INTO [dbo].[exams] ([exam_id], [instructor_id], [course_code], [exam_code], [exam_name], [created_at], [duration_minutes], [total_points], [status], [instructions], [randomize_questions])
    VALUES (
        'A1111111-AAAA-AAAA-AAAA-111111111111',
        'INST-ADMIN-01',
        'CCS415',
        'CCS415-DEMO-LIVE',
        'Object-Oriented Programming Midterm Examination',
        DATEADD(DAY, -1, GETDATE()),
        60,
        50.00,
        'active',
        'Ensure your face remains directly visible to the webcam. Navigating away, switching windows, or opening third-party tools will be recorded in the audit log.',
        0
    );

    -- Question 1 for Active Exam (Multiple Choice)
    INSERT INTO [dbo].[questions] ([question_id], [exam_id], [question_type], [text], [correct_answer], [points], [requires_rubric], [grading_method], [order_index], [created_at], [section_name])
    VALUES (
        'A1111111-1111-0000-0000-000000000001',
        'A1111111-AAAA-AAAA-AAAA-111111111111',
        'multiple_choice',
        'Which OOP principle is primarily demonstrated by bundling data and methods that operate on that data within a single class while restricting direct external access?',
        'Encapsulation',
        20.00,
        0,
        'objective',
        1,
        GETDATE(),
        'Part 1: Concept Verification'
    );

    -- Options for Question 1
    INSERT INTO [dbo].[question_options] ([option_id], [question_id], [option_text], [order_index])
    VALUES
        ('A1000001-0001-0000-0000-000000000001', 'A1111111-1111-0000-0000-000000000001', 'Encapsulation', 1),
        ('A1000001-0001-0000-0000-000000000002', 'A1111111-1111-0000-0000-000000000001', 'Polymorphism', 2),
        ('A1000001-0001-0000-0000-000000000003', 'A1111111-1111-0000-0000-000000000001', 'Inheritance', 3),
        ('A1000001-0001-0000-0000-000000000004', 'A1111111-1111-0000-0000-000000000001', 'Abstraction', 4);

    -- Question 2 for Active Exam (Identification)
    INSERT INTO [dbo].[questions] ([question_id], [exam_id], [question_type], [text], [correct_answer], [points], [requires_rubric], [grading_method], [order_index], [created_at], [section_name])
    VALUES (
        'A1111111-1111-0000-0000-000000000002',
        'A1111111-AAAA-AAAA-AAAA-111111111111',
        'identification',
        'What keyword in C# is used to explicitly declare that a method in a derived class overrides a virtual or abstract base method?',
        'override',
        30.00,
        0,
        'objective',
        2,
        GETDATE(),
        'Part 2: Applied Syntax'
    );
END

-- Assessment 2: ARCHIVED EXAM WITH COMPLETED LOGS (CCS415-DEMO-ARCH)
IF NOT EXISTS (SELECT 1 FROM [dbo].[exams] WHERE [exam_code] = 'CCS415-DEMO-ARCH')
BEGIN
    INSERT INTO [dbo].[exams] ([exam_id], [instructor_id], [course_code], [exam_code], [exam_name], [created_at], [duration_minutes], [total_points], [status], [instructions], [randomize_questions])
    VALUES (
        'B2222222-BBBB-BBBB-BBBB-222222222222',
        'INST-ADMIN-01',
        'CCS415',
        'CCS415-DEMO-ARCH',
        'Object-Oriented Programming Final Examination',
        DATEADD(DAY, -10, GETDATE()),
        90,
        100.00,
        'archived',
        'Comprehensive assessment covering polymorphism, design patterns, and multithreading.',
        0
    );

    -- Question 1 for Archived Exam
    INSERT INTO [dbo].[questions] ([question_id], [exam_id], [question_type], [text], [correct_answer], [points], [requires_rubric], [grading_method], [order_index], [created_at], [section_name])
    VALUES (
        'B2222222-2222-0000-0000-000000000001',
        'B2222222-BBBB-BBBB-BBBB-222222222222',
        'multiple_choice',
        'Which behavioral design pattern defines a one-to-many dependency between objects so that when one object changes state, all its dependents are notified automatically?',
        'Observer',
        40.00,
        0,
        'objective',
        1,
        DATEADD(DAY, -10, GETDATE()),
        'Design Patterns'
    );

    INSERT INTO [dbo].[question_options] ([option_id], [question_id], [option_text], [order_index])
    VALUES
        ('B2000001-0001-0000-0000-000000000001', 'B2222222-2222-0000-0000-000000000001', 'Observer', 1),
        ('B2000001-0001-0000-0000-000000000002', 'B2222222-2222-0000-0000-000000000001', 'Factory Method', 2),
        ('B2000001-0001-0000-0000-000000000003', 'B2222222-2222-0000-0000-000000000001', 'Singleton', 3),
        ('B2000001-0001-0000-0000-000000000004', 'B2222222-2222-0000-0000-000000000001', 'Adapter', 4);

    -- Question 2 for Archived Exam
    INSERT INTO [dbo].[questions] ([question_id], [exam_id], [question_type], [text], [correct_answer], [points], [requires_rubric], [grading_method], [order_index], [created_at], [section_name])
    VALUES (
        'B2222222-2222-0000-0000-000000000002',
        'B2222222-BBBB-BBBB-BBBB-222222222222',
        'identification',
        'What C# interface is implemented to define custom equality comparison between instances of a reference type?',
        'IEquatable',
        60.00,
        0,
        'objective',
        2,
        DATEADD(DAY, -10, GETDATE()),
        'Advanced Interfaces'
    );
END

-- --------------------------------------------------------------------
-- 4. COMPLETED ATTEMPTS FOR ARCHIVED EXAM
-- --------------------------------------------------------------------

-- Attempt 1: Student 23-1111-111 (Completed, Low/Medium Risk)
IF NOT EXISTS (SELECT 1 FROM [dbo].[student_exam_attempts] WHERE [attempt_id] = 'C1111111-CCCC-CCCC-CCCC-111111111111')
BEGIN
    INSERT INTO [dbo].[student_exam_attempts] (
        [attempt_id], [student_id], [exam_id], [exam_code_used], [started_at], [submitted_at], [status], [total_score], [extra_time_minutes], [ip_address], [device_info]
    )
    VALUES (
        'C1111111-CCCC-CCCC-CCCC-111111111111',
        '23-1111-111',
        'B2222222-BBBB-BBBB-BBBB-222222222222',
        'CCS415-DEMO-ARCH',
        DATEADD(DAY, -8, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME))),
        DATEADD(DAY, -8, DATEADD(HOUR, 10, CAST(CAST(GETDATE() AS DATE) AS DATETIME))),
        'graded',
        100.00,
        0,
        '192.168.1.101',
        'LAB-WORKSTATION-01'
    );
END

-- Attempt 2: Student 23-4444-444 (Completed, High Risk - Multiple Flags)
IF NOT EXISTS (SELECT 1 FROM [dbo].[student_exam_attempts] WHERE [attempt_id] = 'C2222222-CCCC-CCCC-CCCC-222222222222')
BEGIN
    INSERT INTO [dbo].[student_exam_attempts] (
        [attempt_id], [student_id], [exam_id], [exam_code_used], [started_at], [submitted_at], [status], [total_score], [extra_time_minutes], [ip_address], [device_info]
    )
    VALUES (
        'C2222222-CCCC-CCCC-CCCC-222222222222',
        '23-4444-444',
        'B2222222-BBBB-BBBB-BBBB-222222222222',
        'CCS415-DEMO-ARCH',
        DATEADD(DAY, -8, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME))),
        DATEADD(DAY, -8, DATEADD(MINUTE, 75, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        'graded',
        40.00,
        0,
        '192.168.1.104',
        'LAB-WORKSTATION-04'
    );
END

-- --------------------------------------------------------------------
-- 5. HISTORICAL ANOMALY MONITORING RECORDS (AUDIT LOGS)
-- --------------------------------------------------------------------

-- Logs for Attempt 1 (Student 23-1111-111): 2 anomalies (Low/Medium Risk)
IF NOT EXISTS (SELECT 1 FROM [dbo].[audit_logs] WHERE [log_id] = 'D1000001-1111-0000-0000-000000000001')
BEGIN
    INSERT INTO [dbo].[audit_logs] ([log_id], [user_id], [action], [entity_type], [entity_id], [details], [ip_address], [created_at], [is_dismissed], [dismissal_reason])
    VALUES (
        'D1000001-1111-0000-0000-000000000001',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-1111-111'),
        'NoFaceDetected',
        'StudentExamAttempt',
        'c1111111-cccc-cccc-cccc-111111111111',
        'No face detected - candidate leaned outside camera viewing angle for 3 seconds.',
        '192.168.1.101',
        DATEADD(DAY, -8, DATEADD(MINUTE, 15, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        0,
        NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[audit_logs] WHERE [log_id] = 'D1000001-1111-0000-0000-000000000002')
BEGIN
    INSERT INTO [dbo].[audit_logs] ([log_id], [user_id], [action], [entity_type], [entity_id], [details], [ip_address], [created_at], [is_dismissed], [dismissal_reason])
    VALUES (
        'D1000001-1111-0000-0000-000000000002',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-1111-111'),
        'WindowFocusLost',
        'StudentExamAttempt',
        'c1111111-cccc-cccc-cccc-111111111111',
        'Exam window lost focus temporarily.',
        '192.168.1.101',
        DATEADD(DAY, -8, DATEADD(MINUTE, 32, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        0,
        NULL
    );
END

-- Logs for Attempt 2 (Student 23-4444-444): 6 anomalies (High Risk >= 5 flags)
IF NOT EXISTS (SELECT 1 FROM [dbo].[audit_logs] WHERE [log_id] = 'D2000002-2222-0000-0000-000000000001')
BEGIN
    INSERT INTO [dbo].[audit_logs] ([log_id], [user_id], [action], [entity_type], [entity_id], [details], [ip_address], [created_at], [is_dismissed], [dismissal_reason])
    VALUES (
        'D2000002-2222-0000-0000-000000000001',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-4444-444'),
        'MultipleFacesDetected',
        'StudentExamAttempt',
        'c2222222-cccc-cccc-cccc-222222222222',
        'Multiple faces detected in webcam feed. Second individual visible in background.',
        '192.168.1.104',
        DATEADD(DAY, -8, DATEADD(MINUTE, 8, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        0,
        NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[audit_logs] WHERE [log_id] = 'D2000002-2222-0000-0000-000000000002')
BEGIN
    INSERT INTO [dbo].[audit_logs] ([log_id], [user_id], [action], [entity_type], [entity_id], [details], [ip_address], [created_at], [is_dismissed], [dismissal_reason])
    VALUES (
        'D2000002-2222-0000-0000-000000000002',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-4444-444'),
        'AltTabAttempt',
        'StudentExamAttempt',
        'c2222222-cccc-cccc-cccc-222222222222',
        'Alt+Tab hotkey combination intercepted and blocked.',
        '192.168.1.104',
        DATEADD(DAY, -8, DATEADD(MINUTE, 14, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        0,
        NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[audit_logs] WHERE [log_id] = 'D2000002-2222-0000-0000-000000000003')
BEGIN
    INSERT INTO [dbo].[audit_logs] ([log_id], [user_id], [action], [entity_type], [entity_id], [details], [ip_address], [created_at], [is_dismissed], [dismissal_reason])
    VALUES (
        'D2000002-2222-0000-0000-000000000003',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-4444-444'),
        'ClipboardAttempt',
        'StudentExamAttempt',
        'c2222222-cccc-cccc-cccc-222222222222',
        'Unauthorized clipboard paste operation intercepted.',
        '192.168.1.104',
        DATEADD(DAY, -8, DATEADD(MINUTE, 22, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        0,
        NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[audit_logs] WHERE [log_id] = 'D2000002-2222-0000-0000-000000000004')
BEGIN
    INSERT INTO [dbo].[audit_logs] ([log_id], [user_id], [action], [entity_type], [entity_id], [details], [ip_address], [created_at], [is_dismissed], [dismissal_reason])
    VALUES (
        'D2000002-2222-0000-0000-000000000004',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-4444-444'),
        'WindowFocusLost',
        'StudentExamAttempt',
        'c2222222-cccc-cccc-cccc-222222222222',
        'Exam window lost focus. External process brought to foreground.',
        '192.168.1.104',
        DATEADD(DAY, -8, DATEADD(MINUTE, 29, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        0,
        NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[audit_logs] WHERE [log_id] = 'D2000002-2222-0000-0000-000000000005')
BEGIN
    INSERT INTO [dbo].[audit_logs] ([log_id], [user_id], [action], [entity_type], [entity_id], [details], [ip_address], [created_at], [is_dismissed], [dismissal_reason])
    VALUES (
        'D2000002-2222-0000-0000-000000000005',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-4444-444'),
        'GazeDiverted',
        'StudentExamAttempt',
        'c2222222-cccc-cccc-cccc-222222222222',
        'Gaze diverted away from screen exceeding threshold duration.',
        '192.168.1.104',
        DATEADD(DAY, -8, DATEADD(MINUTE, 41, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        0,
        NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[audit_logs] WHERE [log_id] = 'D2000002-2222-0000-0000-000000000006')
BEGIN
    INSERT INTO [dbo].[audit_logs] ([log_id], [user_id], [action], [entity_type], [entity_id], [details], [ip_address], [created_at], [is_dismissed], [dismissal_reason])
    VALUES (
        'D2000002-2222-0000-0000-000000000006',
        (SELECT [user_id] FROM [dbo].[users] WHERE [username] = '23-4444-444'),
        'NoFaceDetected',
        'StudentExamAttempt',
        'c2222222-cccc-cccc-cccc-222222222222',
        'Candidate absent from webcam frame for 8 seconds.',
        '192.168.1.104',
        DATEADD(DAY, -8, DATEADD(MINUTE, 52, DATEADD(HOUR, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)))),
        0,
        NULL
    );
END

PRINT 'AI-CAMS seed data populated successfully.';
