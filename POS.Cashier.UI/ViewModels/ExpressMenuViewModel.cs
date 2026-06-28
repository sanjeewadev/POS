using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Cashier.UI.Messages;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class ExpressMenuViewModel : ObservableObject
    {
        private readonly ExpressItemRepository _repository;

        // New simple one-grid collection.
        public ObservableCollection<ExpressItemButtonDto> ExpressButtons { get; } = new();

        // Kept for compatibility with your current old XAML until we replace it.
        public ObservableCollection<ExpressItemButtonDto> CurrentTabButtons => ExpressButtons;

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private string _statusMessage = "Loading express items...";

        [ObservableProperty]
        private int _buttonCount = 0;

        [ObservableProperty]
        private ExpressItemButtonDto? _selectedButton;

        public ExpressMenuViewModel(ExpressItemRepository repository)
        {
            _repository = repository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadButtonsAsync();
        }

        [RelayCommand]
        private async Task LoadButtonsAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading express item buttons...";

            try
            {
                ExpressButtons.Clear();

                var buttons = await _repository.GetActiveCashierButtonsAsync();

                foreach (var button in buttons
                             .OrderBy(b => b.GridRow)
                             .ThenBy(b => b.GridColumn)
                             .ThenBy(b => b.DisplayLabel))
                {
                    ExpressButtons.Add(button);
                }

                ButtonCount = ExpressButtons.Count;

                StatusMessage = ButtonCount == 0
                    ? "No express item buttons configured."
                    : $"Loaded {ButtonCount} express button(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load express item buttons.";

                System.Windows.MessageBox.Show(
                    $"Failed to load express items:\n\n{ex.Message}",
                    "Database Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ButtonClicked(ExpressItemButtonDto? button)
        {
            if (button == null)
                return;

            if (button.ItemVariantId <= 0)
                return;

            SelectedButton = button;

            WeakReferenceMessenger.Default.Send(
                new AddToCartMessage(
                    new AddToCartRequest(
                        itemVariantId: button.ItemVariantId,
                        skuCode: button.SkuCode,
                        barcode: button.Barcode,
                        quantity: 1)));

            StatusMessage = $"Added {button.DisplayLabel}.";
        }
    }
}