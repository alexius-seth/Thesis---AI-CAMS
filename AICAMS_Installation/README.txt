====================================================================
AI-CAMS: AI-assisted Centralized Assessment Monitoring System
Setup & Demonstration Media
====================================================================

MINIMUM SYSTEM REQUIREMENTS:
- Windows 10 / 11 (64-bit)
- USB or integrated webcam
- 4 GB RAM minimum (8 GB recommended)
- Screen resolution: 1920x1080 recommended

QUICKSTART INSTALLATION:
1. Ensure prerequisite runtimes in the "Prerequisites" folder are installed 
   (VC++ 2015-2022 x64 Redistributable is required for OpenCV/Dlib).
2. Right-click "AICAMS_Setup.exe" and select "Run as administrator".
3. Follow the wizard defaults. Installation directory defaults to Program Files.
4. Launch the application via the "AI-CAMS" desktop shortcut.

DEMO CREDENTIALS:
- Faculty Login:
    Username: faculty_admin
    Default Password: (Set during initial onboarding or via master key)
- Sample Student Login:
    Student ID: 23-1111-111
    Default Password: (Prompted to change on first login if reset)

CENTRALIZED DATABASE INITIALIZATION:
- Database is centralized on Host Server: DESKTOP-QD67JHP\SQLEXPRESS
- Seed data must only be loaded once on the Host Server:
    sqlcmd -S ".\SQLEXPRESS" -d aicamsdb -i "Database_Backups\aicams_seed_data.sql"
- Client workstations connect to the host via local hostname resolution.
