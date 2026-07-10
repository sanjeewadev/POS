using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Enums;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;
using POS.Core.Utilities;
using System;
using System.Threading.Tasks;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class FirstRunAdminViewModel : ObservableObject
    {
        private readonly UserRepository _userRepository;

        [ObservableProperty]
        private string _firstName = string.Empty;

        [ObservableProperty]
        private string _lastName = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isProcessing;

        public FirstRunAdminViewModel(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<(bool Success, string Message)> CreateAdministratorAsync(
            string password,
            string confirmPassword)
        {
            if (IsProcessing)
            {
                return (false, "Administrator creation is already in progress.");
            }

            IsProcessing = true;
            ErrorMessage = string.Empty;

            try
            {
                string firstName = FirstName.Trim();
                string lastName = LastName.Trim();
                string username = Username.Trim();

                if (string.IsNullOrWhiteSpace(firstName))
                {
                    return (false, "First name is required.");
                }

                if (firstName.Length > 50)
                {
                    return (false, "First name cannot exceed 50 characters.");
                }

                if (string.IsNullOrWhiteSpace(lastName))
                {
                    return (false, "Last name is required.");
                }

                if (lastName.Length > 50)
                {
                    return (false, "Last name cannot exceed 50 characters.");
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    return (false, "Username is required.");
                }

                if (username.Length < 4 || username.Length > 50)
                {
                    return (false, "Username must contain between 4 and 50 characters.");
                }

                if (IsReservedUsername(username))
                {
                    return (false, "That username is reserved. Choose another username.");
                }

                if (password != confirmPassword)
                {
                    return (false, "The passwords do not match.");
                }

                var passwordValidation = PasswordPolicy.Validate(password, username);
                if (!passwordValidation.IsValid)
                {
                    return (false, passwordValidation.ErrorMessage);
                }

                if (await _userRepository.AnyUsersAsync())
                {
                    return (false, "Initial administrator setup has already been completed.");
                }

                if (!await _userRepository.IsUsernameUniqueAsync(username))
                {
                    return (false, "That username is already in use.");
                }

                string passwordHash =
                    SecurityHelper.HashData(password, out string passwordSalt);

                var administrator = new User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Username = username,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                bool created =
                    await _userRepository.CreateFirstAdministratorAsync(administrator);

                return created
                    ? (true, "The first administrator account was created successfully.")
                    : (false, "Initial administrator setup has already been completed.");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "First administrator creation",
                    ex);

                return (
                    false,
                    "The Administrator account could not be created. " +
                    "Technical details were saved in the local POS Logs folder.");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private static bool IsReservedUsername(string username)
        {
            return username.Equals("sa", StringComparison.OrdinalIgnoreCase) ||
                   username.Equals("superadmin", StringComparison.OrdinalIgnoreCase) ||
                   username.Equals("system", StringComparison.OrdinalIgnoreCase);
        }
    }
}
