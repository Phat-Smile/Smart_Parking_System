# TÀI LIỆU KỸ THUẬT TOÀN DIỆN: THIẾT KẾ KIẾN TRÚC, CODING & LOGIC NGHIỆP VỤ HỆ THỐNG XÁC THỰC VÀ ĐỊNH DANH (IDENTITY & AUTHENTICATION)

## Smart Parking Management System

> **Dự án:** Smart Parking Management System  
> **Phạm vi 3 Pull Requests:**  
> - **PR #18**: `feat(auth): implement email otp verification and resend` — Tính năng `US-AUTH-003` (Email OTP Verification, HMAC-SHA256 Pepper, CSPRNG 6 số, Row Locking `SELECT FOR UPDATE`, Outbox, RabbitMQ, Hybrid Email Dispatch).  
> - **PR #17**: `feat(auth): implement user login` — Tính năng `US-AUTH-004 & US-AUTH-008` (User Login, Dummy BCrypt Timing Attack Mitigation, RS256 Asymmetric JWT Issuer, Refresh Token SHA-256 Storage & Rotation, Account Lockout Policy 5 lần/15 phút).  
> - **PR #19**: `feat(auth): implement user registration` — Tính năng `US-AUTH-001 Mở rộng` (Họ tên Unicode tiếng Việt, Ngày sinh >= 16 tuổi, Số điện thoại Việt Nam tùy chọn, Điều khoản, Entity `Profile` quan hệ 1:1 Cascade DB, Frontend Registration Page).  
> **Kiến trúc áp dụng:** Clean Architecture, Domain-Driven Design (DDD), Transactional Outbox Pattern, Event-Driven Architecture (EDA) qua RabbitMQ, Idempotent Consumer, Single Synchronization Point (PostgreSQL Row Lock), Asymmetric Cryptography (RS256).  
> **Trạng thái:** Production-Ready | Đã kiểm thử tự động trên CI/CD GitHub Actions (100% Green cả 3 PR).

---

