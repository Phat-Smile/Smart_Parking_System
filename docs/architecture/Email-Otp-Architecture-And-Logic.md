# TÀI LIỆU KỸ THUẬT TOÀN DIỆN: THIẾT KẾ KIẾN TRÚC, CODING & LOGIC NGHIỆP VỤ HỆ THỐNG XÁC THỰC VÀ ĐỊNH DANH (IDENTITY & AUTHENTICATION)

## Smart Parking Management System

> **Dự án:** Smart Parking Management System  
> **Phạm vi 3 Pull Requests:**  
> - **PR #18**: `feat(auth): implement email otp verification and resend` — Tính năng `US-AUTH-003` (Email OTP Verification, HMAC-SHA256 Pepper, CSPRNG 6 số, Row Locking `SELECT FOR UPDATE`, Outbox, RabbitMQ, Hybrid Email Dispatch).  
> - **PR #17**: `feat(auth): implement user login` — Tính năng `US-AUTH-004 & US-AUTH-008` (User Login, Dummy BCrypt Timing Attack Mitigation, RS256 Asymmetric JWT Issuer, Refresh Token SHA-256 Storage & Rotation, Account Lockout Policy 3 lần/15 phút).  
> - **PR #19**: `feat(auth): implement user registration` — Tính năng `US-AUTH-001 Tinh giản` (Họ tên Unicode tiếng Việt, Chuẩn hóa Email, Số điện thoại Việt Nam tùy chọn, Mặc định Role = Customer, Entity `Profile` quan hệ 1:1 Cascade DB, Frontend Registration Page).  
> **Kiến trúc áp dụng:** Clean Architecture, Domain-Driven Design (DDD), Transactional Outbox Pattern, Event-Driven Architecture (EDA) qua RabbitMQ, Idempotent Consumer, Single Synchronization Point (PostgreSQL Row Lock), Asymmetric Cryptography (RS256).  
> **Trạng thái:** Bản thảo Kiến trúc Đang Rà Soát (Architecture Proposal under Review — Chờ Mentor phê duyệt các Open Decisions) | Unit & Architecture Tests đạt 100% Green.

---

