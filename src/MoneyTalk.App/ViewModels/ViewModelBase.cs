using CommunityToolkit.Mvvm.ComponentModel;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

/// <summary>Common plumbing every page ViewModel needs: a fresh <see cref="IUnitOfWork"/> per
/// operation, the active company id, and busy/error state for the UI to bind against.</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;
    protected readonly LocalSettingsService SettingsService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    protected ViewModelBase(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        SettingsService = settingsService;
    }

    protected IUnitOfWork NewUnitOfWork() => _unitOfWorkFactory();

    protected Guid ActiveCompanyId => SettingsService.Load().ActiveCompanyId
        ?? throw new InvalidOperationException("No active company is selected.");

    protected async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
