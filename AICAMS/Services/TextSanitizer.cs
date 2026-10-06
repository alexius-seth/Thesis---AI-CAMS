using System;
using System.Text.RegularExpressions;

namespace ACPAMS.Services
{
    /// <summary>
    /// Strips personally identifiable information (PII) such as student IDs,
    /// email addresses, local user directory file paths, and code author comments
    /// from student answers prior to transmitting payloads to external AI endpoints.
    /// </summary>
    public static class TextSanitizer
    {
        private static readonly Regex EmailRegex = new(
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex StudentIdRegex = new(
            @"\b\d{2,4}-\d{3,5}(-\d{1,4})?\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UserDirectoryRegex = new(
            @"[A-Za-z]:\\[Uu]sers\\[^\s\\\/]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex AuthorHeaderRegex = new(
            @"(//|/\*|#|\*)\s*(author|student\s*name|student_name|by)\s*[:=].*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Sanitizes student input text by redacting PII before transmission
        /// to external AI services (e.g. Google Gemini).
        /// </summary>
        /// <param name="rawText">Raw student input from database or submission.</param>
        /// <returns>Sanitized string with PII replaced by redaction placeholders.</returns>
        public static string SanitizeStudentInput(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return rawText ?? string.Empty;

            string sanitized = rawText;

            // 1. Scrub source code author declarations
            sanitized = AuthorHeaderRegex.Replace(sanitized, "// [REDACTED_AUTHOR]");

            // 2. Scrub user directory paths that leak OS username
            sanitized = UserDirectoryRegex.Replace(sanitized, @"C:\Users\[REDACTED_USER]");

            // 3. Scrub email addresses
            sanitized = EmailRegex.Replace(sanitized, "[REDACTED_EMAIL]");

            // 4. Scrub student ID numbers (e.g. 12-3456-789 or 2024-12345)
            sanitized = StudentIdRegex.Replace(sanitized, "[REDACTED_ID]");

            return sanitized;
        }

        /// <summary>
        /// Checks whether the raw student input contains potential PII
        /// (author headers, user directories, email addresses, or student IDs).
        /// </summary>
        public static bool ContainsPii(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return false;

            return AuthorHeaderRegex.IsMatch(rawText)
                || UserDirectoryRegex.IsMatch(rawText)
                || EmailRegex.IsMatch(rawText)
                || StudentIdRegex.IsMatch(rawText);
        }
    }
}

