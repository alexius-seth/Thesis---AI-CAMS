-- Insert test faculty user
INSERT INTO users (user_id, role, username, password_hash, created_at, is_active)
VALUES (NEWID(), 'instructor', 'testfaculty', 'Admin@1234', GETDATE(), 1);

-- Insert corresponding instructor record
INSERT INTO instructors (instructor_id, user_id, first_name, last_name, department, created_at)
SELECT 'FAC-001', user_id, 'Test', 'Faculty', 'Computer Studies', GETDATE()
FROM users WHERE username = 'testfaculty';