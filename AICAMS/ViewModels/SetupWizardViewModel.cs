using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ACPAMS.Data;
using ACPAMS.Services;

namespace ACPAMS.ViewModels
{
    public class SetupWizardViewModel : INotifyPropertyChanged
    {
        private int _currentStep = 1;
        private bool _isInstructorHost = true;
        private bool _isStudentClient = false;

        private string _server = "localhost\\SQLEXPRESS";
        private string _database = "aicams";
        private bool _isWindowsAuth = true;
        private string _username = "sa";
        private string _password = string.Empty;
        private bool _trustServerCertificate = true;

        private bool _isTesting = false;
        private bool _hasTested = false;
        private bool _isTestSuccess = false;
        private string _testResultMessage = string.Empty;
        private string _troubleshootingHint = string.Empty;
        private int _tableCount = 0;

        private bool _isCameraPolicyAccepted = false;
        private bool _isKioskPolicyAccepted = false;
        private bool _isGradingPolicyAccepted = false;

        private bool _isSaving = false;
        private string _statusText = string.Empty;

        public event Action<bool>? RequestClose;

        public SetupWizardViewModel()
        {
            LoadCurrentConfiguration();

            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync(), _ => !IsTesting && !IsSaving);
            SelectHostRoleCommand = new RelayCommand(_ => SelectHostRole());
            SelectClientRoleCommand = new RelayCommand(_ => SelectClientRole());
            GoToStep1Command = new RelayCommand(_ => CurrentStep = 1);
            GoToStep2Command = new RelayCommand(_ => CurrentStep = 2, _ => IsTestSuccess);
            AcceptAllPoliciesCommand = new RelayCommand(_ => AcceptAllPolicies());
            SaveAndLaunchCommand = new RelayCommand(async _ => await SaveAndLaunchAsync(), _ => CanSaveAndLaunch);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (_currentStep != value)
                {
                    _currentStep = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStep1));
                    OnPropertyChanged(nameof(IsStep2));
                }
            }
        }

        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;

        public bool IsInstructorHost
        {
            get => _isInstructorHost;
            set
            {
                if (_isInstructorHost != value)
                {
                    _isInstructorHost = value;
                    _isStudentClient = !value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStudentClient));
                }
            }
        }

        public bool IsStudentClient
        {
            get => _isStudentClient;
            set
            {
                if (_isStudentClient != value)
                {
                    _isStudentClient = value;
                    _isInstructorHost = !value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsInstructorHost));
                }
            }
        }

        public string Server
        {
            get => _server;
            set { if (_server != value) { _server = value; InvalidateTest(); OnPropertyChanged(); } }
        }

        public string Database
        {
            get => _database;
            set { if (_database != value) { _database = value; InvalidateTest(); OnPropertyChanged(); } }
        }

        public bool IsWindowsAuth
        {
            get => _isWindowsAuth;
            set
            {
                if (_isWindowsAuth != value)
                {
                    _isWindowsAuth = value;
                    InvalidateTest();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSqlAuth));
                }
            }
        }

        public bool IsSqlAuth => !IsWindowsAuth;

        public string Username
        {
            get => _username;
            set { if (_username != value) { _username = value; InvalidateTest(); OnPropertyChanged(); } }
        }

        public string Password
        {
            get => _password;
            set { if (_password != value) { _password = value; InvalidateTest(); OnPropertyChanged(); } }
        }

        public bool TrustServerCertificate
        {
            get => _trustServerCertificate;
            set { if (_trustServerCertificate != value) { _trustServerCertificate = value; InvalidateTest(); OnPropertyChanged(); } }
        }

        public bool IsTesting
        {
            get => _isTesting;
            set { _isTesting = value; OnPropertyChanged(); }
        }

        public bool HasTested
        {
            get => _hasTested;
            set { _hasTested = value; OnPropertyChanged(); }
        }

        public bool IsTestSuccess
        {
            get => _isTestSuccess;
            set
            {
                _isTestSuccess = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoToStep2));
                OnPropertyChanged(nameof(CanSaveAndLaunch));
            }
        }

        public string TestResultMessage
        {
            get => _testResultMessage;
            set { _testResultMessage = value; OnPropertyChanged(); }
        }

        public string TroubleshootingHint
        {
            get => _troubleshootingHint;
            set { _troubleshootingHint = value; OnPropertyChanged(); }
        }

        public int TableCount
        {
            get => _tableCount;
            set { _tableCount = value; OnPropertyChanged(); }
        }

        // Policies & Compliance
        public bool IsCameraPolicyAccepted
        {
            get => _isCameraPolicyAccepted;
            set
            {
                if (_isCameraPolicyAccepted != value)
                {
                    _isCameraPolicyAccepted = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ArePoliciesAccepted));
                    OnPropertyChanged(nameof(CanSaveAndLaunch));
                }
            }
        }

        public bool IsKioskPolicyAccepted
        {
            get => _isKioskPolicyAccepted;
            set
            {
                if (_isKioskPolicyAccepted != value)
                {
                    _isKioskPolicyAccepted = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ArePoliciesAccepted));
                    OnPropertyChanged(nameof(CanSaveAndLaunch));
                }
            }
        }

        public bool IsGradingPolicyAccepted
        {
            get => _isGradingPolicyAccepted;
            set
            {
                if (_isGradingPolicyAccepted != value)
                {
                    _isGradingPolicyAccepted = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ArePoliciesAccepted));
                    OnPropertyChanged(nameof(CanSaveAndLaunch));
                }
            }
        }

        public bool ArePoliciesAccepted => IsCameraPolicyAccepted && IsKioskPolicyAccepted && IsGradingPolicyAccepted;

        public bool CanGoToStep2 => IsTestSuccess;

        public bool CanSaveAndLaunch => IsTestSuccess && ArePoliciesAccepted && !IsSaving;

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                _isSaving = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSaveAndLaunch));
            }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public ICommand TestConnectionCommand { get; }
        public ICommand SelectHostRoleCommand { get; }
        public ICommand SelectClientRoleCommand { get; }
        public ICommand GoToStep1Command { get; }
        public ICommand GoToStep2Command { get; }
        public ICommand AcceptAllPoliciesCommand { get; }
        public ICommand SaveAndLaunchCommand { get; }
        public ICommand CancelCommand { get; }

        private void LoadCurrentConfiguration()
        {
            string currentConn = ConfigurationService.GetCurrentConnectionString();
            var p = ConfigurationService.ParseConnectionString(currentConn);

            _server = p.Server;
            _database = p.Database;
            _isWindowsAuth = p.IsWindowsAuth;
            _username = p.Username;
            _password = p.Password;
            _trustServerCertificate = p.TrustServerCertificate;

            if (p.IsStudentClient)
            {
                _isStudentClient = true;
                _isInstructorHost = false;
            }
            else
            {
                _isInstructorHost = true;
                _isStudentClient = false;
            }

            if (ConfigurationService.ArePoliciesAccepted())
            {
                _isCameraPolicyAccepted = true;
                _isKioskPolicyAccepted = true;
                _isGradingPolicyAccepted = true;
            }
        }

        private void SelectHostRole()
        {
            IsInstructorHost = true;
            if (string.IsNullOrWhiteSpace(Server) || Server.Contains("192.168.") || Server.Contains("10.0."))
            {
                Server = "localhost\\SQLEXPRESS";
            }
            IsWindowsAuth = true;
        }

        private void SelectClientRole()
        {
            IsStudentClient = true;
            if (Server.Equals("localhost\\SQLEXPRESS", StringComparison.OrdinalIgnoreCase) ||
                Server.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
            {
                Server = "192.168.1.100\\SQLEXPRESS";
            }
            IsWindowsAuth = false;
            if (string.IsNullOrWhiteSpace(Username) || Username == "sa")
            {
                Username = "aicams_client";
            }
        }

        private void InvalidateTest()
        {
            HasTested = false;
            IsTestSuccess = false;
            TestResultMessage = string.Empty;
            TroubleshootingHint = string.Empty;
        }

        public async Task TestConnectionAsync()
        {
            IsTesting = true;
            HasTested = false;
            TestResultMessage = "Connecting to database server...";
            TroubleshootingHint = string.Empty;

            var p = new ConnectionParameters
            {
                Server = Server,
                Database = Database,
                IsWindowsAuth = IsWindowsAuth,
                Username = Username,
                Password = Password,
                TrustServerCertificate = TrustServerCertificate,
                IsStudentClient = IsStudentClient
            };

            string connStr = ConfigurationService.BuildConnectionString(p, timeoutSeconds: 6);
            var result = await ConfigurationService.TestConnectionAsync(connStr, timeoutSeconds: 6);

            IsTesting = false;
            HasTested = true;
            IsTestSuccess = result.Success;
            TestResultMessage = result.Message;
            TroubleshootingHint = result.TroubleshootingHint;
            TableCount = result.TableCount;
        }

        private void AcceptAllPolicies()
        {
            bool newVal = !ArePoliciesAccepted;
            IsCameraPolicyAccepted = newVal;
            IsKioskPolicyAccepted = newVal;
            IsGradingPolicyAccepted = newVal;
        }

        public async Task SaveAndLaunchAsync()
        {
            if (!CanSaveAndLaunch) return;

            IsSaving = true;
            StatusText = "Saving configuration...";

            try
            {
                var p = new ConnectionParameters
                {
                    Server = Server,
                    Database = Database,
                    IsWindowsAuth = IsWindowsAuth,
                    Username = Username,
                    Password = Password,
                    TrustServerCertificate = TrustServerCertificate,
                    IsStudentClient = IsStudentClient
                };

                string connStr = ConfigurationService.BuildConnectionString(p);
                ConfigurationService.SaveConnectionString(connStr);
                ConfigurationService.SavePoliciesAccepted(true);

                StatusText = "Initializing database schema...";
                await Task.Run(() =>
                {
                    using var context = new AppDbContext();
                    AppInitializer.Initialize(context);
                });

                StatusText = "Setup complete! Launching AI-CAMS...";
                await Task.Delay(400);

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                IsSaving = false;
                StatusText = $"Initialization error: {ex.Message}";
                TestResultMessage = $"Failed to initialize database: {ex.Message}";
                IsTestSuccess = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

