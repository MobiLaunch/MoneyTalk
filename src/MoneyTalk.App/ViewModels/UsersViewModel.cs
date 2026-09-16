using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class UsersViewModel : ViewModelBase
{
    public ObservableCollection<User> Users { get; } = new();

    public static IReadOnlyList<string> RoleOptions { get; } = Enum.GetNames<UserRole>();

    public UsersViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService)
        : base(unitOfWorkFactory, settingsService)
    {
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var users = await uow.Users.FindAsync(u => u.CompanyId == ActiveCompanyId);
            Users.Clear();
            foreach (var user in users.OrderBy(u => u.Name)) Users.Add(user);
        });
    }

    public async Task<bool> AddUserAsync(string name, string email, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Name is required.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var user = new User { CompanyId = ActiveCompanyId, Name = name.Trim(), Email = email.Trim(), Role = role };
            await uow.Users.AddAsync(user);
            await uow.SaveChangesAsync();
            Users.Add(user);
            success = true;
        });
        return success;
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(User? user)
    {
        if (user == null) return;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var tracked = await uow.Users.GetByIdAsync(user.Id) ?? throw new InvalidOperationException("User no longer exists.");
            tracked.IsActive = !tracked.IsActive;
            uow.Users.Update(tracked);
            await uow.SaveChangesAsync();
            user.IsActive = tracked.IsActive;
        });
    }
}
