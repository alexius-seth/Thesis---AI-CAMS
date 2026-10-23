using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data;
using ACPAMS.Models;

namespace ACPAMS.Services
{
    public class ExamAccessService
    {
        public enum AccessResult
        {
            Allowed,
            PendingApproval,
            Denied,
            RequiresRequest
        }

        public async Task<AccessResult> CheckAccessStatusAsync(Guid examId, string studentId)
        {
            using var context = new AppDbContext();

            var exam = await context.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null) return AccessResult.Denied;

            var auth = await context.ExamAuthorizedStudents
                .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == studentId);

            if (auth != null)
            {
                if (auth.ValidUntil.HasValue && auth.ValidUntil.Value < DateTime.UtcNow)
                {
                    return AccessResult.Denied;
                }

                return auth.Status switch
                {
                    "PreApproved" => AccessResult.Allowed,
                    "Approved" => AccessResult.Allowed,
                    "PendingApproval" => AccessResult.PendingApproval,
                    "Denied" => AccessResult.Denied,
                    _ => AccessResult.Denied
                };
            }

            // If exam requires approval/whitelist and student has no record yet
            if (exam.RequiresApproval)
            {
                return AccessResult.RequiresRequest;
            }

            return AccessResult.Allowed;
        }

        public async Task<ExamAuthorizedStudent> RequestAccessAsync(Guid examId, string studentId)
        {
            using var context = new AppDbContext();

            var existing = await context.ExamAuthorizedStudents
                .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == studentId);

            if (existing != null)
            {
                if (existing.Status == "Denied" || existing.Status == "PendingApproval")
                {
                    existing.Status = "PendingApproval";
                    existing.RequestedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }
                return existing;
            }

            var auth = new ExamAuthorizedStudent
            {
                AuthorizationId = Guid.NewGuid(),
                ExamId = examId,
                StudentId = studentId,
                Status = "PendingApproval",
                RequestedAt = DateTime.UtcNow
            };

            context.ExamAuthorizedStudents.Add(auth);
            await context.SaveChangesAsync();
            return auth;
        }

        public async Task<ExamAuthorizedStudent> PreApproveStudentAsync(
            Guid examId,
            string studentId,
            string instructorId,
            DateTime? validUntil = null)
        {
            using var context = new AppDbContext();

            var existing = await context.ExamAuthorizedStudents
                .FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == studentId);

            if (existing != null)
            {
                existing.Status = "PreApproved";
                existing.ApprovedByInstructorId = instructorId;
                existing.ValidUntil = validUntil;
                await context.SaveChangesAsync();
                return existing;
            }

            var auth = new ExamAuthorizedStudent
            {
                AuthorizationId = Guid.NewGuid(),
                ExamId = examId,
                StudentId = studentId,
                Status = "PreApproved",
                ApprovedByInstructorId = instructorId,
                ValidUntil = validUntil,
                RequestedAt = DateTime.UtcNow
            };

            context.ExamAuthorizedStudents.Add(auth);
            await context.SaveChangesAsync();
            return auth;
        }

        public async Task<bool> ApproveRequestAsync(Guid authorizationId, string instructorId)
        {
            using var context = new AppDbContext();
            var auth = await context.ExamAuthorizedStudents.FirstOrDefaultAsync(a => a.AuthorizationId == authorizationId);
            if (auth == null) return false;

            auth.Status = "Approved";
            auth.ApprovedByInstructorId = instructorId;
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DenyRequestAsync(Guid authorizationId, string instructorId)
        {
            using var context = new AppDbContext();
            var auth = await context.ExamAuthorizedStudents.FirstOrDefaultAsync(a => a.AuthorizationId == authorizationId);
            if (auth == null) return false;

            auth.Status = "Denied";
            auth.ApprovedByInstructorId = instructorId;
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveAuthorizationAsync(Guid authorizationId)
        {
            using var context = new AppDbContext();
            var auth = await context.ExamAuthorizedStudents.FirstOrDefaultAsync(a => a.AuthorizationId == authorizationId);
            if (auth == null) return false;

            context.ExamAuthorizedStudents.Remove(auth);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ExamAuthorizedStudent>> GetAuthorizationsForExamAsync(Guid examId)
        {
            using var context = new AppDbContext();
            return await context.ExamAuthorizedStudents
                .Include(a => a.Student)
                .Where(a => a.ExamId == examId)
                .OrderByDescending(a => a.RequestedAt)
                .ToListAsync();
        }

        public async Task<List<ExamAuthorizedStudent>> GetPendingRequestsAsync(Guid? examId = null)
        {
            using var context = new AppDbContext();
            var query = context.ExamAuthorizedStudents
                .Include(a => a.Student)
                .Include(a => a.Exam)
                .Where(a => a.Status == "PendingApproval");

            if (examId.HasValue)
            {
                query = query.Where(a => a.ExamId == examId.Value);
            }

            return await query
                .OrderBy(a => a.RequestedAt)
                .ToListAsync();
        }
    }
}

