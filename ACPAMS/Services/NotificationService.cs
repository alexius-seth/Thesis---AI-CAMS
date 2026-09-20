using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data;
using ACPAMS.Models;

namespace ACPAMS.Services
{
    public class NotificationService
    {
        public async Task<int> GetUnreadCountAsync(Guid recipientUserId)
        {
            using var db = new AppDbContext();
            return await db.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == recipientUserId && !n.IsRead)
                .CountAsync();
        }

        public async Task<List<NotificationDto>> GetNotificationsAsync(Guid recipientUserId, int limit = 20)
        {
            using var db = new AppDbContext();
            var items = await db.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == recipientUserId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return items.Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                Category = n.Category,
                Severity = n.Severity,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                TargetRoute = n.TargetRoute,
                TargetEntityId = n.TargetEntityId
            }).ToList();
        }

        public async Task MarkAsReadAsync(Guid notificationId)
        {
            using var db = new AppDbContext();
            var n = await db.Notifications.FindAsync(notificationId);
            if (n != null && !n.IsRead)
            {
                n.IsRead = true;
                await db.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(Guid recipientUserId)
        {
            using var db = new AppDbContext();
            var unread = await db.Notifications
                .Where(n => n.RecipientUserId == recipientUserId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            if (unread.Count > 0)
                await db.SaveChangesAsync();
        }

        public async Task CreateNotificationAsync(Guid recipientUserId, string category, string severity, string title, string message, string? targetRoute = null, string? targetEntityId = null)
        {
            using var db = new AppDbContext();
            db.Notifications.Add(new AppNotification
            {
                NotificationId = Guid.NewGuid(),
                RecipientUserId = recipientUserId,
                Category = category,
                Severity = severity,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.Now,
                TargetRoute = targetRoute,
                TargetEntityId = targetEntityId
            });
            await db.SaveChangesAsync();
        }
    }

    public class NotificationDto
    {
        public Guid NotificationId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? TargetRoute { get; set; }
        public string? TargetEntityId { get; set; }

        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalMinutes < 1) return "just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                return CreatedAt.ToString("MMM dd");
            }
        }

        public System.Windows.Media.Brush SeverityBrush => Severity switch
        {
            "Critical" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626")),
            "Warning" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B45309")),
            "Success" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#15803D")),
            _ => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2"))
        };

        public System.Windows.Media.Brush BackgroundBrush => IsRead
            ? System.Windows.Media.Brushes.Transparent
            : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8FAFC"));
    }
}

