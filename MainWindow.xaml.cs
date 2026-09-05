using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace ClientVisitManager
{
    public class ClientEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public DateTime VisitTimestamp { get; set; } = DateTime.Now;

        public DateTime AutoThanksScheduledAt { get; set; } = DateTime.Now.AddHours(1);

        public bool IsThanksSent { get; set; } = false;
    }

    public class AppDbContext : DbContext
    {
        public DbSet<ClientEntity> Clients { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "client_visit_manager.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AppDbContext _db;

        public ObservableCollection<ClientEntity> AllClients { get; set; } = new();
        public ObservableCollection<ClientEntity> FilteredClients { get; set; } = new();
        public ObservableCollection<string> OfferPresets { get; set; } = new();

        private string _userEmail = "exonicsalon2@gmail.com";
        public string UserEmail
        {
            get => _userEmail;
            set { _userEmail = value; OnPropertyChanged(); }
        }

        private string _clientName = string.Empty;
        public string ClientName
        {
            get => _clientName;
            set { _clientName = value; OnPropertyChanged(); }
        }

        private string _clientPhone = string.Empty;
        public string ClientPhone
        {
            get => _clientPhone;
            set { _clientPhone = value; OnPropertyChanged(); }
        }

        private string _selectedService = string.Empty;
        public string SelectedService
        {
            get => _selectedService;
            set { _selectedService = value; OnPropertyChanged(); }
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
                FilterClients();
            }
        }

        private string _offerTemplate = "Special Offer for You! Get 20% OFF on your next visit.";
        public string OfferTemplate
        {
            get => _offerTemplate;
            set { _offerTemplate = value; OnPropertyChanged(); }
        }

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(); }
        }

        public ICommand SaveClientCommand { get; }
        public ICommand DeleteClientCommand { get; }
        public ICommand SelectServiceCommand { get; }
        public ICommand SelectPresetCommand { get; }
        public ICommand WhatsAppCommand { get; }
        public ICommand SmsCommand { get; }
        public ICommand CallCommand { get; }
        public ICommand BroadcastCommand { get; }

        public MainViewModel()
        {
            _db = new AppDbContext();
            _db.Database.EnsureCreated();

            OfferPresets.Add("Special Offer for You! Get 20% OFF on your next visit.");
            OfferPresets.Add("Namaste! Thank you for visiting us. Flat ₹200 OFF on your next booking.");

            SaveClientCommand = new RelayCommand(_ => SaveClient());
            DeleteClientCommand = new RelayCommand(param => DeleteClient(param as ClientEntity));
            SelectServiceCommand = new RelayCommand(param => SelectedService = param?.ToString() ?? "");
            SelectPresetCommand = new RelayCommand(param => OfferTemplate = param?.ToString() ?? "");
            WhatsAppCommand = new RelayCommand(param => OpenWhatsApp(param as ClientEntity, OfferTemplate));
            SmsCommand = new RelayCommand(param => OpenSms(param as ClientEntity));
            CallCommand = new RelayCommand(param => OpenCall(param as ClientEntity));
            BroadcastCommand = new RelayCommand(_ => StartBroadcast());

            LoadClients();
        }

        private void LoadClients()
        {
            AllClients.Clear();
            var items = _db.Clients.OrderByDescending(c => c.VisitTimestamp).ToList();
            foreach (var item in items)
            {
                AllClients.Add(item);
            }
            FilterClients();
        }

        private void FilterClients()
        {
            FilteredClients.Clear();
            var query = SearchQuery?.Trim().ToLower() ?? "";
            var matches = AllClients.Where(c => string.IsNullOrEmpty(query) ||
                                                c.Name.ToLower().Contains(query) ||
                                                c.Phone.Contains(query));
            foreach (var match in matches)
            {
                FilteredClients.Add(match);
            }
        }

        private void SaveClient()
        {
            if (string.IsNullOrWhiteSpace(ClientName) || string.IsNullOrWhiteSpace(ClientPhone))
            {
                MessageBox.Show("Please enter both Name and Mobile Number!", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string formattedPhone = FormatPhoneNumber(ClientPhone);
            var newClient = new ClientEntity
            {
                Name = ClientName.Trim(),
                Phone = formattedPhone,
                Notes = SelectedService,
                VisitTimestamp = DateTime.Now,
                AutoThanksScheduledAt = DateTime.Now.AddHours(1),
                IsThanksSent = false
            };

            _db.Clients.Add(newClient);
            _db.SaveChanges();

            ClientName = string.Empty;
            ClientPhone = string.Empty;
            SelectedService = string.Empty;

            LoadClients();
            MessageBox.Show("Client saved successfully! Automatic Thank You SMS scheduled.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteClient(ClientEntity? client)
        {
            if (client == null) return;

            var res = MessageBox.Show($"Delete {client.Name} (+{client.Phone})?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _db.Clients.Remove(client);
                _db.SaveChanges();
                LoadClients();
            }
        }

        private string FormatPhoneNumber(string input)
        {
            string digits = Regex.Replace(input, @"[^\d]", "");
            if (digits.Length == 10) return "91" + digits;
            if (digits.StartsWith("0") && digits.Length == 11) return "91" + digits.Substring(1);
            return digits;
        }

        private void OpenWhatsApp(ClientEntity? client, string message)
        {
            if (client == null) return;
            string url = $"https://wa.me/{client.Phone}?text={Uri.EscapeDataString(message)}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void OpenSms(ClientEntity? client)
        {
            if (client == null) return;
            string msg = $"Thanks for visiting us, {client.Name}! {OfferTemplate}";
            string url = $"mailto:?body={Uri.EscapeDataString(msg)}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void OpenCall(ClientEntity? client)
        {
            if (client == null) return;
            string url = $"tel:{client.Phone}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void StartBroadcast()
        {
            if (!AllClients.Any())
            {
                MessageBox.Show("No clients saved to broadcast!", "Broadcast", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var client in AllClients)
            {
                var res = MessageBox.Show($"Send WhatsApp to {client.Name} (+{client.Phone})?\n\nMsg: {OfferTemplate}",
                                          "Broadcast Wizard", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    OpenWhatsApp(client, OfferTemplate);
                }
                else if (res == MessageBoxResult.Cancel)
                {
                    break;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
        }
    }
}
