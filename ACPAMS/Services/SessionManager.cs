using ACPAMS.Models;

namespace ACPAMS.Services
{
    // Stores the currently logged-in user for the session
    public static class SessionManager
    {
        public static User? CurrentUser { get; set; }
        public static Student? CurrentStudent { get; set; }
        public static Instructor? CurrentInstructor { get; set; }

        public static bool IsLoggedIn => CurrentUser != null;
        public static bool IsStudent => CurrentUser?.Role == "student";
        public static bool IsInstructor => CurrentUser?.Role == "instructor";

        public static void Clear()
        {
            CurrentUser = null;
            CurrentStudent = null;
            CurrentInstructor = null;
        }
    }
}