using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;
using MoneyTalk.Core.Interfaces.Integrations;
using MoneyTalk.Integrations.QuickBooks;
using MoneyTalk.Integrations.Square;

namespace MoneyTalk.App.ViewModels;

/// <summary>Manages the three external connections MoneyTalk supports. OAuth here follows the
/// "paste the authorization code back" pattern rather than an embedded web view or a registered
/// custom URI scheme: the app opens the provider's consent page in the system browser, and once
/// the user approves, Square/QuickBooks redirect to the small "oauth-relay" landing page (see
/// /oauth-relay in the repo) — the only piece of this app that needs to be reachable on the public
/// internet, since Square/Intuit require a real HTTPS redirect URL registered in their dashboards
/// and won't accept a custom URI scheme. That page displays the <c>code</c> (and, for QuickBooks,
/// <c>realmId</c>) query-string values, which the user copies and pastes in here. It's a few extra
/// clicks, but it needs no packaging identity, no registry changes, and no background HTTP
/// listener in the desktop app itself.</summary>
public partial class IntegrationsSettingsViewModel : ViewModelBase
{
    private readonly ISecureTokenStore _secureTokenStore;
    private readonly ISquareClient _squareClient;
    private readonly IQuickBooksClient _quickBooksClient;
    private readonly SquareOptions _squareOptions;
    private readonly QuickBooksOptions _quickBooksOptions;

    [ObservableProperty] private string geminiApiKeyInput = string.Empty;
    [ObservableProperty] private bool hasGeminiKey;
    [ObservableProperty] private string geminiStatusMessage = string.Empty;

    [ObservableProperty] private string squareClientIdInput = string.Empty;
    [ObservableProperty] private string squareClientSecretInput = string.Empty;
    [ObservableProperty] private string squareRedirectUriInput = string.Empty;
    [ObservableProperty] private bool squareUseSandboxInput = true;
    [ObservableProperty] private bool squareIsConnected;
    [ObservableProperty] private string squareAuthorizationUrl = string.Empty;
    [ObservableProperty] private string squareAuthCodeInput = string.Empty;
    [ObservableProperty] private string squareStatusMessage = string.Empty;

    [ObservableProperty] private string quickBooksClientIdInput = string.Empty;
    [ObservableProperty] private string quickBooksClientSecretInput = string.Empty;
    [ObservableProperty] private string quickBooksRedirectUriInput = string.Empty;
    [ObservableProperty] private bool quickBooksUseSandboxInput = true;
    [ObservableProperty] private bool quickBooksIsConnected;
    [ObservableProperty] private string quickBooksAuthorizationUrl = string.Empty;
    [ObservableProperty] private string quickBooksAuthCodeInput = string.Empty;
    [ObservableProperty] private string quickBooksRealmIdInput = string.Empty;
    [ObservableProperty] private string quickBooksStatusMessage = string.Empty;

    public ObservableCollection<BankAccount> BankAccounts { get; } = new();
    [ObservableProperty] private BankAccount? selectedSquareDepositAccount;

    public IntegrationsSettingsViewModel(
        Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService,
        ISecureTokenStore secureTokenStore, ISquareClient squareClient, IQuickBooksClient quickBooksClient,
        SquareOptions squareOptions, QuickBooksOptions quickBooksOptions)
        : base(unitOfWorkFactory, settingsService)
    {
        _secureTokenStore = secureTokenStore;
        _squareClient = squareClient;
        _quickBooksClient = quickBooksClient;
        _squareOptions = squareOptions;
        _quickBooksOptions = quickBooksOptions;
    }

    [RelayCommand]
    private void SaveSquareAppCredentials()
    {
        _squareOptions.ClientId = SquareClientIdInput.Trim();
        if (!string.IsNullOrWhiteSpace(SquareClientSecretInput))
        {
            _squareOptions.ClientSecret = SquareClientSecretInput.Trim();
            _secureTokenStore.SaveSecret(SecretKeys.SquareClientSecret, _squareOptions.ClientSecret);
        }
        if (!string.IsNullOrWhiteSpace(SquareRedirectUriInput))
            _squareOptions.RedirectUri = SquareRedirectUriInput.Trim();
        _squareOptions.ApiBaseUrl = SquareUseSandboxInput
            ? "https://connect.squareupsandbox.com" : "https://connect.squareup.com";

        var settings = SettingsService.Load();
        settings.SquareClientId = _squareOptions.ClientId;
        settings.SquareRedirectUri = _squareOptions.RedirectUri;
        settings.SquareUseSandbox = SquareUseSandboxInput;
        SettingsService.Save(settings);

        SquareClientSecretInput = string.Empty;
        SquareAuthorizationUrl = _squareClient.BuildAuthorizationUrl(
            Guid.NewGuid().ToString("N"), new[] { "PAYMENTS_READ", "MERCHANT_PROFILE_READ" });
        SquareStatusMessage = "Square app credentials saved.";
    }

