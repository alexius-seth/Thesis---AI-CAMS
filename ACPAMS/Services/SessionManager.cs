using ACPAMS.Models;

namespace ACPAMS.Services
{
    public static class SessionManager
    {
        public static User? CurrentUser { get; set; }
        public static Student? CurrentStudent { get; set; }
        public static Instructor? CurrentInstructor { get; set; }
        public static StudentExamAttempt? CurrentAttempt { get; set; }
        public static Exam? CurrentExam { get; set; }

        public static bool IsLoggedIn => CurrentUser != null;
        public static bool IsStudent => CurrentUser?.Role == "student";
        public static bool IsInstructor => CurrentUser?.Role == "instructor";

        public static System.Windows.Media.Imaging.BitmapImage? CachedAvatar { get; set; }

        public static string Initials
        {
            get
            {
                if (CurrentInstructor != null)
                {
                    string f = !string.IsNullOrWhiteSpace(CurrentInstructor.FirstName) ? CurrentInstructor.FirstName[..1] : "";
                    string l = !string.IsNullOrWhiteSpace(CurrentInstructor.LastName) ? CurrentInstructor.LastName[..1] : "";
                    return (f + l).ToUpper();
                }
                if (CurrentStudent != null)
                {
                    string f = !string.IsNullOrWhiteSpace(CurrentStudent.FirstName) ? CurrentStudent.FirstName[..1] : "";
                    string l = !string.IsNullOrWhiteSpace(CurrentStudent.LastName) ? CurrentStudent.LastName[..1] : "";
                    return (f + l).ToUpper();
                }
                return "AC";
            }
        }

        public static event System.Action? AvatarChanged;

        public static void RefreshAvatarCache()
        {
            byte[]? bytes = CurrentInstructor?.AvatarImage ?? CurrentStudent?.AvatarImage;
            CachedAvatar = AvatarService.ByteArrayToBitmap(bytes);
            AvatarChanged?.Invoke();
        }

        public static string? CurrentInstructorId => CurrentInstructor?.InstructorId;

        public static void Clear()
        {
            CurrentUser = null;
            CurrentStudent = null;
            CurrentInstructor = null;
            CurrentAttempt = null;
            CurrentExam = null;
            CachedAvatar = null;
            AvatarChanged?.Invoke();
        }

        public static void Logout()
        {
            Clear();
            MainWindow.Logout();
        }
    }
}