## 📑 MỤC LỤC
1. [Bảng Ánh xạ Phân chia 3 Pull Requests Độc lập](#1-bảng-ánh-xạ-phân-chia-3-pull-requests-độc-lập)
2. [Tổng quan Kiến trúc Hệ thống & Luồng Dữ liệu Toàn trình (EDA)](#2-tổng-quan-kiến-trúc-hệ-thống--luồng-dữ-liệu-toàn-trình-eda)
3. [Chi tiết Coding & Logic PR #19: Đăng ký Tài khoản & Hồ sơ (US-AUTH-001 Tinh giản)](#3-chi-tiết-coding--logic-pr-19-đăng-ký-tài-khoản--hồ-sơ-us-auth-001-tinh-giản)
   - 3.1 [Domain Layer: Entity `Account` & Entity `Profile`](#31-domain-layer-entity-account--entity-profile)
   - 3.2 [Validation Rules: Tên Unicode tiếng Việt, Email chuẩn hóa, SĐT Việt Nam tùy chọn](#32-validation-rules-tên-unicode-tiếng-việt-email-chuẩn-hóa-sđt-việt-nam-tùy-chọn)
   - 3.3 [Application Layer: Command, Validator & Handler](#33-application-layer-command-validator--handler)
   - 3.4 [Persistence Layer: Cấu hình EF Core 1:1 Cascade & Migration Profile](#34-persistence-layer-cấu-hình-ef-core-11-cascade--migration-profile)
   - 3.5 [Frontend Integration: Form Đăng ký Next.js & Kiểm thử Playwright E2E](#35-frontend-integration-form-đăng-ký-nextjs--kiểm-thử-playwright-e2e)
4. [Chi tiết Coding & Logic PR #18: Phát hành & Xác thực Email OTP (US-AUTH-003)](#4-chi-tiết-coding--logic-pr-18-phát-hành--xác-thực-email-otp-us-auth-003)
   - 4.1 [Domain Layer: Entity `EmailVerification` & 5 Quy tắc Bất biến](#41-domain-layer-entity-emailverification--5-quy-tắc-bất-biến)
   - 4.2 [Bảo mật Băm OTP Keyed HMAC-SHA256 (Server Pepper) & Chống Timing Attack](#42-bảo-mật-băm-otp-keyed-hmac-sha256-server-pepper--chống-timing-attack)
   - 4.3 [CSPRNG Generator: Sinh mã OTP 6 số ngẫu nhiên an toàn](#43-csprng-generator-sinh-mã-otp-6-số-ngẫu-nhiên-an-toàn)
   - 4.4 [Chiến lược Đồng thời: PostgreSQL Row-Level Lock `SELECT FOR UPDATE`](#44-chiến-lược-đồng-thời-postgresql-row-level-lock-select-for-update)
   - 4.5 [Application Layer: Xử lý Xác thực (`VerifyEmailOtpCommand`) & Gửi lại (`ResendOtpCommand`)](#45-application-layer-xử-lý-xác-thực-verifyemailotpcommand--gửi-lại-resendotpcommand)
5. [Chi tiết Coding & Logic PR #17: Đăng nhập, RS256 JWT & Khóa tài khoản (US-AUTH-004 & US-AUTH-008)](#5-chi-tiết-coding--logic-pr-17-đăng-nhập-rs256-jwt--khóa-tài-khoản-us-auth-004--us-auth-008)
   - 5.1 [Chống Tấn công Timing Enumeration: Dummy BCrypt Password Verification](#51-chống-tấn-công-timing-enumeration-dummy-bcrypt-password-verification)
   - 5.2 [Hạ tầng Ký Asymmetric JWT Token RS256 & Cache Private Key ([OPEN DECISION] TTL 15m vs 24h)](#52-hạ-tầng-ký-asymmetric-jwt-token-rs256--cache-private-key-open-decision-ttl-15m-vs-24h)
   - 5.3 [Kiến trúc Quản lý Refresh Token & Lưu trữ Băm SHA-256](#53-kiến-trúc-quản-lý-refresh-token--lưu-trữ-băm-sha-256)
   - 5.4 [Chính sách Khóa tài khoản (Account Lockout Policy: 3 lần/15 phút)](#54-chính-sách-khóa-tài-khoản-account-lockout-policy-3-lần15-phút)
   - 5.5 [Kiểm tra Trạng thái Tài khoản: Yêu cầu `EmailVerified`, `Suspended`, `Disabled`](#55-kiểm-tra-trạng-thái-tài-khoản-yêu-cầu-emailverified-suspended-disabled)
   - 5.6 [Phân quyền Người dùng & Vòng đời Cấp phát Role ([PROPOSED] Role Lifecycle)](#56-phân-quyền-người-dùng--vòng-đời-cấp-phát-role-proposed-role-lifecycle)
   - 5.7 [Đóng băng Thứ tự Kiểm tra Đăng nhập (Frozen Login Check Order)](#57-đóng-băng-thứ-tự-kiểm-tra-đăng-nhập-frozen-login-check-order)
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
| **Trọng tâm nghiệp vụ** | Phát hành & xác thực mã OTP, băm HMAC-SHA256, khóa dòng Postgres `SELECT FOR UPDATE`, Outbox EDA, Notification SMTP retry/DLQ. | Xác thực đăng nhập, phòng thủ timing attack qua dummy BCrypt, ký RS256 JWT, Refresh Token SHA-256, lockout 15 phút. | Đăng ký tài khoản tinh giản (Lean Registration: FullName Unicode tiếng Việt, Email chuẩn hóa, Password, SĐT tùy chọn), gán mặc định Role Customer, Entity Profile 1:1, UI Form. |
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
    * `string FullName`: Họ và tên người dùng (2 - 100 ký tự, hỗ trợ Unicode tiếng Việt).
    * `DateOnly? DateOfBirth`: Ngày tháng năm sinh (tùy chọn, cập nhật khi xác minh danh tính/KYC sau).
    * `string? CitizenIdNumber`: Số CCCD / CMND (tùy chọn, cập nhật ở bước hồ sơ).
    * `string? DriverLicenseNumber`: Số giấy phép lái xe (tùy chọn, cập nhật ở bước hồ sơ).
    * `string? AvatarUrl`: Đường dẫn ảnh đại diện (tùy chọn).
    * `DateTimeOffset CreatedAtUtc` & `DateTimeOffset? UpdatedAtUtc`: Dữ liệu audit thời gian.
  * Factory Method `Profile.Create(accountId, fullName, now, dateOfBirth: null)`: Khởi tạo hồ sơ cơ bản từ tên người dùng, các trường CCCD/GPLX/DOB để trống để người dùng bổ sung sau.

### 3.2 Validation Rules: Tên Unicode tiếng Việt, Email chuẩn hóa, SĐT Việt Nam tùy chọn

Theo quyết định tinh giản luồng đăng ký công khai (Lean Registration), API Register chỉ tiếp nhận 4 trường thiết yếu. Logic kiểm thực dữ liệu đầu vào được quản lý tập trung tại [RegisterAccountCommandValidator](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Application/Accounts/RegisterAccount/RegisterAccountCommand.cs):

```csharp
public sealed partial class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    public RegisterAccountCommandValidator()
    {
        // 1. Họ và tên: 2 - 100 ký tự, hỗ trợ đầy đủ Unicode tiếng Việt có dấu
        RuleFor(command => command.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("FullName is required.")
            .Must(name => name.Trim().Length >= 2).WithMessage("FullName must be at least 2 characters.")
            .Must(name => name.Trim().Length <= Profile.MaxFullNameLength).WithMessage($"FullName must not exceed {Profile.MaxFullNameLength} characters.")
            .Must(name => FullNameRegex().IsMatch(name.Trim())).WithMessage("FullName must only contain letters, spaces, hyphens, or apostrophes.");

        // 2. Email: chuẩn định dạng email, tối đa 254 ký tự (RFC 5321)
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        // 3. Mật khẩu: 8 - 72 ký tự, gồm chữ hoa, chữ thường, số, ký tự đặc biệt (giới hạn 72 bytes do thuật toán BCrypt)
        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8)
            .MaximumLength(72)
            .Matches("[A-Z]").WithMessage("Password must contain an upper-case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower-case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^A-Za-z0-9]").WithMessage("Password must contain a special character.");

        // 4. Số điện thoại (Tùy chọn): nếu nhập phải đúng định dạng di động Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx)
        RuleFor(command => command.PhoneNumber)
            .Matches(@"^\s*(0|\+84)[35789][0-9]{8}\s*$")
            .WithMessage("Phone number must be a Vietnamese mobile number: 0 or +84 followed by 9 digits starting with 3, 5, 7, 8 or 9.")
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));
    }

    [GeneratedRegex(@"^[\p{L}]+(?:[ '-][\p{L}]+)*$")]
    private static partial Regex FullNameRegex();
}
```

> [!NOTE]
> **Quy chuẩn Scope Đăng ký & Tách biệt Trách nhiệm**:
> - Client **tuyệt đối không được gửi role** (Hệ thống tự động gán `AccountRole.Customer`).
> - Thông tin Ngày sinh (`DateOfBirth`), CCCD (`CitizenIdNumber`), Giấy phép lái xe (`DriverLicenseNumber`), Phương tiện xe (`Vehicle`), Ảnh đại diện (`Avatar`) được tách biệt hoàn toàn và chuyển sang trang **Hồ sơ cá nhân (Profile / KYC)** sau khi người dùng đăng nhập.

### 3.3 Application Layer: Command, Validator & Handler

* **Command**: `RegisterAccountCommand(string FullName, string Email, string Password, string? PhoneNumber)`.
* **Handler (`RegisterAccountCommandHandler`)**:
  1. Chuẩn hóa Email: `Account.NormalizeEmail(request.Email)` (`Trim().ToLowerInvariant()`).
  2. Băm mật khẩu bằng `BCryptPasswordHasher` (work factor 11).
  3. Khởi tạo thực thể `Account` với trạng thái `PendingEmailVerification` và vai trò mặc định `Customer`.
  4. Khởi tạo thực thể `Profile` với `accountId = account.Id` và `FullName = request.FullName`.
  5. Phát hành mã OTP 6 số qua `IOtpGenerator`, băm HMAC-SHA256 lưu `EmailVerification`, ghi nhận Outbox event `UserVerificationOtpIssuedIntegrationEvent`.
  6. Lưu DB qua `accounts.TrySaveChangesAsync(cancellationToken)`. Nếu xảy ra race condition khi có 2 request đăng ký cùng email cùng lúc, lệnh commit sẽ vi phạm Unique Index `ux_accounts_email` trên Postgres, handler bắt lỗi và trả về mã lỗi `identity.email_already_registered` (HTTP 409 Conflict).
  7. Trả về `Result<RegisteredAccount>.Success`.

### 3.4 Persistence Layer: Cấu hình EF Core 1:1 Cascade & Migration Profile

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
        builder.Property(profile => profile.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date").IsRequired(false);
        
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
2. **Giới hạn số lần thử**: Cho phép tối đa 3 lần nhập sai (`MaxAttempts = 3`). Vượt quá giới hạn (lần thứ 3 nhập sai), mã OTP bị vô hiệu hóa ngay lập tức (trả về lỗi `identity.otp_max_attempts_exceeded`) và người dùng bắt buộc phải yêu cầu gửi lại mã OTP mới qua Resend OTP.
3. **Thời gian chờ giữa các lần gửi (Cooldown)**: [PROPOSED / OPEN DECISION] Tối thiểu 60 giây giữa 2 lần yêu cầu gửi lại mã (`ResendCooldownSeconds = 60`) nhằm chống flood request và tiết kiệm chi phí gửi email, chờ Mentor/team phê duyệt tham số chính thức.
4. **Giới hạn phát hành OTP trong 1 giờ**: Tối đa phát hành 5 mã OTP / 1 giờ / 1 email (`MaxHourlyOtpIssued = 5`), tính gộp cả OTP phát hành lần đầu lúc Đăng ký (Register) lẫn các lần Gửi lại (Resend). Với mỗi mã có tối đa 3 lần thử, tương ứng tối đa 15 lần thử/giờ.
5. **Vòng đời vô hiệu hóa (Invalidation Lifecycle)**: Khi người dùng yêu cầu gửi lại OTP mới, bản ghi OTP cũ chưa sử dụng sẽ được đánh dấu `IsInvalidated = true`, ngăn chặn việc dùng lại mã OTP cũ.

### 4.2 Bảo mật Băm OTP Keyed HMAC-SHA256 (Server Pepper) & Chống Timing Attack

* **Vấn đề bảo mật**: Không bao giờ lưu trữ OTP dạng thô (plain-text) trong bảng nghiệp vụ (`email_verifications`). Vì không gian mẫu của mã OTP 6 chữ số rất nhỏ (chỉ $10^6$ trường hợp), kẻ tấn công nếu truy cập được cơ sở dữ liệu có thể dùng Rainbow Table giải mã toàn bộ OTP trong tích tắc nếu dùng thuật toán băm thông thường (MD5, SHA-256). Mã OTP dạng thô chỉ xuất hiện tạm thời trong payload sự kiện Outbox (`identity.outbox_messages`) để Worker phục vụ gửi email qua Event-Driven Architecture và được dọn dẹp sau khi gửi.
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
  4. Kiểm tra OTP hết hạn (quá 5 phút) hoặc đã vượt quá 3 lần thử (`MaxAttempts = 3`).
  5. Gọi `otpHasher.VerifyOtp`:
     * Nếu sai: Tăng số lần thử `FailedAttempts++`, lưu DB và trả về lỗi.
     * Nếu đúng: Đánh dấu `IsUsed = true`, chuyển trạng thái `Account.Status = EmailVerified`.
  6. Enqueue sự kiện `AccountEmailVerifiedIntegrationEvent` vào bảng Outbox.
  7. Commit transaction.

* **Luồng Gửi lại (`ResendOtpCommand`)**:
  1. Mở transaction và lấy khóa dòng `Account` qua `SELECT FOR UPDATE`.
  2. Kiểm tra cooldown [PROPOSED] (mặc định 60s) và quota phát hành tối đa 5 mã OTP trong 1 giờ (`MaxHourlyOtpIssued = 5`) tính cả OTP khởi tạo lúc đăng ký.
  3. Đánh dấu vô hiệu hóa toàn bộ OTP cũ của tài khoản (`IsInvalidated = true`).
  4. Sinh mã OTP 6 số mới qua CSPRNG, băm HMAC-SHA256 và lưu bản ghi mới.
  5. Enqueue sự kiện `UserVerificationOtpIssuedIntegrationEvent` vào Outbox.
  6. Commit transaction.

---

## 5. Chi tiết Coding & Logic PR #17: Đăng nhập, RS256 JWT & Khóa tài khoản (US-AUTH-004 & US-AUTH-008)

### 5.1 Chống Tấn công Timing Enumeration: Dummy BCrypt Password Verification

* **Nguy cơ User Enumeration**: Khi kẻ tấn công gửi request đăng nhập với một email bất kỳ, nếu backend kiểm tra `if (account == null) return Failure()`, thời gian phản hồi của request sẽ cực nhanh (~2ms). Ngược lại, nếu email tồn tại và backend chạy hàm `BCrypt.Verify()` (tốn ~80ms-150ms do work factor 11), kẻ tấn công có thể đo độ trễ mạng để dò tìm toàn bộ danh sách email hợp lệ trong hệ thống.
* **Giải pháp Dummy Verification chuẩn xác trong [BCryptPasswordHasher.cs](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Infrastructure/Security/BCryptPasswordHasher.cs)**:
  ```csharp
  public sealed class BCryptPasswordHasher : IPasswordHasher
  {
      private const int WorkFactor = 11;
      // Khởi tạo một lần duy nhất lúc nạp static class, đảm bảo cú pháp BCrypt chuẩn xác, không ném ngoại lệ SaltParseException
      private static readonly string DummyHash =
          BCrypt.Net.BCrypt.HashPassword("DummyTimingMitigationPassword", WorkFactor);

      public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
      public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
      public void VerifyDummy(string password) => BCrypt.Net.BCrypt.Verify(password, DummyHash);
  }
  ```
* Khi không tìm thấy tài khoản trong database, `LoginCommandHandler` gọi trực tiếp:
  ```csharp
  if (account is null)
  {
      passwordHasher.VerifyDummy(request.Password);
      return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
  }
  ```
  Hàm `VerifyDummy` tiêu tốn thời gian tính toán của CPU tương đương hoàn toàn với việc kiểm tra một tài khoản có thật (~50-100ms), triệt tiêu hoàn toàn khả năng nhận biết tài khoản tồn tại dựa vào thời gian phản hồi (timing side-channel).

### 5.2 Hạ tầng Ký Asymmetric JWT Token RS256 & Cache Private Key ([OPEN DECISION] TTL 15m vs 24h)

* **Thuật toán RS256**: Sử dụng cặp khóa bất đối xứng RSA:
  * Khóa bí mật (**RSA Private Key**): Chỉ duy nhất Identity Service nắm giữ để ký số Token.
  * Khóa công khai (**RSA Public Key**): Các service khác (Parking, Booking, Payment) hoặc API Gateway sử dụng để kiểm tra chữ ký số mà không bao giờ cần biết khóa bí mật.
* **Tối ưu Caching trong [JwtTokenIssuer.cs](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Infrastructure/Security/JwtTokenIssuer.cs)**:
  * Khởi tạo và nạp RSA Private Key từ cấu hình (.pem / secret vault). Hệ thống fail-fast ngay lúc khởi động (`ValidateOnStart`) nếu thiếu private key, tuyệt đối không tự sinh ngẫu nhiên trên RAM trong môi trường Production để bảo đảm tính nhất quán chữ ký trên toàn cụm đa instance và không làm mất phiên đăng nhập khi restart pod.
  * Cache đối tượng `SigningCredentials` và `RsaSecurityKey` trong suốt vòng đời ứng dụng (Singleton), loại bỏ chi phí khởi tạo lại RSA key cho mỗi request (giảm CPU overhead đáng kể).
  * Claims trong Token:
    * `sub`: Mã định danh tài khoản (`account.Id`).
    * `email`: Địa chỉ email.
    * `role`: Vai trò người dùng (`Customer`, `Staff`, `ParkingLotOwner`, `SystemAdmin`) dùng cho downstream services (Parking, Booking, Gate, Payment) authorize.
    * `jti`: Mã định danh duy nhất của Token (phục vụ audit log và distributed tracing; hiện chưa hỗ trợ thu hồi token trực tiếp do không duy trì blacklist phân tán).
    * `exp`: Thời hạn hiệu lực của Access Token.
* > [!IMPORTANT]
  > **[OPEN DECISION / CONFLICT] Thời hạn hiệu lực Access Token: Mặc định 15 Phút + Configurable (Chờ Mentor phê duyệt 15 Phút vs 24 Giờ)**:  
  > - **Hiện trạng triển khai**: Hệ thống áp dụng cấu hình mặc định **15 phút** (`expiresIn = 900`, `ExpirationMinutes = 15`) theo khuyến nghị bảo mật OWASP API Security và OAuth 2.0 Best Current Practice (giảm thiểu rủi ro khi Access Token bị rò rỉ). Trải nghiệm người dùng duy trì liền mạch thông qua Refresh Token rotation (7 ngày) chạy ngầm dưới background.  
  > - **Khả năng cấu hình (Configurable)**: Tham số được liên kết động qua `Security:Jwt:ExpirationMinutes` trong `appsettings.json`, cho phép tùy biến thời hạn khi phát triển, kiểm thử hoặc thay đổi chính sách mà không cần sửa code.  
  > - **Trạng thái quyết định**: Đang chờ Mentor và Solution Architect chốt chính thức giữa:  
  >   * **Phương án 1 (Khuyến nghị OWASP)**: **15 Phút** (Security-First + Refresh Token Rotation 7 ngày).  
  >   * **Phương án 2**: **24 Giờ** (Simplicity-First — đơn giản hóa phía client nếu hạn chế gọi Refresh Token).

### 5.3 Kiến trúc Quản lý Refresh Token & Lưu trữ Băm SHA-256

* **Thực thể `RefreshToken` (`Identity.Domain/Accounts/RefreshToken.cs`)**:
  * `Guid Id`: Khóa chính.
  * `Guid AccountId`: Khóa ngoại tham chiếu `Account.Id`.
  * `string TokenHash`: Giá trị băm SHA-256 của Refresh Token (chuỗi token thô 64 ký tự CSPRNG chỉ trả về cho client đúng 1 lần duy nhất).
  * `DateTimeOffset ExpiresAtUtc`: Thời hạn hiệu lực (7 ngày).
  * `DateTimeOffset? RevokedAtUtc`: Thời gian bị thu hồi.
* **Cơ chế Xoay vòng (Rotation)**: Mỗi khi dùng Refresh Token để lấy cặp Token mới, Refresh Token cũ sẽ bị thu hồi ngay lập tức (`RevokedAtUtc = now`) và một Refresh Token mới được cấp phát.

### 5.4 Chính sách Khóa tài khoản (Account Lockout Policy: 3 lần/15 phút)

Theo yêu cầu nghiệp vụ `FR-USER-008` (Mục 6.1 Context & Master Sheet):
1. **Theo dõi đăng nhập sai**:
   * Thuộc tính `FailedLoginAttempts` và `LockoutEndUtc` trên `Account`.
   * Khóa bi quan PostgreSQL (`SELECT FOR UPDATE`) trong `ExecuteInTransactionAsync` tuần tự hóa các yêu cầu đăng nhập sai đồng thời, chống triệt để race condition và lỗi xung đột đồng thời.
   * Khi người dùng nhập sai mật khẩu: `FailedLoginAttempts++`.
2. **Kích hoạt khóa**:
   * Khi `FailedLoginAttempts >= 3`: Thiết lập `LockoutEndUtc = clock.UtcNow.AddMinutes(15)`.
   * Các request tiếp theo trong vòng 15 phút sẽ bị từ chối ngay lập tức với lỗi `identity.account_temporarily_locked` (HTTP 423 Locked).
3. **Mở khóa và Reset**:
   * Khi hết thời gian 15 phút: Tài khoản tự động được phép thử lại (`ClearExpiredLockout`).
   * Khi người dùng đăng nhập thành công với mật khẩu chính xác: `FailedLoginAttempts = 0` và `LockoutEndUtc = null` (`RecordCorrectCredential`).

### 5.5 Kiểm tra Trạng thái Tài khoản: Yêu cầu `EmailVerified`, `Suspended`, `Disabled`

Hệ thống bảo vệ đăng nhập qua ma trận trạng thái:
* `PendingEmailVerification`: Trả về `identity.email_not_verified` (HTTP 403 Forbidden).
* `Suspended`: Tài khoản bị tạm khóa do vi phạm hoặc điều tra -> Trả về `identity.account_suspended` (HTTP 403 Forbidden).
* `Disabled`: Tài khoản bị vô hiệu hóa vĩnh viễn -> Trả về `identity.account_disabled` (HTTP 403 Forbidden).
* `EmailVerified`: Trạng thái hợp lệ duy nhất cho phép phát hành token (HTTP 200 OK).

### 5.6 Phân quyền Người dùng & Vòng đời Cấp phát Role ([PROPOSED] Role Lifecycle — Sprint 2 / Backoffice Spec)

Hệ thống định nghĩa 4 Role trong enum [`AccountRole`](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Domain/Accounts/AccountRole.cs):
1. **`Customer`** (Người dùng cuối / Khách gửi xe) — **[CONFIRMED BASELINE]**:
   * Tự động gán mặc định cho tất cả tài khoản tạo qua Public Registration. Client không được truyền role khi đăng ký.
2. **`Staff`** (Nhân viên soát vé / vận hành bãi xe) — **[PROPOSED — SPRINT 2 / BACKOFFICE SPEC]**:
   * Không thể tự đăng ký. [PROPOSED] Được cấp tài khoản bởi `ParkingLotOwner` hoặc `SystemAdmin` qua Backoffice Portal (chờ chốt đặc tả Backoffice).
3. **`ParkingLotOwner`** (Chủ bãi đỗ xe đối tác) — **[PROPOSED — SPRINT 2 / BACKOFFICE SPEC]**:
   * Không thể tự đăng ký public. [PROPOSED] Trải qua quy trình Onboarding Đối tác, nộp hồ sơ pháp lý bãi đỗ, được `SystemAdmin` thẩm định KYC và phê duyệt cấp quyền (chờ chốt quy trình Onboarding).
4. **`SystemAdmin`** (Quản trị viên nền tảng) — **[PROPOSED — SPRINT 2 / BACKOFFICE SPEC]**:
   * [PROPOSED] Được khởi tạo ban đầu qua Migration Database Seed hoặc được cấp quyền bởi Super Admin (chờ chốt quy trình quản trị hạ tầng).

> [!NOTE]
> **[SCOPE BOUNDARY]**: Vòng đời cấp phát (lifecycle) cho các vai trò `Staff`, `ParkingLotOwner`, `SystemAdmin` hiện được đánh dấu là **[PROPOSED]** và thuộc phạm vi thiết kế của Sprint 2 (Backoffice & Admin Management), không nằm trong các requirement đã chốt của Sprint 1 Identity Baseline.

### 5.7 Đóng băng Thứ tự Kiểm tra Đăng nhập (Frozen Login Check Order)

Toàn bộ quy trình đăng nhập trong [`LoginCommand.cs`](file:///f:/Ky_5/Smart_Parking_System/src/Services/Identity/Identity.Application/Accounts/Login/LoginCommand.cs) được đóng băng bất biến theo trình tự:
1. `SELECT FOR UPDATE` khóa dòng dữ liệu theo email đã chuẩn hóa.
2. Kiểm tra `account is null`:
   * Nếu null $\rightarrow$ Thực thi `passwordHasher.VerifyDummy(request.Password)` $\rightarrow$ Trả về **`401 Unauthorized`** (`identity.invalid_credentials`).
3. Kiểm tra `account.IsLockedOut(now)`:
   * Nếu đang bị khóa $\rightarrow$ Trả về **`423 Locked`** (`identity.account_temporarily_locked`).
4. Xóa khóa quá hạn `account.ClearExpiredLockout(now)`.
5. Xác thực mật khẩu `passwordHasher.Verify(request.Password, account.PasswordHash)`:
   * **Nếu SAI**:
     * `account.RecordFailedLogin(now)`.
     * Nếu $\ge 3$ lần $\rightarrow$ khóa 15 phút.
     * Lưu database.
     * Trả về **`423 Locked`** (nếu vừa bị khóa) hoặc **`401 Unauthorized`** (`identity.invalid_credentials`).
   * **Nếu ĐÚNG**:
     * Reset chuỗi thất bại `account.RecordCorrectCredential()`.
6. Kiểm tra Trạng thái tài khoản:
   * `Suspended` $\rightarrow$ Lưu DB $\rightarrow$ Trả về **`403 Forbidden`** (`identity.account_suspended`).
   * `Disabled` $\rightarrow$ Lưu DB $\rightarrow$ Trả về **`403 Forbidden`** (`identity.account_disabled`).
   * Khác `EmailVerified` $\rightarrow$ Lưu DB $\rightarrow$ Trả về **`403 Forbidden`** (`identity.email_not_verified`).
7. Đăng nhập thành công:
   * Sinh `RefreshToken` (raw 64 ký tự hex, lưu hash SHA-256 vào DB).
   * Commit transaction nguyên tử.
   * Ký JWT Access Token RS256 (thời hạn 15 phút, claim `sub`, `email`, `role`, `jti`).
   * Trả về **`200 OK`** (`accessToken`, `tokenType: "Bearer"`, `expiresIn: 900`, `refreshToken`, `account: { id, email, role, status }`).

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
  * Chạy định kỳ, sử dụng cơ chế **Lease Lock** (`locked_until_utc`) để nhiều instance Identity Service có thể scale ngang với cam kết gửi ít nhất một lần (**At-least-once delivery**). Việc khử trùng lặp (de-duplication) được đảm bảo ở phía consumer qua mẫu Idempotent Inbox.
  * Đọc bản ghi outbox, publish lên RabbitMQ exchange, sau đó đánh dấu `processed_at_utc = now`.

### 6.3 RabbitMQ Consumer & Idempotent Inbox tại Notification Service

* **Consumer**: `UserVerificationOtpIssuedConsumer` tại Notification Service đăng ký lắng nghe sự kiện qua MassTransit.
* **Quy trình Thực thi Idempotent Inbox & Cơ chế Transactional Rollback**:
  * `NotificationInboxExecutionCoordinator` mở 1 Database Transaction cục bộ:
    ```sql
    INSERT INTO "notification"."inbox_messages" ("message_id", "consumer_key", "received_at_utc", "processed_at_utc")
    VALUES (@messageId, @consumerKey, @now, @now)
    ON CONFLICT ("message_id", "consumer_key") DO NOTHING;
    ```
  * **Nếu bản tin đã xử lý trước đó** (`inserted == 0`): Rollback transaction và trả về `InboxExecutionResult.Duplicate` $\rightarrow$ Bỏ qua, ngăn chặn gửi email lần 2 khi RabbitMQ re-deliver.
  * **Nếu bản tin mới** (`inserted == 1`): Thực thi logic gọi `IEmailSender.SendVerificationOtpEmailAsync(...)`.
    * **Trường hợp gửi email thành công**: Cập nhật `processed_at_utc = now`, gọi `transaction.CommitAsync()` $\rightarrow$ Bản ghi inbox được lưu vĩnh viễn và commit thành công.
    * **Trường hợp SMTP gặp sự cố (Timeout, Network Failure, SMTP error)**: Bắt ngoại lệ trong khối `catch`, kích hoạt `transaction.RollbackAsync()` $\rightarrow$ **Toàn bộ bản ghi inbox bị rollback hoàn toàn khỏi cơ sở dữ liệu**! Lỗi sau đó được `throw;` lại để MassTransit kích hoạt Retry Policy. Khi MassTransit thử lại sau vài giây, vì bảng inbox chưa ghi nhận message này, Consumer được phép thực thi lại để gửi email thành công.

### 6.4 Hạ tầng Gửi Email Linh hoạt & Cơ chế Retry / DLQ qua MassTransit

Notification Service hỗ trợ 3 chiến lược gửi email theo môi trường:
1. **Môi trường Development không có SMTP (`LoggingEmailSender`)**:
   * Ghi log mã OTP và tiêu đề email trực tiếp ra console, giúp lập trình viên test luồng nhanh chóng mà không cần kết nối mạng.
2. **Môi trường Test Container (`Mailpit`)**:
   * Chạy Mailpit qua Docker Compose (port 1025 SMTP, port 8025 Web UI) để kiểm tra giao diện HTML email thực tế.
3. **Môi trường Production (`SmtpEmailSender`)**:
   * Kết nối máy chủ SMTP thật (Google SMTP, SendGrid, Amazon SES) qua kết nối TLS bảo mật.
   * **Cơ chế Retry & DLQ**: Khi xảy ra lỗi mạng hoặc timeout SMTP, `SmtpEmailSender` ném lại ngoại lệ (`throw;`). Nhờ cơ chế Transactional Rollback của Inbox, MassTransit tự động retry với exponential backoff mà không bị bộ lọc inbox chặn nhầm. Nếu vượt quá số lần retry tối đa, message được chuyển vào **Dead Letter Queue (DLQ)** (`_error` queue) để giám sát và cảnh báo.

---

## 7. Đặc tả Hợp đồng API (API Contract Specification)

### 7.1 Đăng ký tài khoản (`POST /api/identity/accounts`) — [PR #19](https://github.com/Phat-Smile/Smart_Parking_System/pull/19)

* **Request Body** (Luồng Tinh giản - Lean Registration):
```json
{
  "fullName": "Nguyễn Văn An",
  "email": "user@example.com",
  "password": "Password#123",
  "phoneNumber": "0901234567"
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
  * `400 Bad Request`: Vi phạm validation (họ tên không hợp lệ, email sai định dạng, mật khẩu yếu, số điện thoại không đúng chuẩn di động Việt Nam).
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
  * `400 Bad Request`: `errorCode: "identity.invalid_otp"` (Sai mã OTP).
  * `400 Bad Request`: `errorCode: "identity.otp_expired"` (OTP hết hạn hoặc không tìm thấy phiên xác thực hợp lệ).
  * `400 Bad Request`: `errorCode: "identity.otp_max_attempts_exceeded"` (Nhập sai quá 3 lần, mã OTP bị vô hiệu hóa).
  * > [!NOTE]
    > **[SECURITY / ANTI-ENUMERATION — OPEN DECISION CONFIRMED]**: Endpoint Verify OTP là public endpoint, hệ thống chủ động **không trả về 404 Not Found** khi email không tồn tại hoặc sai trạng thái, mà trả về lỗi generic `identity.otp_expired` (HTTP 400) như đã triển khai trong `VerifyEmailOtpCommand.cs`, nhằm triệt tiêu hoàn toàn nguy cơ rò rỉ sự tồn tại của tài khoản (User Enumeration Attack).

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
  * `429 Too Many Requests`: `errorCode: "identity.otp_cooldown_active"` (Chưa hết thời gian cooldown 60s [PROPOSED] hoặc vượt quá quota phát hành 5 mã OTP/giờ).
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
  "tokenType": "Bearer",
  "expiresIn": 900,
  "refreshToken": "d8f1e2c3b4a5968746352415...",
  "account": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "role": "Customer",
    "status": "EmailVerified"
  }
}
```
* **Mã lỗi RFC 7807**:
  * `401 Unauthorized`: `errorCode: "identity.invalid_credentials"` (Sai email hoặc mật khẩu, chạy dummy verify chống timing attack).
  * `423 Locked`: `errorCode: "identity.account_temporarily_locked"` (Tài khoản đang bị khóa 15 phút do nhập sai mật khẩu 3 lần liên tiếp).
  * `403 Forbidden`: `errorCode: "identity.email_not_verified"` (Chưa xác thực email).
  * `403 Forbidden`: `errorCode: "identity.account_suspended"` (Tài khoản đang bị tạm đình chỉ).
  * `403 Forbidden`: `errorCode: "identity.account_disabled"` (Tài khoản đã bị vô hiệu hóa).

---

## 8. Ma trận Kiểm thử Tự động & Kết quả Nghiệm thu (Testing & QA)

Hệ thống được bảo vệ bởi bộ kiểm thử tự động toàn diện trên cả 3 tầng:

### 8.1 Thống kê Test Suites

| Bộ kiểm thử | Số lượng test | Trạng thái | Thuộc PR | Nội dung kiểm thử chính |
| :--- | :---: | :---: | :---: | :--- |
| **Identity Unit Tests (Registration)** | 50 / 50 | **PASS (100%)** | PR #19 | Tên Unicode tiếng Việt, Chuẩn hóa email, Phone Việt Nam, Lưu Profile 1:1, Trùng lặp email. |
| **Identity Unit Tests (Login & Lockout)** | 85 / 85 | **PASS (100%)** | PR #17 | Dummy BCrypt timing verify, Ký RS256 JWT, Refresh Token hash, Lockout 3 lần/15 phút, Role claims, Suspended/Disabled guards, Concurrency row-locking. |
| **Identity Unit Tests (Email OTP)** | 64 / 64 | **PASS (100%)** | PR #18 | Keyed HMAC-SHA256, FixedTimeEquals, CSPRNG 6 số, Max 3 attempts, Cooldown 60s, Quota 5/giờ, Invalidation. |
| **Notification Unit Tests** | 2 / 2 | **PASS (100%)** | PR #18 | SmtpEmailSender định dạng HTML, ném ngoại lệ khi lỗi transport để kích hoạt Retry/DLQ. |
| **Architecture Tests** | 16 / 16 | **PASS (100%)** | Cả 3 PR | Kiểm tra ranh giới Clean Architecture, Domain không phụ thuộc Infrastructure, cấm phụ thuộc vòng. |
| **WebFoundation Tests** | 13 / 13 | **PASS (100%)** | Cả 3 PR | Kiểm thử kiến trúc WebAPI và Gateway. |
| **Git Policy Tests** | 6 / 6 | **PASS (100%)** | Cả 3 PR | Kiểm thử quy chuẩn đặt tên branch, Conventional Commits và loại bỏ merge commit. |

---

## 9. Bảng Ánh xạ Mã nguồn Toàn diện theo từng PR (Source Code Map)

Dưới đây là danh mục toàn bộ các file mã nguồn cốt lõi phân theo từng PR và Clean Architecture:

| Layer | Đường dẫn file | Thuộc PR | Vai trò & Trách nhiệm |
| :--- | :--- | :---: | :--- |
| **Domain** | `src/Services/Identity/Identity.Domain/Accounts/Account.cs` | PR #17, #18, #19 | Aggregate Root tài khoản, quản lý email, vai trò (Role), trạng thái, mật khẩu, lockout 3 lần. |
| **Domain** | `src/Services/Identity/Identity.Domain/Accounts/AccountRole.cs` | **PR #20** | Enum định nghĩa 4 vai trò: `Customer`, `Staff`, `ParkingLotOwner`, `SystemAdmin`. |
| **Domain** | `src/Services/Identity/Identity.Domain/Profiles/Profile.cs` | **PR #19** | Thực thể hồ sơ người dùng (AccountId 1:1, FullName, DateOfBirth tùy chọn, audit dates). |
| **Domain** | `src/Services/Identity/Identity.Domain/EmailVerifications/EmailVerification.cs` | **PR #18** | Thực thể xác thực OTP (thời hạn 5 phút, max 3 attempts, cooldown 60s, max 5/giờ). |
| **Domain** | `src/Services/Identity/Identity.Domain/EmailVerifications/IOtpHasher.cs` | **PR #18** | Cổng trừu tượng băm và kiểm tra mã OTP. |
| **Domain** | `src/Services/Identity/Identity.Domain/Accounts/RefreshToken.cs` | **PR #17** | Thực thể Refresh Token (TokenHash SHA-256, thời hạn 7 ngày, thu hồi). |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/RegisterAccount/RegisterAccountCommand.cs` | **PR #19** | Command, FluentValidator và Handler cho luồng đăng ký tài khoản & hồ sơ tinh giản. |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/VerifyEmailOtp/VerifyEmailOtpCommand.cs` | **PR #18** | Command và Handler xác thực OTP với row-level lock `SELECT FOR UPDATE`. |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/ResendOtp/ResendOtpCommand.cs` | **PR #18** | Command và Handler gửi lại mã OTP, vô hiệu hóa mã cũ và kiểm tra quota. |
| **Application** | `src/Services/Identity/Identity.Application/Accounts/Login/LoginCommand.cs` | **PR #17, #20** | Command và Handler đăng nhập tài khoản, timing-safe dummy verify, lockout 3 lần, row-locking transaction. |
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
| **Unit Tests** | `tests/Identity/Identity.UnitTests/RegisterAccountTests.cs` | **PR #19** | Bộ 50 test unit kiểm thử toàn diện quy chuẩn đăng ký và hồ sơ (Registration & Profile Lean). |
| **Unit Tests** | `tests/Identity/Identity.UnitTests/EmailOtpTests.cs` | **PR #18** | Bộ test unit kiểm thử toàn diện các case OTP, cooldown, limit, hết hạn. |
| **Unit Tests** | `tests/Identity/Identity.UnitTests/LoginTests.cs` | **PR #17** | Bộ test unit kiểm thử đăng nhập, lockout, RS256 token. |
| **Unit Tests** | `tests/Notification/Notification.UnitTests/SmtpEmailSenderTests.cs` | **PR #18** | Bộ test unit kiểm thử cơ chế re-throw lỗi của SMTP sender. |
