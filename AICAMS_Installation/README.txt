====================================================================
AI-CAMS: AI-assisted Centralized Assessment Monitoring System
Setup & Demonstration Media Package
University of Luzon - College of Computer Studies
====================================================================

MINIMUM SYSTEM REQUIREMENTS:
- Windows 10 / 11 (64-bit)
- USB or integrated webcam (minimum 720p recommended)
- 4 GB RAM minimum (8 GB recommended)
- Screen resolution: 1920x1080 recommended

QUICKSTART INSTALLATION:
1. Ensure prerequisite runtimes in the "Prerequisites" folder are present
   (Visual C++ 2015-2022 x64 Redistributable is required for OpenCV/Dlib).
2. Right-click "AICAMS_Setup.exe" and select "Run as administrator".
3. Follow the wizard defaults. Installation directory defaults to Program Files.
4. Launch the application via the "AI-CAMS" desktop shortcut.
5. On first launch, the interactive Setup Wizard guides you through:
   - Selecting Workstation Role (Instructor Host vs. Student Client).
   - Configuring the Host Address (<HOST_PC_OR_IP>\SQLEXPRESS).
   - Testing connectivity.
   - Reviewing & accepting institutional policies (Privacy, Kiosk, and Grading).

DEMO CREDENTIALS:
- Faculty Login:
    Username: faculty_admin
    Default Password: Admin@1234 (or Master Recovery Key: AICAMS-ADMIN-2026)
- Sample Student Login:
    Student ID: 23-1111-111 (Whitelisted Active Student)
    Default Password: Admin@1234

CENTRALIZED DATABASE INITIALIZATION:
- Database is centralized on the Instructor Host Server SQL instance.
- Seed data must only be loaded once on the Host Server:
    sqlcmd -S ".\SQLEXPRESS" -d aicams -i "Database_Backups\aicams_seed_data.sql"
- Client workstations connect to the host via the in-app Setup Wizard.

