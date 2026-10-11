using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using ACPAMS.Data;
using ACPAMS.Models;

namespace ACPAMS.ViewModels
{
    public class StudentProfileViewModel : INotifyPropertyChanged
    {
        private readonly AppDbContext _context;
        private Student? _currentStudent;

        public Student? CurrentStudent
        {
            get => _currentStudent;
            set
            {
                if (_currentStudent != value)
                {
                    _currentStudent = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActiveButtonText));
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        public bool IsActive => CurrentStudent?.IsActive ?? false;

        public string ActiveButtonText => (CurrentStudent?.IsActive ?? true)
            ? "Remove / Deactivate"
            : "Reactivate Account";

        public ICommand ToggleStudentActiveStateCommand { get; }
        public ICommand ResetPasswordCommand { get; }

        public Action? OnNavigateBack { get; set; }

        public StudentProfileViewModel(AppDbContext? context = null)
        {
            _context = context ?? new AppDbContext();
            ToggleStudentActiveStateCommand = new AsyncRelayCommand(ToggleStudentActiveStateAsync);
            ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync);
        }

        public async Task ToggleStudentActiveStateAsync()
        {
            if (CurrentStudent == null) return;

            var student = await _context.Students
                .Include(s => s.ExamAttempts)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == CurrentStudent.StudentId);

            if (student == null) return;

            if (student.IsActive)
            {
                // Deactivate / Safe delete logic
                if (student.ExamAttempts != null && student.ExamAttempts.Any())
                {
                    var confirm = MessageBox.Show(
                        "This student has completed exam records. Deactivate this account to revoke login access while preserving historical grades?",
                        "Confirm Deactivation",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirm != MessageBoxResult.Yes) return;

                    student.IsActive = false;
                    if (student.User != null) student.User.IsActive = false;
                    await _context.SaveChangesAsync();
                    CurrentStudent.IsActive = false;
                }
                else
                {
                    var confirm = MessageBox.Show(
                        "This student has no recorded exam attempts. Permanently delete this account?",
                        "Confirm Permanent Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirm != MessageBoxResult.Yes) return;

                    var user = student.User;
                    _context.Students.Remove(student);
                    if (user != null) _context.Users.Remove(user);
                    await _context.SaveChangesAsync();
                    CurrentStudent = null;
                    NavigateBack();
                    return;
                }
            }
            else
            {
                // Reactivation flow
                var confirm = MessageBox.Show(
                    $"Reactivate account for {student.FullName} ({student.StudentId})? This will restore their access to log in and take scheduled exams.",
                    "Confirm Reactivation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;

                student.IsActive = true;
                if (student.User != null) student.User.IsActive = true;
                await _context.SaveChangesAsync();
                CurrentStudent.IsActive = true;
            }

            OnPropertyChanged(nameof(CurrentStudent));
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(ActiveButtonText));
        }

        private void NavigateBack()
        {
            OnNavigateBack?.Invoke();
            MainWindow.NavigateBackStatic();
        }

        public async Task ResetPasswordAsync()
        {
            if (CurrentStudent == null) return;

            var student = await _context.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == CurrentStudent.StudentId);

            if (student == null) return;

            var confirm = MessageBox.Show(
                $"Reset password for {student.FullName} ({student.StudentId}) to default credentials?",
                "Confirm Password Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            string tempPassword = $"Temp@{student.StudentId}";
            string hash = BCrypt.Net.BCrypt.HashPassword(tempPassword, 12);

            student.MustChangePassword = true;
            if (student.User != null)
            {
                student.User.PasswordHash = hash;
            }
            else
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == student.StudentId);
                if (user != null)
                {
                    user.PasswordHash = hash;
                }
            }

            await _context.SaveChangesAsync();

            MessageBox.Show(
                $"Password reset successfully.\n\nTemporary Credentials:\nUsername: {student.StudentId}\nPassword: {tempPassword}",
                "Reset Completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            _isExecuting = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _execute();
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}

