using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Views.Dialogs;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Documents;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class CustomerReturnsAuditViewModel : ViewModelBase
    {
        private readonly CustomerReturnRepository _repository;
        private readonly StoreSettingsRepository _storeSettingsRepository;
        private readonly CustomerCreditNoteTextFormatter _formatter;

        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private CustomerReturnHistoryRowDto? _selectedReturn;
        [ObservableProperty] private CustomerReturnHistoryDetailsDto? _selectedDetails;

        public ObservableCollection<CustomerReturnHistoryRowDto> Returns { get; } = new();

        public bool HasSelection => SelectedDetails != null;
        public bool IsEmpty => !IsBusy && Returns.Count == 0;

        public CustomerReturnsAuditViewModel(
            CustomerReturnRepository repository,
            StoreSettingsRepository storeSettingsRepository,
            CustomerCreditNoteTextFormatter formatter)
        {
            _repository = repository;
            _storeSettingsRepository = storeSettingsRepository;
            _formatter = formatter;
            _ = LoadAsync();
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show("Start date cannot be later than end date.", "Customer Return", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Loading customer returns...";
            try
            {
                var rows = await _repository.GetReturnHistoryAsync(StartDate, EndDate, SearchText);
                Returns.Clear();
                foreach (var row in rows)
                    Returns.Add(row);

                SelectedReturn = Returns.Count > 0 ? Returns[0] : null;
                StatusMessage = $"{Returns.Count:N0} return document(s).";
                OnPropertyChanged(nameof(IsEmpty));
            }
            catch (Exception ex)
            {
                StatusMessage = "Customer return history could not be loaded.";
                MessageBox.Show(ex.Message, "Customer Return", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private async Task LoadDetailsAsync(int? returnHeaderId)
        {
            if (!returnHeaderId.HasValue)
            {
                SelectedDetails = null;
                OnPropertyChanged(nameof(HasSelection));
                return;
            }

            try
            {
                SelectedDetails = await _repository.GetReturnHistoryDetailsAsync(returnHeaderId.Value);
                OnPropertyChanged(nameof(HasSelection));
            }
            catch (Exception ex)
            {
                SelectedDetails = null;
                OnPropertyChanged(nameof(HasSelection));
                MessageBox.Show(ex.Message, "Customer Return Details", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ResetAsync()
        {
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            SearchText = string.Empty;
            await LoadAsync();
        }

        [RelayCommand(CanExecute = nameof(CanPreviewCreditNote))]
        private async Task PreviewCreditNoteAsync()
        {
            if (SelectedReturn == null)
                return;

            try
            {
                var document = await _repository.GetReturnDocumentAsync(SelectedReturn.Id);
                if (document == null)
                    throw new InvalidOperationException("The selected Credit Note could not be found.");

                var settings = await _storeSettingsRepository.GetActiveAsync()
                    ?? StoreSettingsRepository.CreateDefaultSettings();
                string text = _formatter.FormatCreditNote(document, settings, 80);

                var dialog = new CreditNotePreviewDialog(text)
                {
                    Owner = Application.Current?.MainWindow
                };
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Credit Note Preview", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanPreviewCreditNote() => SelectedReturn != null;

        partial void OnSelectedReturnChanged(CustomerReturnHistoryRowDto? oldValue, CustomerReturnHistoryRowDto? newValue)
        {
            PreviewCreditNoteCommand.NotifyCanExecuteChanged();
            _ = LoadDetailsAsync(newValue?.Id);
        }
    }
}