## 📑 MỤC LỤC
1. [Bảng Ánh xạ Phân chia 3 Pull Requests Độc lập](#1-bảng-ánh-xạ-phân-chia-3-pull-requests-độc-lập)
2. [Tổng quan Kiến trúc Hệ thống & Luồng Dữ liệu Toàn trình (EDA)](#2-tổng-quan-kiến-trúc-hệ-thống--luồng-dữ-liệu-toàn-trình-eda)
3. [Chi tiết Coding & Logic PR #19: Đăng ký Tài khoản & Hồ sơ (US-AUTH-001 Mở rộng)](#3-chi-tiết-coding--logic-pr-19-đăng-ký-tài-khoản--hồ-sơ-us-auth-001-mở-rộng)
   - 3.1 [Domain Layer: Entity `Account` & Entity `Profile`](#31-domain-layer-entity-account--entity-profile)
   - 3.2 [Validation Rules: Tên Unicode tiếng Việt, Tuổi >= 16, Số điện thoại Việt Nam](#32-validation-rules-tên-unicode-tiếng-việt-tuổi--16-số-điện-thoại-việt-nam)
   - 3.3 [Application Layer: Command, Validator & Handler](#33-application-layer-command-validator--handler)
   - 3.4 [Persistence Layer: Cấu hình EF Core 1:1 Cascade & Migration `AddProfilesTable`](#34-persistence-layer-cấu-hình-ef-core-11-cascade--migration-addprofilestable)
   - 3.5 [Frontend Integration: Form Đăng ký Next.js & Kiểm thử Playwright E2E](#35-frontend-integration-form-đăng-ký-nextjs--kiểm-thử-playwright-e2e)
4. [Chi tiết Coding & Logic PR #18: Phát hành & Xác thực Email OTP (US-AUTH-003)](#4-chi-tiết-coding--logic-pr-18-phát-hành--xác-thực-email-otp-us-auth-003)
   - 4.1 [Domain Layer: Entity `EmailVerification` & 5 Quy tắc Bất biến](#41-domain-layer-entity-emailverification--5-quy-tắc-bất-biến)
   - 4.2 [Bảo mật Băm OTP Keyed HMAC-SHA256 (Server Pepper) & Chống Timing Attack](#42-bảo-mật-băm-otp-keyed-hmac-sha256-server-pepper--chống-timing-attack)
   - 4.3 [CSPRNG Generator: Sinh mã OTP 6 số ngẫu nhiên an toàn](#43-csprng-generator-sinh-mã-otp-6-số-ngẫu-nhiên-an-toàn)
   - 4.4 [Chiến lược Đồng thời: PostgreSQL Row-Level Lock `SELECT FOR UPDATE`](#44-chiến-lược-đồng-thời-postgresql-row-level-lock-select-for-update)
   - 4.5 [Application Layer: Xử lý Xác thực (`VerifyEmailOtpCommand`) & Gửi lại (`ResendOtpCommand`)](#45-application-layer-xử-lý-xác-thực-verifyemailotpcommand--gửi-lại-resendotpcommand)
5. [Chi tiết Coding & Logic PR #17: Đăng nhập, RS256 JWT & Khóa tài khoản (US-AUTH-004 & US-AUTH-008)](#5-chi-tiết-coding--logic-pr-17-đăng-nhập-rs256-jwt--khóa-tài-khoản-us-auth-004--us-auth-008)
   - 5.1 [Chống Tấn công Timing Enumeration: Dummy BCrypt Password Verification](#51-chống-tấn-công-timing-enumeration-dummy-bcrypt-password-verification)
   - 5.2 [Hạ tầng Ký Asymmetric JWT Token RS256 & Cache Private Key](#52-hạ-tầng-ký-asymmetric-jwt-token-rs256--cache-private-key)
   - 5.3 [Kiến trúc Quản lý Refresh Token & Lưu trữ Băm SHA-256](#53-kiến-trúc-quản-lý-refresh-token--lưu-trữ-băm-sha-256)
   - 5.4 [Chính sách Khóa tài khoản (Account Lockout Policy: 5 lần/15 phút)](#54-chính-sách-khóa-tài-khoản-account-lockout-policy-5-lần15-phút)
   - 5.5 [Kiểm tra Trạng thái Tài khoản: Yêu cầu `EmailVerified`](#55-kiểm-tra-trạng-thái-tài-khoản-yêu-cầu-emailverified)
6. [Tích hợp EDA, Transactional Outbox & Notification Service](#6-tích-hợp-eda-transactional-outbox--notification-service)
   - 6.1 [Sự kiện Tích hợp (Integration Event Contracts)](#61-sự-kiện-tích-hợp-integration-event-contracts)
   - 6.2 [Transactional Outbox Pattern & Background Dispatcher](#62-transactional-outbox-pattern--background-dispatcher)
   - 6.3 [RabbitMQ Consumer & Idempotent Inbox tại Notification Service](#63-rabbitmq-consumer--idempotent-inbox-tại-notification-service)
   - 6.4 [Hạ tầng Gửi Email Linh hoạt & Cơ chế Retry / DLQ qua MassTransit](#64-hạ-tầng-gửi-email-linh-hoạt--cơ-chế-retry--dlq-qua-masstransit)
7. [Đặc tả Hợp đồng API (API Contract Specification)](#7-đặc-tả-hợp-đồng-api-api-contract-specification)
8. [Ma trận Kiểm thử Tự động & Kết quả Nghiệm thu (Testing & QA)](#8-ma-trận-kiểm-thử-tự-động--kết-quả-nghiệm-thu-testing--qa)
9. [Bảng Ánh xạ Mã nguồn Toàn diện theo từng PR (Source Code Map)](#9-bảng-ánh-xạ-mã-nguồn-toàn-diện-theo-từng-pr-source-code-map)

---

## 1. Bảng Ánh xạ Phân chia 3 Pull Requests Độc lập

| Thông tin | PR 1: Email OTP (US-AUTH-003) | PR 2: User Login (US-AUTH-004 & 008) | PR 3: User Registration (US-AUTH-001) |
| :--- | :--- | :--- | :--- |
| **Pull Request** | **[PR #18](https://github.com/Phat-Smile/Smart_Parking_System/pull/18)** | **[PR #17](https://github.com/Phat-Smile/Smart_Parking_System/pull/17)** | **[PR #19](https://github.com/Phat-Smile/Smart_Parking_System/pull/19)** |
| **Nhánh (Branch)** | `features/Implementation_EmailOtpVerification` | `features/Implementation_LoginWithEmail` | `features/Implementation_UserRegistration` |
| **Tiêu đề Commit** | `feat(auth): implement email otp verification and resend` | `feat(auth): implement user login` | `feat(auth): implement user registration` |
| **Trọng tâm nghiệp vụ** | Phát hành & xác thực mã OTP, băm HMAC-SHA256, khóa dòng Postgres `SELECT FOR UPDATE`, Outbox EDA, Notification SMTP retry/DLQ. | Xác thực đăng nhập, phòng thủ timing attack qua dummy BCrypt, ký RS256 JWT, Refresh Token SHA-256, lockout 15 phút. | Đăng ký tài khoản, mở rộng trường FullName Unicode tiếng Việt, Ngày sinh $\ge 16$ tuổi, SĐT tùy chọn, Điều khoản, Entity `Profile` 1:1, UI Form. |
| **Trạng thái CI** | **100% PASS (7/7 checks)** | **100% PASS (7/7 checks)** | **100% PASS (All checks)** |

---

## 2. Tổng quan Kiến trúc Hệ thống & Luồng Dữ liệu Toàn trình (EDA)

Hệ thống Identity & Authentication được thiết kế theo kiến trúc **Clean Architecture** kết hợp mô hình **Event-Driven Architecture (EDA)**. Các ranh giới nghiệp vụ được phân định rõ ràng:

```
[ Client / Web Browser / Mobile App ]
                 │
                 ▼  HTTPS (REST / JSON)
[ Identity Service WebAPI (Port 5001) ]
  ├── Endpoints: POST /accounts, POST /verify-email, POST /resend-otp, POST /login
  ├── Application Layer: MediatR Commands, FluentValidation, Ports (Repositories, Security)
  ├── Domain Layer: Account, Profile, EmailVerification, RefreshToken
  └── Persistence Layer (EF Core / PostgreSQL Schema: "identity")
        ├── identity.accounts
        ├── identity.profiles (1:1 Cascade FK to accounts)
        ├── identity.email_verifications
        ├── identity.refresh_tokens
        └── identity.outbox_messages (Transactional Outbox)
                 │
                 ▼  Outbox Background Dispatcher (Lease Lock)
[ Message Broker: RabbitMQ (Topic Exchange: "parking-system") ]
                 │
                 ▼  AMQP 0-9-1
[ Notification Service WebAPI (Port 5005) ]
  ├── MassTransit Idempotent Consumer (notification.inbox_messages)
  └── Email Dispatcher (Live SMTP / Mailpit / Console Fallback)
                 │
                 ▼  SMTP Transport
[ User Email Inbox / SMTP Server ]
```

---

## 3. Chi tiết Coding & Logic PR #19: Đăng ký Tài khoản & Hồ sơ (US-AUTH-001 Mở rộng)

### 3.1 Domain Layer: Entity `Account` & Entity `Profile`

* **Entity `Account` (`Identity.Domain/Accounts/Account.cs`)**:
  * Đóng vai trò là Aggregate Root quản lý thông tin chứng thực tài khoản (Email, Hash mật khẩu, Số điện thoại, Trạng thái tài khoản).
  * Trạng thái khởi tạo bắt buộc là `PendingEmailVerification`.
  * Email được chuẩn hóa bằng phương thức `NormalizeEmail(email)`: chuyển toàn bộ sang chữ thường và loại bỏ khoảng trắng thừa.
  * Số điện thoại được chuẩn hóa bằng `NormalizePhoneNumber(phone)`: chuyển định dạng bắt đầu bằng `0` thành chuẩn quốc tế `+84`.

* **Entity `Profile` (`Identity.Domain/Profiles/Profile.cs`)**:
  * Đóng vai trò thực thể lưu trữ thông tin hồ sơ cá nhân của người dùng, liên kết 1:1 với `Account`.
  * Thuộc tính:
    * `Guid AccountId`: Khóa chính (Primary Key), đồng thời là Khóa ngoại (Foreign Key) tham chiếu trực tiếp tới `Account.Id`.
    * `string FullName`: Họ và tên người dùng (tối đa 100 ký tự, hỗ trợ Unicode).
    * `DateOnly DateOfBirth`: Ngày tháng năm sinh (định dạng `DateOnly` chuẩn của .NET).
    * `string? CitizenIdNumber`: Số CCCD / CMND (tối đa 20 ký tự, tùy chọn).
    * `string? DriverLicenseNumber`: Số giấy phép lái xe (tối đa 20 ký tự, tùy chọn).
    * `string? AvatarUrl`: Đường dẫn ảnh đại diện (tối đa 500 ký tự, tùy chọn).
    * `DateTimeOffset CreatedAtUtc` & `DateTimeOffset? UpdatedAtUtc`: Dữ liệu audit thời gian.
  * Factory Method `Profile.Create(accountId, fullName, dateOfBirth, now)`: Bảo vệ tính toàn vẹn dữ liệu từ tầng Domain, chặn ngay nếu tên rỗng hoặc vượt quá giới hạn.

### 3.2 Validation Rules: Tên Unicode tiếng Việt, Tuổi >= 16, Số điện thoại Việt Nam

Logic kiểm thực dữ liệu đầu vào được quản lý tập trung tại [RegisterAccountCommandValidator](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Application/Accounts/RegisterAccount/RegisterAccountCommand.cs):

```csharp
public sealed partial class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    private const int MinimumAgeYears = 16;
    private const int MaximumAgeYears = 100;

    public RegisterAccountCommandValidator()
    {
        // 1. Họ và tên: 2 - 100 ký tự, hỗ trợ đầy đủ Unicode tiếng Việt có dấu
        RuleFor(command => command.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("FullName is required.")
            .Must(name => name.Trim().Length >= 2).WithMessage("FullName must be at least 2 characters.")
            .Must(name => name.Trim().Length <= Profile.MaxFullNameLength).WithMessage($"FullName must not exceed {Profile.MaxFullNameLength} characters.")
            .Must(name => FullNameRegex().IsMatch(name.Trim())).WithMessage("FullName must only contain letters and single spaces between words.");

        // 2. Email: chuẩn định dạng email, tối đa 254 ký tự (RFC 5321)
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        // 3. Ngày tháng năm sinh: Tuổi từ 16 đến 100, không được là ngày trong tương lai
        RuleFor(command => command.DateOfBirth)
            .Cascade(CascadeMode.Stop)
            .Must(dob => dob <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("DateOfBirth cannot be in the future.")
            .Must(dob => CalculateAge(dob, DateOnly.FromDateTime(DateTime.UtcNow)) >= MinimumAgeYears)
            .WithMessage($"User must be at least {MinimumAgeYears} years old to register.")
            .Must(dob => CalculateAge(dob, DateOnly.FromDateTime(DateTime.UtcNow)) <= MaximumAgeYears)
            .WithMessage($"DateOfBirth is invalid (age cannot exceed {MaximumAgeYears} years).");

        // 4. Mật khẩu: 8 - 72 ký tự, gồm chữ hoa, chữ thường, số, ký tự đặc biệt
        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8)
            .MaximumLength(72)
            .Matches("[A-Z]").WithMessage("Password must contain an upper-case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower-case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^A-Za-z0-9]").WithMessage("Password must contain a special character.");

        // 5. Số điện thoại (Tùy chọn): nếu nhập phải đúng định dạng di động Việt Nam
        RuleFor(command => command.PhoneNumber)
            .Matches(@"^\s*(0|\+84)[35789][0-9]{8}\s*$")
            .WithMessage("Phone number must be a Vietnamese mobile number: 0 or +84 followed by 9 digits starting with 3, 5, 7, 8 or 9.")
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));

        // 6. Điều khoản dịch vụ: Bắt buộc đồng ý (AcceptTerms = true)
        RuleFor(command => command.AcceptTerms)
            .Equal(true)
            .WithMessage("You must accept the terms of service and privacy policy to register.");
    }

    [GeneratedRegex(@"^[\p{L}]+(?: [\p{L}]+)*$")]
    private static partial Regex FullNameRegex();
}
```

### 3.3 Application Layer: Command, Validator & Handler

* **Command**: `RegisterAccountCommand(string FullName, string Email, DateOnly DateOfBirth, string Password, string? PhoneNumber, bool AcceptTerms)`.
* **Handler (`RegisterAccountCommandHandler`)**:
  1. Kiểm tra nhanh email đã tồn tại qua `accounts.EmailExistsAsync`.
  2. Băm mật khẩu bằng `BCryptPasswordHasher` (work factor 11).
  3. Khởi tạo thực thể `Account` với trạng thái `PendingEmailVerification`.
  4. Khởi tạo thực thể `Profile` với `accountId = account.Id`.
  5. Gọi `accounts.TrySaveChangesAsync(cancellationToken)`. Nếu xảy ra race condition khi có 2 request đăng ký cùng email cùng lúc, lệnh commit sẽ vi phạm Unique Index `ux_accounts_email` trên Postgres, handler bắt lỗi và trả về mã lỗi `identity.email_already_registered` (HTTP 409 Conflict).
  6. Trả về `Result<RegisteredAccount>.Success`.

### 3.4 Persistence Layer: Cấu hình EF Core 1:1 Cascade & Migration `AddProfilesTable`

Trong [ProfileConfiguration.cs](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Persistence/Profiles/ProfileConfiguration.cs):
```csharp
public sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("profiles", IdentityDbContext.DefaultSchema);
        builder.HasKey(profile => profile.AccountId);
        builder.Property(profile => profile.AccountId).HasColumnName("account_id");
        builder.Property(profile => profile.FullName).HasColumnName("full_name").HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date").IsRequired();
        
        // Cấu hình quan hệ 1:1 với Account, cascade delete khi Account bị xóa
        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<Profile>(profile => profile.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
```

### 3.5 Frontend Integration: Form Đăng ký Next.js & Kiểm thử Playwright E2E

* **Frontend Page (`web/src/features/auth/register/`)**:
  * `RegisterForm.tsx`: Component form đăng ký với giao diện hiện đại, hỗ trợ ẩn/hiện mật khẩu, kiểm tra password policy realtime, xử lý lỗi RFC 7807 problem details an toàn.
  * `registerSchema.ts`: Schema kiểm thực phía Client bằng Zod đồng bộ với Backend.
  * `registerApi.ts`: Client gọi qua API Gateway POST `/api/identity/accounts`.
* **Kiểm thử E2E Playwright (`web/e2e/registration.spec.ts`)**:
  * Kiểm thử việc form chặn submit khi thiếu trường mà không gọi API.
  * Đảm bảo mật khẩu và credentials không bao giờ bị lộ ra URL query string.
  * Kiểm thử xử lý lỗi 409 Conflict hiển thị thông báo an toàn không làm lộ thông tin database nội bộ.

---

## 4. Chi tiết Coding & Logic PR #18: Phát hành & Xác thực Email OTP (US-AUTH-003)

### 4.1 Domain Layer: Entity `EmailVerification` & 5 Quy tắc Bất biến

Thực thể [EmailVerification](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Domain/EmailVerifications/EmailVerification.cs) đảm bảo các quy tắc kinh doanh nghiêm ngặt:
1. **Thời hạn OTP**: Hết hạn chính xác sau 5 phút kể từ lúc phát hành (`ExpiresAtUtc = now.AddMinutes(5)`).
2. **Giới hạn số lần thử**: Cho phép tối đa 5 lần nhập sai (`MaxAttempts = 5`). Vượt quá giới hạn, mã OTP bị vô hiệu hóa ngay lập tức.
3. **Thời gian chờ giữa các lần gửi (Cooldown)**: Tối thiểu 60 giây giữa 2 lần yêu cầu gửi lại mã (`ResendCooldownSeconds = 60`).
4. **Giới hạn gửi lại trong 1 giờ**: Cho phép tối đa 5 lần gửi lại mã trong vòng 1 giờ (`MaxResendsPerHour = 5`).
5. **Vòng đời vô hiệu hóa (Invalidation Lifecycle)**: Khi người dùng yêu cầu gửi lại OTP mới, bản ghi OTP cũ chưa sử dụng sẽ được đánh dấu `IsInvalidated = true`, ngăn chặn việc dùng lại mã OTP cũ.

### 4.2 Bảo mật Băm OTP Keyed HMAC-SHA256 (Server Pepper) & Chống Timing Attack

* **Vấn đề bảo mật**: Không bao giờ lưu trữ OTP dạng thô (plain-text) trong cơ sở dữ liệu. Vì không gian mẫu của mã OTP 6 chữ số rất nhỏ (chỉ $10^6$ trường hợp), kẻ tấn công nếu dump được cơ sở dữ liệu có thể dùng Rainbow Table giải mã toàn bộ OTP trong tích tắc nếu dùng thuật toán băm thông thường (MD5, SHA-256).
* **Giải pháp Keyed HMAC-SHA256**:
  * Sử dụng khóa bí mật cấp server (**Server Pepper**) được cấu hình qua biến môi trường `Security:Otp:Pepper`.
  * Kết hợp **Account Salt** theo từng tài khoản: Chuỗi đầu vào được băm có dạng `{accountId}:{otpCode}`.
  * Thuật toán băm: `HMAC-SHA256(Pepper, UTF8("{accountId}:{otpCode}"))`.
* **Chống Timing Attack**:
  * Khi người dùng nhập OTP kiểm tra, hàm so khớp sử dụng `CryptographicOperations.FixedTimeEquals`:
  ```csharp
  public bool VerifyOtp(string candidateOtp, Guid accountId, string expectedHash)
  {
      var candidateHash = HashOtp(candidateOtp, accountId);
      var candidateBytes = Convert.FromHexString(candidateHash);
      var expectedBytes = Convert.FromHexString(expectedHash);
      return CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes);
  }
  ```
  Phương thức này đảm bảo thời gian thực thi việc so sánh là hằng số, triệt tiêu hoàn toàn nguy cơ bị tấn công timing side-channel.

### 4.3 CSPRNG Generator: Sinh mã OTP 6 số ngẫu nhiên an toàn

Trong [SecureOtpGenerator.cs](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Infrastructure/Security/SecureOtpGenerator.cs):
```csharp
public sealed class SecureOtpGenerator : IOtpGenerator
{
    public string GenerateNumericOtp(int length = 6)
    {
        // Sử dụng CSPRNG từ System.Security.Cryptography để sinh số ngẫu nhiên bảo mật cao
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return number.ToString("D6"); // Luôn định dạng đủ 6 chữ số với số 0 ở đầu
    }
}
```

### 4.4 Chiến lược Đồng thời: PostgreSQL Row-Level Lock `SELECT FOR UPDATE`

* **Nguy cơ Race Condition**:
  * Nếu người dùng gửi đồng thời 2 request: 1 request `verify-email` và 1 request `resend-otp` (hoặc 2 request `verify-email` đồng thời với brute-force OTP), nếu chỉ dựa vào cờ `row_version` (Optimistic Concurrency), có thể xảy ra tình trạng cả 2 request cùng đọc trạng thái cũ và xử lý song song trước khi ghi.
* **Giải pháp Pessimistic Row Locking (`SELECT FOR UPDATE`)**:
  * Trong [AccountRepository.cs](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Persistence/Accounts/AccountRepository.cs):
  ```csharp
  public async Task<Account?> FindByEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken)
  {
      return await dbContext.Accounts
          .FromSqlInterpolated($"SELECT * FROM identity.accounts WHERE email = {normalizedEmail} FOR UPDATE")
          .FirstOrDefaultAsync(cancellationToken);
  }
  ```
  * Cả `VerifyEmailOtpCommand` và `ResendOtpCommand` đều được thực thi trong cùng một Transaction Boundary tường minh:
  ```csharp
  return await accounts.ExecuteInTransactionAsync(async () =>
  {
      var account = await accounts.FindByEmailForUpdateAsync(email, cancellationToken);
      // Toàn bộ các thao tác kiểm tra trạng thái, tăng số lần thử, đổi status đều được bảo vệ
  }, cancellationToken);
  ```

### 4.5 Application Layer: Xử lý Xác thực (`VerifyEmailOtpCommand`) & Gửi lại (`ResendOtpCommand`)

* **Luồng Xác thực (`VerifyEmailOtpCommand`)**:
  1. Mở transaction và lấy khóa dòng `Account` qua `SELECT FOR UPDATE`.
  2. Kiểm tra tài khoản tồn tại và trạng thái là `PendingEmailVerification`.
  3. Lấy bản ghi OTP mới nhất còn hiệu lực của tài khoản.
  4. Kiểm tra OTP hết hạn (quá 5 phút) hoặc đã vượt quá 5 lần thử.
  5. Gọi `otpHasher.VerifyOtp`:
     * Nếu sai: Tăng số lần thử `FailedAttempts++`, lưu DB và trả về lỗi.
     * Nếu đúng: Đánh dấu `IsUsed = true`, chuyển trạng thái `Account.Status = EmailVerified`.
  6. Enqueue sự kiện `AccountEmailVerifiedIntegrationEvent` vào bảng Outbox.
  7. Commit transaction.

* **Luồng Gửi lại (`ResendOtpCommand`)**:
  1. Mở transaction và lấy khóa dòng `Account` qua `SELECT FOR UPDATE`.
  2. Kiểm tra cooldown 60 giây và quota tối đa 5 lần trong 1 giờ.
  3. Đánh dấu vô hiệu hóa toàn bộ OTP cũ của tài khoản (`IsInvalidated = true`).
  4. Sinh mã OTP 6 số mới qua CSPRNG, băm HMAC-SHA256 và lưu bản ghi mới.
  5. Enqueue sự kiện `UserVerificationOtpIssuedIntegrationEvent` vào Outbox.
  6. Commit transaction.

---

## 5. Chi tiết Coding & Logic PR #17: Đăng nhập, RS256 JWT & Khóa tài khoản (US-AUTH-004 & US-AUTH-008)

### 5.1 Chống Tấn công Timing Enumeration: Dummy BCrypt Password Verification

* **Nguy cơ User Enumeration**: Khi kẻ tấn công gửi request đăng nhập với một email bất kỳ, nếu backend kiểm tra `if (account == null) return Failure()`, thời gian phản hồi của request sẽ cực nhanh (~2ms). Ngược lại, nếu email tồn tại và backend chạy hàm `BCrypt.Verify()` (tốn ~80ms-150ms do work factor 11), kẻ tấn công có thể đo độ trễ mạng để dò tìm toàn bộ danh sách email hợp lệ trong hệ thống.
* **Giải pháp Dummy Verification trong [LoginCommandHandler.cs](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Application/Accounts/Login/LoginCommand.cs)**:
  ```csharp
  private static readonly string DummyPasswordHash =
      "$2a$11$e8k8h5k8O8d8u8m8m8y8H8a8s8h8F8o8r8T8i8m8i8n8g8S8a8f8e8t8y8";

  if (account is null)
  {
      // Chạy hash giả lập để thời gian phản hồi tương đương với tài khoản tồn tại
      passwordHasher.Verify(request.Password, DummyPasswordHash);
      return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
  }
  ```

### 5.2 Hạ tầng Ký Asymmetric JWT Token RS256 & Cache Private Key

* **Thuật toán RS256**: Sử dụng cặp khóa bất đối xứng RSA:
  * Khóa bí mật (**RSA Private Key**): Chỉ duy nhất Identity Service nắm giữ để ký số Token.
  * Khóa công khai (**RSA Public Key**): Các service khác (Parking, Booking, Payment) hoặc API Gateway sử dụng để kiểm tra chữ ký số mà không bao giờ cần biết khóa bí mật.
* **Tối ưu Caching trong [JwtTokenIssuer.cs](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Infrastructure/Security/JwtTokenIssuer.cs)**:
  * Khởi tạo và nạp RSA Key từ cấu hình hoặc tự sinh lúc khởi động.
  * Cache đối tượng `SigningCredentials` và `RsaSecurityKey` trong suốt vòng đời ứng dụng (Singleton), loại bỏ chi phí khởi tạo lại RSA key cho mỗi request (giảm CPU overhead đáng kể).
  * Claims trong Token:
    * `sub`: Mã định danh tài khoản (`account.Id`).
    * `email`: Địa chỉ email.
    * `jti`: Mã định danh duy nhất của Token (chống replay).
    * `exp`: Thời hạn 15 phút.

### 5.3 Kiến trúc Quản lý Refresh Token & Lưu trữ Băm SHA-256

* **Thực thể `RefreshToken` (`Identity.Domain/Accounts/RefreshToken.cs`)**:
  * `Guid Id`: Khóa chính.
  * `Guid AccountId`: Khóa ngoại tham chiếu `Account.Id`.
  * `string TokenHash`: Giá trị băm SHA-256 của Refresh Token (chuỗi token thô 64 ký tự CSPRNG chỉ trả về cho client đúng 1 lần duy nhất).
  * `DateTimeOffset ExpiresAtUtc`: Thời hạn hiệu lực (7 ngày).
  * `DateTimeOffset? RevokedAtUtc`: Thời gian bị thu hồi.
* **Cơ chế Xoay vòng (Rotation)**: Mỗi khi dùng Refresh Token để lấy cặp Token mới, Refresh Token cũ sẽ bị thu hồi ngay lập tức (`RevokedAtUtc = now`) và một Refresh Token mới được cấp phát.

### 5.4 Chính sách Khóa tài khoản (Account Lockout Policy: 5 lần/15 phút)

Theo yêu cầu nghiệp vụ `US-AUTH-008`:
1. **Theo dõi đăng nhập sai**:
   * Thuộc tính `FailedLoginCount` và `LockoutEndUtc` trên `Account`.
   * Khi người dùng nhập sai mật khẩu: `FailedLoginCount++`.
2. **Kích hoạt khóa**:
   * Khi `FailedLoginCount >= 5`: Thiết lập `LockoutEndUtc = clock.UtcNow.AddMinutes(15)`.
   * Các request tiếp theo trong vòng 15 phút sẽ bị từ chối ngay lập tức với lỗi `identity.account_locked`.
3. **Mở khóa và Reset**:
   * Khi hết thời gian 15 phút: Tài khoản tự động được phép thử lại.
   * Khi người dùng đăng nhập thành công với mật khẩu chính xác: `FailedLoginCount = 0` và `LockoutEndUtc = null`.

### 5.5 Kiểm tra Trạng thái Tài khoản: Yêu cầu `EmailVerified`

* Để đảm bảo an toàn, hệ thống chỉ cho phép đăng nhập nếu tài khoản đã xác thực email thành công:
```csharp
if (account.Status != AccountStatus.EmailVerified)
{
    return Result<LoginResponse>.Failure(LoginErrors.EmailNotVerified);
}
```
* Nếu tài khoản vẫn ở trạng thái `PendingEmailVerification`, hệ thống trả về lỗi `identity.email_not_verified` với hướng dẫn người dùng hoàn tất xác thực OTP.

---

## 6. Tích hợp EDA, Transactional Outbox & Notification Service

### 6.1 Sự kiện Tích hợp (Integration Event Contracts)

Các sự kiện được định nghĩa trong [BuildingBlocks/Events](file:///f:/Ky_5/Smart_Parking_System/src/BuildingBlocks/Events/):
* `UserVerificationOtpIssuedIntegrationEvent(Guid AccountId, string Email, string OtpCode, DateTimeOffset ExpiresAtUtc)`:
  * Event Type: `identity.user-verification-otp-issued.v1` (Version 1).
  * Được phát hành khi tài khoản đăng ký mới hoặc khi yêu cầu gửi lại OTP.
* `AccountEmailVerifiedIntegrationEvent(Guid AccountId, string Email, DateTimeOffset VerifiedAtUtc)`:
  * Event Type: `identity.account-email-verified.v1` (Version 1).
  * Được phát hành khi xác thực email thành công, báo hiệu cho các domain khác (Notification, Marketing, Audit).

### 6.2 Transactional Outbox Pattern & Background Dispatcher

* **Đảm bảo tính nhất quán Dual-Write**: Không bao giờ gửi trực tiếp message lên RabbitMQ trong request HTTP. Lệnh ghi dữ liệu nghiệp vụ (`Account`, `EmailVerification`) và lệnh ghi message sự kiện vào bảng `identity.outbox_messages` được thực thi trong **cùng 1 Database Transaction**.
* **Background Dispatcher**:
  * Chạy định kỳ, sử dụng cơ chế **Lease Lock** (`locked_until_utc`) để nhiều instance Identity Service có thể scale ngang mà không bao giờ gửi trùng lặp sự kiện.
  * Đọc bản ghi outbox, publish lên RabbitMQ exchange, sau đó đánh dấu `processed_at_utc = now`.

### 6.3 RabbitMQ Consumer & Idempotent Inbox tại Notification Service

* **Consumer**: `UserVerificationOtpIssuedConsumer` tại Notification Service đăng ký lắng nghe sự kiện qua MassTransit.
* **Idempotent Inbox Pattern**:
  * Trước khi xử lý gửi email, consumer kiểm tra bảng `notification.inbox_messages`.
  * Nếu `MessageId` đã tồn tại: Bỏ qua (chống gửi trùng email khi RabbitMQ re-deliver).
  * Nếu chưa tồn tại: Lưu `MessageId` vào inbox và tiến hành gửi email.

### 6.4 Hạ tầng Gửi Email Linh hoạt & Cơ chế Retry / DLQ qua MassTransit

Notification Service hỗ trợ 3 chiến lược gửi email theo môi trường:
1. **Môi trường Development không có SMTP (`LoggingEmailSender`)**:
   * Ghi log mã OTP và tiêu đề email trực tiếp ra console, giúp lập trình viên test luồng nhanh chóng mà không cần kết nối mạng.
2. **Môi trường Test Container (`Mailpit`)**:
   * Chạy Mailpit qua Docker Compose (port 1025 SMTP, port 8025 Web UI) để kiểm tra giao diện HTML email thực tế.
3. **Môi trường Production (`SmtpEmailSender`)**:
   * Kết nối máy chủ SMTP thật (Google SMTP, SendGrid, Amazon SES) qua kết nối TLS bảo mật.
   * **Cơ chế Retry & DLQ**: Khi xảy ra lỗi mạng hoặc timeout SMTP, `SmtpEmailSender` ném lại ngoại lệ (`throw;`). MassTransit sẽ tự động retry 3 lần với exponential backoff. Nếu vẫn thất bại, message được chuyển vào **Dead Letter Queue (DLQ)** để giám sát và xử lý sau mà không làm mất thông tin.

---

## 7. Đặc tả Hợp đồng API (API Contract Specification)

### 7.1 Đăng ký tài khoản (`POST /api/identity/accounts`) — [PR #19](https://github.com/Phat-Smile/Smart_Parking_System/pull/19)

* **Request Body**:
```json
{
  "fullName": "Nguyễn Văn An",
  "email": "user@example.com",
  "dateOfBirth": "1998-05-20",
  "password": "Password#123",
  "phoneNumber": "0901234567",
  "acceptTerms": true
}
```
* **Response `201 Created`**:
```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "fullName": "Nguyễn Văn An",
  "status": "PendingEmailVerification"
}
```
* **Mã lỗi RFC 7807**:
  * `400 Bad Request`: Vi phạm validation (tuổi dưới 16, họ tên không hợp lệ, mật khẩu yếu, chưa chấp nhận điều khoản).
  * `409 Conflict`: `errorCode: "identity.email_already_registered"` (Email đã tồn tại).

---

### 7.2 Xác thực Email OTP (`POST /api/identity/accounts/verify-email`) — [PR #18](https://github.com/Phat-Smile/Smart_Parking_System/pull/18)

* **Request Body**:
```json
{
  "email": "user@example.com",
  "otp": "123456"
}
```
* **Response `200 OK`**:
```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "status": "EmailVerified"
}
```
* **Mã lỗi RFC 7807**:
  * `400 Bad Request`: `errorCode: "identity.invalid_otp"` (Sai OTP, OTP hết hạn, vượt quá 5 lần thử, hoặc tài khoản đã verify).
  * `404 Not Found`: Không tìm thấy tài khoản.

---

### 7.3 Gửi lại mã OTP (`POST /api/identity/accounts/resend-otp`) — [PR #18](https://github.com/Phat-Smile/Smart_Parking_System/pull/18)

* **Request Body**:
```json
{
  "email": "user@example.com"
}
```
* **Response `200 OK`**:
```json
{
  "message": "A new verification code has been dispatched."
}
```
* **Mã lỗi RFC 7807**:
  * `429 Too Many Requests`: `errorCode: "identity.otp_cooldown_active"` (Chưa hết 60s cooldown hoặc vượt quá 5 lần/giờ).
  * `400 Bad Request`: `errorCode: "identity.account_already_verified"`.

---

### 7.4 Đăng nhập tài khoản (`POST /api/identity/accounts/login`) — [PR #17](https://github.com/Phat-Smile/Smart_Parking_System/pull/17)

* **Request Body**:
```json
{
  "email": "user@example.com",
  "password": "Password#123"
}
```
* **Response `200 OK`**:
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "d8f1e2c3b4a59687...",
  "expiresIn": 900,
  "tokenType": "Bearer"
}
```
* **Mã lỗi RFC 7807**:
  * `401 Unauthorized`: `errorCode: "identity.invalid_credentials"` (Sai email hoặc mật khẩu, chạy dummy verify chống timing attack).
  * `403 Forbidden`: `errorCode: "identity.email_not_verified"` (Chưa xác thực email).
  * `403 Forbidden`: `errorCode: "identity.account_locked"` (Tài khoản đang bị khóa 15 phút do nhập sai 5 lần).

---

## 8. Ma trận Kiểm thử Tự động & Kết quả Nghiệm thu (Testing & QA)

Hệ thống được bảo vệ bởi bộ kiểm thử tự động toàn diện trên cả 3 tầng:

### 8.1 Thống kê Test Suites

| Bộ kiểm thử | Số lượng test | Trạng thái | Thuộc PR | Nội dung kiểm thử chính |
| :--- | :---: | :---: | :---: | :--- |
| **Identity Unit Tests (Registration)** | 54 / 54 | **PASS (100%)** | PR #19 | Tên Unicode tiếng Việt, Tuổi $\ge 16$, Phone Việt Nam, Điều khoản, Lưu Profile 1:1, Trùng lặp email. |
| **Identity Unit Tests (Login & Lockout)** | 81 / 81 | **PASS (100%)** | PR #17 | Dummy BCrypt verify, Ký RS256 JWT, Refresh Token hash, Lockout 5 lần/15 phút, Status guards. |
| **Identity Unit Tests (Email OTP)** | 64 / 64 | **PASS (100%)** | PR #18 | Keyed HMAC-SHA256, FixedTimeEquals, CSPRNG 6 số, Cooldown 60s, Quota 5/giờ, Invalidation. |
| **Notification Unit Tests** | 2 / 2 | **PASS (100%)** | PR #18 | SmtpEmailSender định dạng HTML, ném ngoại lệ khi lỗi transport để kích hoạt Retry/DLQ. |
| **Architecture Tests** | 16 / 16 | **PASS (100%)** | Cả 3 PR | Kiểm tra ranh giới Clean Architecture, Domain không phụ thuộc Infrastructure, cấm phụ thuộc vòng. |
| **WebFoundation Tests** | 13 / 13 | **PASS (100%)** | Cả 3 PR | Kiểm thử kiến trúc WebAPI và Gateway. |
| **Git Policy Tests** | 6 / 6 | **PASS (100%)** | Cả 3 PR | Kiểm thử quy chuẩn đặt tên branch, Conventional Commits và loại bỏ merge commit. |

---

## 9. Bảng Ánh xạ Mã nguồn Toàn diện theo từng PR (Source Code Map)

Dưới đây là danh mục toàn bộ các file mã nguồn cốt lõi phân theo từng PR và Clean Architecture:

| Layer | Đường dẫn file | Thuộc PR | Vai trò & Trách nhiệm |
| :--- | :--- | :---: | :--- |
| **Domain** | `src/Services/Identity/Identity.Domain/Accounts/Account.cs` | PR #17, #18, #19 | Aggregate Root tài khoản, quản lý email, trạng thái, mật khẩu, lockout tracking. |
| **Domain** | `src/Services/Identity/Identity.Domain/Profiles/Profile.cs` | **PR #19** | Thực thể hồ sơ người dùng (AccountId 1:1, FullName, DateOfBirth, audit dates). |
| **Domain** | `src/Services/Identity/Identity.Domain/EmailVerifications/EmailVerification.cs` | **PR #18** | Thực thể xác thực OTP (thời hạn 5 phút, max 5 attempts, cooldown 60s, max 5/giờ). |
| **Domain** | `src/Services/Identity/Identity.Domain/EmailVerifications/IOtpHasher.cs` | **PR #18** | Cổng trừu tượng băm và kiểm tra mã OTP. |
| **Domain** | `src/Services/Identity/Identity.Domain/Accounts/RefreshToken.cs` | **PR #17** | Thực thể Refresh Token (TokenHash SHA-256, thời hạn 7 ngày, thu hồi). |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/RegisterAccount/RegisterAccountCommand.cs` | **PR #19** | Command, FluentValidator và Handler cho luồng đăng ký tài khoản & hồ sơ. |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/VerifyEmailOtp/VerifyEmailOtpCommand.cs` | **PR #18** | Command và Handler xác thực OTP với row-level lock `SELECT FOR UPDATE`. |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/ResendOtp/ResendOtpCommand.cs` | **PR #18** | Command và Handler gửi lại mã OTP, vô hiệu hóa mã cũ và kiểm tra quota. |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/Login/LoginCommand.cs` | **PR #17** | Command và Handler đăng nhập tài khoản, timing-safe dummy verify, lockout policy. |
| **Application** | `src/Services/Identity/Identity.Application/Profiles/IProfileRepository.cs` | **PR #19** | Cổng giao tiếp lưu trữ và truy vấn hồ sơ người dùng. |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/IAccountRepository.cs` | PR #18 | Cổng giao tiếp lưu trữ tài khoản kèm phương thức `FindByEmailForUpdateAsync`. |
| **Infrastructure** | `src/Services/Identity/Identity.Infrastructure/Security/HmacSha256OtpHasher.cs` | **PR #18** | Triển khai băm OTP HMAC-SHA256 với server pepper và `FixedTimeEquals`. |
| **Infrastructure** | `src/Services/Identity/Identity.Infrastructure/Security/SecureOtpGenerator.cs` | **PR #18** | Sinh mã OTP 6 chữ số ngẫu nhiên an toàn bằng CSPRNG. |
| **Infrastructure** | `src/Services/Identity/Identity.Infrastructure/Security/JwtTokenIssuer.cs` | **PR #17** | Ký Asymmetric JWT RS256 có cache RSA instance và private key. |
| **Infrastructure** | `src/Services/Identity/Identity.Infrastructure/Security/BCryptPasswordHasher.cs` | PR #17, #19 | Băm và xác thực mật khẩu bằng BCrypt work factor 11. |
| **Persistence** | `src/Services/Identity/Identity.Persistence/Profiles/ProfileConfiguration.cs` | **PR #19** | Cấu hình EF Core mapping bảng `identity.profiles`, quan hệ 1:1 cascade. |
| **Persistence** | `src/Services/Identity/Identity.Persistence/Profiles/ProfileRepository.cs` | **PR #19** | Triển khai truy xuất và thêm mới hồ sơ người dùng. |
| **Persistence** | `src/Services/Identity/Identity.Persistence/Accounts/AccountRepository.cs` | PR #18 | Triển khai truy xuất tài khoản và khóa dòng bi quan Postgres `SELECT FOR UPDATE`. |
| **Persistence** | `src/Services/Identity/Identity.Persistence/Migrations/20261006075245_AddProfilesTable.cs` | **PR #19** | Migration tạo bảng `identity.profiles` có khóa ngoại cascade trỏ `accounts(id)`. |
| **WebAPI** | `src/Services/Identity/Identity.WebAPI/Endpoints/AccountEndpoints.cs` | Cả 3 PR | Endpoints RESTful: POST `/accounts`, POST `/verify-email`, POST `/resend-otp`, POST `/login`. |
| **Notification** | `src/Services/Notification/Notification.Infrastructure/Email/SmtpEmailSender.cs` | **PR #18** | Gửi email SMTP thực tế, format HTML, ném ngoại lệ kích hoạt Retry / DLQ. |
| **Notification** | `src/Services/Notification/Notification.Infrastructure/Email/LoggingEmailSender.cs` | **PR #18** | Fallback gửi email qua console log cho môi trường local dev. |
| **Frontend** | `web/src/features/auth/register/RegisterForm.tsx` & `registerSchema.ts` | **PR #19** | Giao diện form đăng ký tài khoản Next.js, Zod schema validation. |
| **CI / CD** | `scripts/ci/check_git_policy.py` & `test_git_policy.py` | Cả 3 PR | Bộ công cụ tự động kiểm tra quy chuẩn nhánh, PR title và commit Conventional. |
| **Unit Tests** | `tests/Identity/Identity.UnitTests/RegisterAccountTests.cs` | **PR #19** | Bộ 54 test unit kiểm thử toàn diện quy chuẩn đăng ký và hồ sơ. |
| **Unit Tests** | `tests/Identity/Identity.UnitTests/EmailOtpTests.cs` | **PR #18** | Bộ test unit kiểm thử toàn diện các case OTP, cooldown, limit, hết hạn. |
| **Unit Tests** | `tests/Identity/Identity.UnitTests/LoginTests.cs` | **PR #17** | Bộ test unit kiểm thử đăng nhập, lockout, RS256 token. |
| **Unit Tests** | `tests/Notification/Notification.UnitTests/SmtpEmailSenderTests.cs` | **PR #18** | Bộ test unit kiểm thử cơ chế re-throw lỗi của SMTP sender. |
