using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.LookupStaff;
using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class LookupStaffAccountQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Returns_not_found_when_email_is_empty_or_whitespace()
    {
        var repo = new FakeAccountRepository([]);
        var handler = new LookupStaffAccountByEmailQueryHandler(repo);

        var resultEmpty = await handler.Handle(new LookupStaffAccountByEmailQuery(""), CancellationToken.None);
        var resultWhitespace = await handler.Handle(new LookupStaffAccountByEmailQuery("   "), CancellationToken.None);
        var resultNull = await handler.Handle(new LookupStaffAccountByEmailQuery(null), CancellationToken.None);

        Assert.True(resultEmpty.IsSuccess);
        Assert.False(resultEmpty.Value!.IsFound);
        Assert.False(resultWhitespace.Value!.IsFound);
        Assert.False(resultNull.Value!.IsFound);
    }

    [Fact]
    public async Task Returns_not_found_when_account_does_not_exist()
    {
        var repo = new FakeAccountRepository([]);
        var handler = new LookupStaffAccountByEmailQueryHandler(repo);

        var result = await handler.Handle(new LookupStaffAccountByEmailQuery("unknown@parkwise.vn"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsFound);
        Assert.Equal(Guid.Empty, result.Value.AccountId);
    }

    [Theory]
    [InlineData(AccountRole.Customer)]
    [InlineData(AccountRole.ParkingLotOwner)]
    [InlineData(AccountRole.SystemAdmin)]
    public async Task Returns_not_found_when_account_has_role_other_than_staff(AccountRole nonStaffRole)
    {
        var account = Account.Register(Guid.NewGuid(), "user@parkwise.vn", "hash", null, Now);
        account.VerifyEmail();
        account.AssignRole(nonStaffRole);

        var repo = new FakeAccountRepository([account]);
        var handler = new LookupStaffAccountByEmailQueryHandler(repo);

        var result = await handler.Handle(new LookupStaffAccountByEmailQuery("user@parkwise.vn"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsFound);
        Assert.Equal(Guid.Empty, result.Value.AccountId);
    }

    [Fact]
    public async Task Returns_not_found_when_staff_account_is_not_verified()
    {
        var account = Account.Register(Guid.NewGuid(), "unverified@parkwise.vn", "hash", null, Now);
        account.AssignRole(AccountRole.Staff);
        // Note: Default status after register is PendingEmailVerification

        var repo = new FakeAccountRepository([account]);
        var handler = new LookupStaffAccountByEmailQueryHandler(repo);

        var result = await handler.Handle(new LookupStaffAccountByEmailQuery("unverified@parkwise.vn"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsFound);
        Assert.Equal(Guid.Empty, result.Value.AccountId);
    }

    [Fact]
    public async Task Returns_not_found_when_staff_account_is_suspended()
    {
        var account = Account.Register(Guid.NewGuid(), "suspended@parkwise.vn", "hash", null, Now);
        account.VerifyEmail();
        account.AssignRole(AccountRole.Staff);
        account.Suspend();

        var repo = new FakeAccountRepository([account]);
        var handler = new LookupStaffAccountByEmailQueryHandler(repo);

        var result = await handler.Handle(new LookupStaffAccountByEmailQuery("suspended@parkwise.vn"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsFound);
        Assert.Equal(Guid.Empty, result.Value.AccountId);
    }

    [Fact]
    public async Task Returns_success_and_account_id_when_email_matches_active_staff_account()
    {
        var staffId = Guid.NewGuid();
        var account = Account.Register(staffId, "staff.alice@parkwise.vn", "hash", null, Now);
        account.VerifyEmail();
        account.AssignRole(AccountRole.Staff);

        var repo = new FakeAccountRepository([account]);
        var handler = new LookupStaffAccountByEmailQueryHandler(repo);

        // Case-insensitive / whitespace trimmed check
        var result = await handler.Handle(new LookupStaffAccountByEmailQuery("  STAFF.ALICE@PARKWISE.VN "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsFound);
        Assert.Equal(staffId, result.Value.AccountId);
    }

    private sealed class FakeAccountRepository(IEnumerable<Account> initial) : IAccountRepository
    {
        private readonly List<Account> _accounts = [..initial];

        public Task<Account?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)));

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.Id == id));

        public Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<Account?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<Account?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.GoogleSubject == googleSubject));

        public Task<Account?> FindByGoogleSubjectForUpdateAsync(string googleSubject, CancellationToken cancellationToken) =>
            FindByGoogleSubjectAsync(googleSubject, cancellationToken);

        public Task<Account?> FindByPhoneNumberAsync(string normalizedPhoneNumber, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.FirstOrDefault(a => a.PhoneNumber == normalizedPhoneNumber));

        public Task<Account?> FindByPhoneNumberForUpdateAsync(string normalizedPhoneNumber, CancellationToken cancellationToken) =>
            FindByPhoneNumberAsync(normalizedPhoneNumber, cancellationToken);

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.Any(a => a.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> PhoneNumberExistsAsync(string normalizedPhoneNumber, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.Any(a => a.PhoneNumber == normalizedPhoneNumber));

        public void Add(Account account) => _accounts.Add(account);
        public Task<SaveAccountResult> SaveNewAccountAsync(CancellationToken cancellationToken) => Task.FromResult(SaveAccountResult.Success);
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken) => action();
    }
}