    [RelayCommand]
    private void SaveQuickBooksAppCredentials()
    {
        _quickBooksOptions.ClientId = QuickBooksClientIdInput.Trim();
        if (!string.IsNullOrWhiteSpace(QuickBooksClientSecretInput))
        {
            _quickBooksOptions.ClientSecret = QuickBooksClientSecretInput.Trim();
            _secureTokenStore.SaveSecret(SecretKeys.QuickBooksClientSecret, _quickBooksOptions.ClientSecret);
        }
        if (!string.IsNullOrWhiteSpace(QuickBooksRedirectUriInput))
            _quickBooksOptions.RedirectUri = QuickBooksRedirectUriInput.Trim();
        _quickBooksOptions.UseSandbox = QuickBooksUseSandboxInput;

        var settings = SettingsService.Load();
        settings.QuickBooksClientId = _quickBooksOptions.ClientId;
        settings.QuickBooksRedirectUri = _quickBooksOptions.RedirectUri;
        settings.QuickBooksUseSandbox = QuickBooksUseSandboxInput;
        SettingsService.Save(settings);

        QuickBooksClientSecretInput = string.Empty;
        QuickBooksAuthorizationUrl = _quickBooksClient.BuildAuthorizationUrl(Guid.NewGuid().ToString("N"));
        QuickBooksStatusMessage = "QuickBooks app credentials saved.";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        HasGeminiKey = !string.IsNullOrWhiteSpace(_secureTokenStore.GetSecret(SecretKeys.GeminiApiKey));
        SquareClientIdInput = _squareOptions.ClientId;
        SquareRedirectUriInput = _squareOptions.RedirectUri;
        SquareUseSandboxInput = _squareOptions.ApiBaseUrl.Contains("squareupsandbox");
        QuickBooksClientIdInput = _quickBooksOptions.ClientId;
        QuickBooksRedirectUriInput = _quickBooksOptions.RedirectUri;
        QuickBooksUseSandboxInput = _quickBooksOptions.UseSandbox;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var connections = await uow.IntegrationConnections.FindAsync(c => c.CompanyId == companyId);
            SquareIsConnected = connections.Any(c => c.Provider == IntegrationProvider.Square && c.IsConnected);
            QuickBooksIsConnected = connections.Any(c => c.Provider == IntegrationProvider.QuickBooksOnline && c.IsConnected);

            var bankAccounts = await uow.BankAccounts.FindAsync(b => b.CompanyId == companyId && b.IsActive);
            BankAccounts.Clear();
            foreach (var account in bankAccounts) BankAccounts.Add(account);
            SelectedSquareDepositAccount ??= BankAccounts.FirstOrDefault();
        });

        SquareAuthorizationUrl = _squareClient.BuildAuthorizationUrl(
            Guid.NewGuid().ToString("N"), new[] { "PAYMENTS_READ", "MERCHANT_PROFILE_READ" });
        QuickBooksAuthorizationUrl = _quickBooksClient.BuildAuthorizationUrl(Guid.NewGuid().ToString("N"));
    }

    [RelayCommand]
    private void SaveGeminiKey()
    {
        if (string.IsNullOrWhiteSpace(GeminiApiKeyInput))
        {
            GeminiStatusMessage = "Enter an API key first.";
            return;
        }

        _secureTokenStore.SaveSecret(SecretKeys.GeminiApiKey, GeminiApiKeyInput.Trim());
        GeminiApiKeyInput = string.Empty;
        HasGeminiKey = true;
        GeminiStatusMessage = "Gemini API key saved.";
    }

    [RelayCommand]
    private async Task CompleteSquareConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(SquareAuthCodeInput))
        {
            SquareStatusMessage = "Paste the authorization code from the redirect URL first.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var tokens = await _squareClient.ExchangeAuthorizationCodeAsync(SquareAuthCodeInput.Trim());
            _secureTokenStore.SaveSecret(SecretKeys.SquareAccessToken(ActiveCompanyId), tokens.AccessToken);
            if (tokens.RefreshToken != null)
                _secureTokenStore.SaveSecret(SecretKeys.SquareRefreshToken(ActiveCompanyId), tokens.RefreshToken);

            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;
            var connection = await uow.IntegrationConnections.FirstOrDefaultAsync(
                c => c.CompanyId == companyId && c.Provider == IntegrationProvider.Square)
                ?? new IntegrationConnection { CompanyId = companyId, Provider = IntegrationProvider.Square };

            connection.IsConnected = true;
            connection.CredentialKey = SecretKeys.SquareAccessToken(companyId);
            connection.ExternalAccountId = tokens.MerchantId;
            connection.TokenExpiresAtUtc = tokens.ExpiresAtUtc;

            var existing = await uow.IntegrationConnections.GetByIdAsync(connection.Id);
            if (existing == null) await uow.IntegrationConnections.AddAsync(connection);
            else uow.IntegrationConnections.Update(connection);
            await uow.SaveChangesAsync();

            SquareIsConnected = true;
            SquareAuthCodeInput = string.Empty;
            SquareStatusMessage = "Square connected.";
        });
    }

    [RelayCommand]
    private async Task SyncSquareAsync()
    {
        if (!SquareIsConnected || SelectedSquareDepositAccount == null)
        {
            SquareStatusMessage = "Connect Square and choose a deposit account first.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var companyId = ActiveCompanyId;
            var accessToken = _secureTokenStore.GetSecret(SecretKeys.SquareAccessToken(companyId))
                ?? throw new InvalidOperationException("No Square access token is stored — reconnect Square.");

            using (var tokenUow = NewUnitOfWork())
            {
                var connection = await tokenUow.IntegrationConnections.FirstOrDefaultAsync(
                    c => c.CompanyId == companyId && c.Provider == IntegrationProvider.Square);
                if (connection?.TokenExpiresAtUtc is { } expiresAt && expiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    var refreshToken = _secureTokenStore.GetSecret(SecretKeys.SquareRefreshToken(companyId))
                        ?? throw new InvalidOperationException("Square's access token expired and no refresh token is stored — reconnect Square.");
                    var refreshed = await _squareClient.RefreshTokenAsync(refreshToken);
                    accessToken = refreshed.AccessToken;
                    _secureTokenStore.SaveSecret(SecretKeys.SquareAccessToken(companyId), accessToken);
                    if (refreshed.RefreshToken != null)
                        _secureTokenStore.SaveSecret(SecretKeys.SquareRefreshToken(companyId), refreshed.RefreshToken);
                    connection.TokenExpiresAtUtc = refreshed.ExpiresAtUtc;
                    tokenUow.IntegrationConnections.Update(connection);
                    await tokenUow.SaveChangesAsync();
                }
            }

            var locations = await _squareClient.GetLocationsAsync(accessToken);
            var location = locations.FirstOrDefault() ?? throw new InvalidOperationException("This Square account has no locations.");

            var payments = await _squareClient.GetPaymentsAsync(accessToken, location.Id, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

            using var uow = NewUnitOfWork();
            var existingIds = (await uow.BankTransactions.FindAsync(t =>
                t.BankAccountId == SelectedSquareDepositAccount.Id && t.Source == BankTransactionSource.SquareSync))
                .Select(t => t.ExternalId).ToHashSet();

            int imported = 0;
            foreach (var payment in payments.Where(p => p.Status == "COMPLETED" && !existingIds.Contains(p.Id)))
            {
                await uow.BankTransactions.AddAsync(new BankTransaction
                {
                    CompanyId = companyId,
                    BankAccountId = SelectedSquareDepositAccount.Id,
                    Date = payment.CreatedAt,
                    Description = $"Square payment {payment.Id}",
                    Amount = payment.AmountMoney - (payment.ProcessingFeeMoney ?? 0),
                    Source = BankTransactionSource.SquareSync,
                    ExternalId = payment.Id,
                    Status = BankTransactionStatus.Unmatched
                });
                imported++;
            }
            await uow.SaveChangesAsync();

            SquareStatusMessage = $"Synced {imported} new Square payment(s) into {SelectedSquareDepositAccount.BankName}.";
        });
    }

    [RelayCommand]
    private async Task CompleteQuickBooksConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(QuickBooksAuthCodeInput) || string.IsNullOrWhiteSpace(QuickBooksRealmIdInput))
        {
            QuickBooksStatusMessage = "Paste both the authorization code and the realm id from the redirect URL.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var tokens = await _quickBooksClient.ExchangeAuthorizationCodeAsync(QuickBooksAuthCodeInput.Trim(), QuickBooksRealmIdInput.Trim());
            _secureTokenStore.SaveSecret(SecretKeys.QuickBooksAccessToken(ActiveCompanyId), tokens.AccessToken);
            _secureTokenStore.SaveSecret(SecretKeys.QuickBooksRefreshToken(ActiveCompanyId), tokens.RefreshToken);

            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;
            var connection = await uow.IntegrationConnections.FirstOrDefaultAsync(
                c => c.CompanyId == companyId && c.Provider == IntegrationProvider.QuickBooksOnline)
                ?? new IntegrationConnection { CompanyId = companyId, Provider = IntegrationProvider.QuickBooksOnline };

            connection.IsConnected = true;
            connection.CredentialKey = SecretKeys.QuickBooksAccessToken(companyId);
            connection.RealmId = tokens.RealmId;
            connection.TokenExpiresAtUtc = tokens.ExpiresAtUtc;

            var existing = await uow.IntegrationConnections.GetByIdAsync(connection.Id);
            if (existing == null) await uow.IntegrationConnections.AddAsync(connection);
            else uow.IntegrationConnections.Update(connection);
            await uow.SaveChangesAsync();

            QuickBooksIsConnected = true;
            QuickBooksAuthCodeInput = string.Empty;
            QuickBooksStatusMessage = $"QuickBooks connected (company {tokens.RealmId}).";
        });
    }
}
