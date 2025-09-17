using Dastone.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentalManagementSystem.Data;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dastone.Controllers
{
    public class AccountController : BaseController
    {
        private readonly RentalDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;
        private readonly HttpClient _httpClient;

        public AccountController(RentalDbContext context, IConfiguration configuration, ILogger<AccountController> logger, HttpClient httpClient) : base(context)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (HttpContext.Session.GetString("UserName") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? username, string? password, string? returnUrl = null)
        {
            _logger.LogInformation("Login attempt for username: {Username}", username);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Login failed: Username or password is empty.");
                return Json(new
                {
                    success = false,
                    message = "Kullanıcı adı ve şifre zorunludur.",
                    errors = new Dictionary<string, string[]>
                    {
                        { "General", new[] { "Kullanıcı adı ve şifre zorunludur." } }
                    }
                });
            }

            var user = await _context.Users
                .Where(u => u.Username == username && u.Password == password)
                .Select(u => new { u.Username, u.FirstName, u.LastName, u.RoleId, u.Email, u.phoneNumber, u.IsTwoFactorEnabled, u.ProfilePicturePath, u.ThemePreference })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Login failed: Invalid username or password for {Username}", username);
                return Json(new
                {
                    success = false,
                    message = "Geçersiz kullanıcı adı veya şifre.",
                    errors = new Dictionary<string, string[]>
                    {
                        { "General", new[] { "Geçersiz kullanıcı adı veya şifre." } }
                    }
                });
            }

            string roleName = "User";
            if (user.RoleId != 0)
            {
                var role = await _context.Roles
                    .Where(r => r.RoleID == user.RoleId)
                    .Select(r => r.RoleName)
                    .FirstOrDefaultAsync();
                roleName = role ?? "User";
            }

            if (user.IsTwoFactorEnabled)
            {
                if (string.IsNullOrEmpty(user.phoneNumber))
                {
                    _logger.LogWarning("Login failed: No phone number registered for {Username}", username);
                    return Json(new
                    {
                        success = false,
                        message = "İki aşamalı doğrulama için telefon numarası gerekli.",
                        errors = new Dictionary<string, string[]> { { "General", new[] { "Telefon numarası kayıtlı değil." } } }
                    });
                }

                string verificationCode = GenerateVerificationCode();
                HttpContext.Session.SetString("VerificationCode", verificationCode);
                HttpContext.Session.SetString("PendingUser", user.Username ?? string.Empty);
                HttpContext.Session.SetString("PendingFirstName", user.FirstName ?? string.Empty);
                HttpContext.Session.SetString("PendingRole", roleName ?? string.Empty);

                string safeReturnUrl = returnUrl ?? Url.Action("Index", "Home") ?? "/";
                HttpContext.Session.SetString("ReturnUrl", safeReturnUrl);

                _logger.LogInformation("Verification code generated for {Username}: {Code}", user.Username, verificationCode);

                try
                {
                    await SendVerificationSMS(user.phoneNumber, verificationCode);
                    _logger.LogInformation("Verification SMS sent to {PhoneNumber}", user.phoneNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send verification SMS to {PhoneNumber}", user.phoneNumber);
                    return Json(new
                    {
                        success = false,
                        message = $"SMS gönderimi başarısız: {ex.Message}",
                        errors = new Dictionary<string, string[]> { { "General", new[] { "SMS gönderimi başarısız." } } }
                    });
                }

                return Json(new
                {
                    success = true,
                    message = "Doğrulama kodu telefonunuza gönderildi.",
                    redirectUrl = Url.Action("VerifyCode") ?? "/"
                });
            }
            else
            {
                HttpContext.Session.SetString("UserName", user.Username ?? string.Empty);
                HttpContext.Session.SetString("FirstName", user.FirstName ?? string.Empty);
                HttpContext.Session.SetString("Role", roleName ?? string.Empty);
                HttpContext.Session.SetString("ProfilePicturePath", user.ProfilePicturePath ?? "~/images/users/default-user.jpg");

                string safeReturnUrl = returnUrl ?? Url.Action("Index", "Home") ?? "/";
                _logger.LogInformation("Login successful for {Username} (2FA disabled)", user.Username);
                return Json(new
                {
                    success = true,
                    message = "Giriş başarılı.",
                    redirectUrl = safeReturnUrl
                });
            }
        }

        // Profil Sayfası (GET)
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Profile accessed without valid session.");
                return RedirectToAction("Login");
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Username == username)
                .Select(u => new
                {
                    u.UserID,
                    u.Username,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.phoneNumber,
                    u.ProfilePicturePath,
                    u.IsTwoFactorEnabled,
                    ThemePreference = u.ThemePreference ?? "light",
                    RoleName = u.Role.RoleName
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Profile failed: User not found for {Username}", username);
                return RedirectToAction("Login");
            }

            ViewBag.User = new
            {
                user.UserID,
                user.Username,
                user.Email,
                user.FirstName,
                user.LastName,
                user.phoneNumber,
                ProfilePicturePath = user.ProfilePicturePath ?? "~/images/users/default-user.jpg",
                user.IsTwoFactorEnabled,
                ThemePreference = user.ThemePreference,
                user.RoleName
            };

            return View();
        }

        // Profil Güncelleme (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string username, string email, string? firstName, string? lastName, string? phoneNumber)
        {
            var currentUsername = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(currentUsername))
            {
                _logger.LogWarning("Profile update failed: No valid session.");
                return Json(new { success = false, message = "Oturum bulunamadı." });
            }

            var user = await _context.Users
                .Where(u => u.Username == currentUsername)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Profile update failed: User not found for {Username}", currentUsername);
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });
            }

            // Username ve Email kontrolü
            if (await _context.Users.AnyAsync(u => u.Username == username && u.UserID != user.UserID))
            {
                return Json(new { success = false, message = "Bu kullanıcı adı zaten kullanılıyor." });
            }

            if (await _context.Users.AnyAsync(u => u.Email == email && u.UserID != user.UserID))
            {
                return Json(new { success = false, message = "Bu e-posta adresi zaten kullanılıyor." });
            }

            // Telefon numarası format kontrolü
            if (!string.IsNullOrEmpty(phoneNumber) && !IsValidPhoneNumber(phoneNumber))
            {
                return Json(new { success = false, message = "Geçersiz telefon numarası formatı." });
            }

            user.Username = username;
            user.Email = email;
            user.FirstName = firstName;
            user.LastName = lastName;
            user.phoneNumber = phoneNumber;

            try
            {
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("UserName", username); // Oturumu güncelle
                HttpContext.Session.SetString("FirstName", firstName ?? string.Empty);
                _logger.LogInformation("Profile updated for user {Username}", username);
                return Json(new { success = true, message = "Profil başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update profile for {Username}", username);
                return Json(new { success = false, message = $"Güncelleme başarısız: {ex.Message}" });
            }
        }

        // Şifre Değiştirme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("ChangePassword failed: No valid session.");
                return Json(new { success = false, message = "Oturum bulunamadı." });
            }

            var user = await _context.Users
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("ChangePassword failed: User not found for {Username}", username);
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });
            }

            if (user.Password != currentPassword)
            {
                _logger.LogWarning("ChangePassword failed: Incorrect current password for {Username}", username);
                return Json(new { success = false, message = "Mevcut şifre yanlış." });
            }

            if (newPassword != confirmPassword)
            {
                _logger.LogWarning("ChangePassword failed: Passwords do not match for {Username}", username);
                return Json(new { success = false, message = "Yeni şifreler eşleşmiyor." });
            }

            user.Password = newPassword;
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Password changed for user {Username}", username);
                return Json(new { success = true, message = "Şifre başarıyla değiştirildi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change password for {Username}", username);
                return Json(new { success = false, message = $"Şifre değiştirme başarısız: {ex.Message}" });
            }
        }

        // 2FA Açma/Kapama
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTwoFactorAuth(bool enable)
        {
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("ToggleTwoFactorAuth failed: No valid session.");
                return Json(new { success = false, message = "Oturum bulunamadı." });
            }

            var user = await _context.Users
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("ToggleTwoFactorAuth failed: User not found for {Username}", username);
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });
            }

            if (enable && string.IsNullOrEmpty(user.phoneNumber))
            {
                _logger.LogWarning("ToggleTwoFactorAuth failed: No phone number registered for {Username}", username);
                return Json(new { success = false, message = "İki aşamalı doğrulama için telefon numarası gerekli." });
            }

            user.IsTwoFactorEnabled = enable;
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Two-factor authentication {Status} for user {Username}", enable ? "enabled" : "disabled", username);
                return Json(new { success = true, message = $"İki aşamalı doğrulama {(enable ? "aktifleştirildi" : "devre dışı bırakıldı")}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle 2FA for {Username}", username);
                return Json(new { success = false, message = $"İşlem başarısız: {ex.Message}" });
            }
        }

        // Profil Fotoğrafı Yükleme
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
        {
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("UploadProfilePicture failed: No valid session.");
                return Json(new { success = false, message = "Oturum bulunamadı." });
            }

            var user = await _context.Users
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("UploadProfilePicture failed: User not found for {Username}", username);
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });
            }

            if (profilePicture == null || profilePicture.Length == 0)
            {
                _logger.LogWarning("UploadProfilePicture failed: No file uploaded for {Username}", username);
                return Json(new { success = false, message = "Lütfen bir dosya seçin." });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("UploadProfilePicture failed: Invalid file type {Extension} for {Username}", extension, username);
                return Json(new { success = false, message = "Sadece JPG veya PNG dosyaları yüklenebilir." });
            }

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/users", fileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }

                user.ProfilePicturePath = $"/images/users/{fileName}";
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("ProfilePicturePath", user.ProfilePicturePath); // Oturumu güncelle
                _logger.LogInformation("Profile picture uploaded for {Username}: {FilePath}", username, user.ProfilePicturePath);
                return Json(new { success = true, message = "Profil fotoğrafı başarıyla güncellendi.", profilePicturePath = user.ProfilePicturePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload profile picture for {Username}", username);
                return Json(new { success = false, message = $"Fotoğraf yükleme başarısız: {ex.Message}" });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyCode()
        {
            if (HttpContext.Session.GetString("VerificationCode") == null)
            {
                _logger.LogWarning("VerifyCode accessed without a valid session.");
                return RedirectToAction("Login");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public IActionResult VerifyCode(string? code)
        {
            var storedCode = HttpContext.Session.GetString("VerificationCode");
            _logger.LogInformation("VerifyCode attempt with code: {Code}, stored code: {StoredCode}", code, storedCode);

            if (string.IsNullOrEmpty(storedCode) || !string.Equals(storedCode, code?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid verification code: {Code}, expected: {StoredCode}", code, storedCode);
                return Json(new
                {
                    success = false,
                    message = "Geçersiz doğrulama kodu.",
                    errors = new Dictionary<string, string[]> { { "code", new[] { "Geçersiz doğrulama kodu." } } }
                });
            }

            // Oturum bilgilerini ayarla
            var username = HttpContext.Session.GetString("PendingUser") ?? string.Empty;
            var firstName = HttpContext.Session.GetString("PendingFirstName") ?? string.Empty;
            var role = HttpContext.Session.GetString("PendingRole") ?? string.Empty;
            var returnUrl = HttpContext.Session.GetString("ReturnUrl") ?? Url.Action("Index", "Home") ?? "/";

            HttpContext.Session.SetString("UserName", username);
            HttpContext.Session.SetString("FirstName", firstName);
            HttpContext.Session.SetString("Role", role);

            // Doğrulama kodunu ve geçici oturum verilerini temizle
            HttpContext.Session.Remove("VerificationCode");
            HttpContext.Session.Remove("PendingUser");
            HttpContext.Session.Remove("PendingFirstName");
            HttpContext.Session.Remove("PendingRole");
            HttpContext.Session.Remove("ReturnUrl");

            _logger.LogInformation("Login successful for {Username}", username);
            return Json(new
            {
                success = true,
                message = "Giriş başarılı.",
                redirectUrl = returnUrl
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult RecoverPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> RecoverPassword(string? identifier)
        {
            _logger.LogInformation("RecoverPassword attempt with identifier: {Identifier}", identifier);

            if (string.IsNullOrEmpty(identifier))
            {
                _logger.LogWarning("RecoverPassword failed: Identifier is empty.");
                return Json(new
                {
                    success = false,
                    message = "Kullanıcı adı, e-posta veya telefon numarası zorunludur.",
                    errors = new Dictionary<string, string[]>
                    {
                        { "identifier", new[] { "Kullanıcı adı, e-posta veya telefon numarası zorunludur." } }
                    }
                });
            }

            var user = await _context.Users
                .Where(u => u.Username == identifier || u.Email == identifier || (u.phoneNumber != null && u.phoneNumber == identifier))
                .Select(u => new { u.UserID, u.Email })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("RecoverPassword failed: No user found for identifier {Identifier}", identifier);
                return Json(new
                {
                    success = false,
                    message = "Bu bilgilere sahip bir kullanıcı bulunamadı.",
                    errors = new Dictionary<string, string[]>
                    {
                        { "identifier", new[] { "Bu bilgilere sahip bir kullanıcı bulunamadı." } }
                    }
                });
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                _logger.LogWarning("RecoverPassword failed: No email found for user with identifier {Identifier}", identifier);
                return Json(new
                {
                    success = false,
                    message = "Bu kullanıcının e-posta adresi kayıtlı değil.",
                    errors = new Dictionary<string, string[]>
                    {
                        { "identifier", new[] { "Bu kullanıcının e-posta adresi kayıtlı değil." } }
                    }
                });
            }

            // Doğrulama token’ı oluştur
            string token = Guid.NewGuid().ToString();
            var resetToken = new PasswordResetToken
            {
                UserID = user.UserID,
                Token = token,
                ExpiryDate = DateTime.UtcNow.AddMinutes(15) // 15 dakika geçerlilik süresi
            };

            _context.PasswordResetTokens.Add(resetToken);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Password reset token generated for user ID {UserID}: {Token}", user.UserID, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save password reset token for user ID {UserID}", user.UserID);
                return Json(new
                {
                    success = false,
                    message = $"Token kaydedilemedi: {ex.Message}",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "Veritabanı hatası." } } }
                });
            }

            // E-posta ile doğrulama linki gönder
            try
            {
                string? resetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Scheme);
                if (string.IsNullOrEmpty(resetLink))
                {
                    _logger.LogError("Failed to generate reset link for token {Token}", token);
                    throw new InvalidOperationException("Şifre sıfırlama linki oluşturulamadı.");
                }

                await SendPasswordResetEmail(user.Email, resetLink);
                _logger.LogInformation("Password reset email sent to {Email} with link: {Link}", user.Email, resetLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                return Json(new
                {
                    success = false,
                    message = $"E-posta gönderimi başarısız: {ex.Message}",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "E-posta gönderimi başarısız." } } }
                });
            }

            return Json(new
            {
                success = true,
                message = "Şifre sıfırlama linki e-postanıza gönderildi.",
                redirectUrl = Url.Action("Login") ?? "/"
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string? token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("ResetPassword accessed with invalid token.");
                return RedirectToAction("Login");
            }

            var resetToken = await _context.PasswordResetTokens
                .Where(t => t.Token == token && t.ExpiryDate > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (resetToken == null)
            {
                _logger.LogWarning("Invalid or expired password reset token: {Token}", token);
                ViewBag.ErrorMessage = "Geçersiz veya süresi dolmuş şifre sıfırlama linki.";
                return View("Login");
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string? token, string? newPassword, string? confirmPassword)
        {
            _logger.LogInformation("ResetPassword attempt with token: {Token}", token);

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("ResetPassword failed: Token is empty.");
                return Json(new
                {
                    success = false,
                    message = "Geçersiz token.",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "Geçersiz token." } } }
                });
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword != confirmPassword)
            {
                _logger.LogWarning("ResetPassword failed: Passwords do not match or are empty.");
                return Json(new
                {
                    success = false,
                    message = "Şifreler eşleşmiyor veya boş.",
                    errors = new Dictionary<string, string[]>
                    {
                        { "newPassword", new[] { "Şifreler eşleşmiyor veya boş." } }
                    }
                });
            }

            var resetToken = await _context.PasswordResetTokens
                .Where(t => t.Token == token && t.ExpiryDate > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (resetToken == null)
            {
                _logger.LogWarning("ResetPassword failed: Invalid or expired token {Token}", token);
                return Json(new
                {
                    success = false,
                    message = "Geçersiz veya süresi dolmuş şifre sıfırlama linki.",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "Geçersiz veya süresi dolmuş link." } } }
                });
            }

            // Sadece gerekli sütunları çek
            var user = await _context.Users
                .Where(u => u.UserID == resetToken.UserID)
                .Select(u => new { u.UserID, u.Password })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("ResetPassword failed: User not found for ID {UserID}", resetToken.UserID);
                return Json(new
                {
                    success = false,
                    message = "Kullanıcı bulunamadı.",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "Kullanıcı bulunamadı." } } }
                });
            }

            // Şifreyi güncelle
            var userEntity = await _context.Users.FindAsync(user.UserID);
            if (userEntity == null)
            {
                _logger.LogWarning("ResetPassword failed: User entity not found for ID {UserID}", user.UserID);
                return Json(new
                {
                    success = false,
                    message = "Kullanıcı bulunamadı.",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "Kullanıcı bulunamadı." } } }
                });
            }

            userEntity.Password = newPassword!;
            _context.PasswordResetTokens.Remove(resetToken); // Token’ı sil
            await _context.SaveChangesAsync();
            _logger.LogInformation("Password reset successful for user ID {UserID}", user.UserID);

            return Json(new
            {
                success = true,
                message = "Şifre başarıyla sıfırlandı.",
                redirectUrl = Url.Action("Login") ?? "/"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public IActionResult Logout()
        {
            _logger.LogInformation("User logged out.");
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private string GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private async Task SendVerificationSMS(string phoneNumber, string code)
        {
            var netGsmSettings = _configuration.GetSection("NetGsmSettings");
            var username = netGsmSettings["Username"] ?? throw new InvalidOperationException("NetGSM username ayarı eksik.");
            var password = netGsmSettings["Password"] ?? throw new InvalidOperationException("NetGSM password ayarı eksik.");
            var header = netGsmSettings["Header"] ?? throw new InvalidOperationException("NetGSM header ayarı eksik.");
            var apiUrl = netGsmSettings["ApiUrl"] ?? throw new InvalidOperationException("NetGSM API URL ayarı eksik.");

            // Telefon numarasını formatla
            var formattedPhoneNumber = FormatPhoneNumber(phoneNumber);
            _logger.LogInformation("Formatted phone number: {FormattedPhoneNumber}", formattedPhoneNumber);

            // XML isteği oluştur (1:n formatı için tek bir numara ile)
            var xml = new XDocument(
                new XElement("mainbody",
                    new XElement("header",
                        new XElement("company", "Netgsm", new XAttribute("dil", "TR")),
                        new XElement("usercode", username),
                        new XElement("password", password),
                        new XElement("type", "1:n"),
                        new XElement("msgheader", header)
                    ),
                    new XElement("body",
                        new XElement("msg",
                            new XCData($"Doğrulama kodunuz: {code}")
                        ),
                        new XElement("no", formattedPhoneNumber)
                    )
                )
            );

            var content = new StringContent(xml.ToString(), Encoding.UTF8, "application/xml");
            _logger.LogInformation("Sending SMS request to {ApiUrl} with XML: {Xml}", apiUrl, xml.ToString());

            try
            {
                var response = await _httpClient.PostAsync(apiUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("SMS response from {ApiUrl}: {ResponseContent}", apiUrl, responseContent);

                if (!response.IsSuccessStatusCode || responseContent.StartsWith("40") || responseContent.StartsWith("70"))
                {
                    string errorMessage = responseContent switch
                    {
                        "40" => "Kullanıcı adı veya şifre hatalı. Lütfen NetGSM ayarlarını kontrol edin.",
                        "70" => "Geçersiz telefon numarası veya eksik parametreler. Telefon numarası: " + formattedPhoneNumber,
                        _ => $"SMS gönderimi başarısız. NetGSM yanıtı: {responseContent}"
                    };
                    throw new HttpRequestException(errorMessage);
                }

                _logger.LogInformation("SMS sent successfully to {PhoneNumber}. Response: {ResponseContent}", phoneNumber, responseContent);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while sending SMS to {PhoneNumber}", phoneNumber);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending SMS to {PhoneNumber}", phoneNumber);
                throw new Exception($"SMS gönderimi sırasında beklenmeyen bir hata oluştu: {ex.Message}", ex);
            }
        }

        private string FormatPhoneNumber(string phoneNumber)
        {
            // Telefon numarasını temizle ve NetGSM formatına dönüştür (905365813028)
            phoneNumber = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
            if (phoneNumber.Length == 10 && phoneNumber.StartsWith("0"))
                phoneNumber = "9" + phoneNumber; // 05365813028 -> 905365813028
            else if (phoneNumber.Length == 11 && !phoneNumber.StartsWith("90"))
                phoneNumber = "90" + phoneNumber.Substring(1); // 5365813028 -> 905365813028
            return phoneNumber;
        }

        private bool IsValidPhoneNumber(string phoneNumber)
        {
            // Telefon numarasını temizle ve formatla
            var formattedPhoneNumber = FormatPhoneNumber(phoneNumber);
            return System.Text.RegularExpressions.Regex.IsMatch(formattedPhoneNumber, @"^90[0-9]{9}$") && formattedPhoneNumber.Length == 11;
        }

        private async Task SendPasswordResetEmail(string email, string resetLink)
        {
            if (string.IsNullOrEmpty(resetLink))
                throw new ArgumentNullException(nameof(resetLink));

            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var server = smtpSettings["Server"] ?? throw new InvalidOperationException("SMTP server ayarı eksik.");
            var portStr = smtpSettings["Port"];
            int port = 25;
            if (!string.IsNullOrEmpty(portStr) && !int.TryParse(portStr, out port))
            {
                port = 25;
            }
            var username = smtpSettings["Username"] ?? string.Empty;
            var password = smtpSettings["Password"] ?? string.Empty;
            var senderEmail = smtpSettings["SenderEmail"] ?? throw new InvalidOperationException("SenderEmail ayarı eksik.");
            var senderName = smtpSettings["SenderName"] ?? string.Empty;

            _logger.LogInformation("Sending password reset email to {Email} with SMTP settings: Server={Server}, Port={Port}, Username={Username}", email, server, port, username);
            var smtpClient = new System.Net.Mail.SmtpClient(server)
            {
                Port = port,
                Credentials = new System.Net.NetworkCredential(username, password),
                EnableSsl = true,
            };

            var mailMessage = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(senderEmail, senderName),
                Subject = "RMS Şifre Sıfırlama",
                Body = $"Şifrenizi sıfırlamak için aşağıdaki linke tıklayın:\n{resetLink}\n\nBu link 15 dakika boyunca geçerlidir.",
                IsBodyHtml = false,
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerificationCode()
        {
            _logger.LogInformation("ResendVerificationCode attempt");

            // Oturumda PendingUser var mı kontrol et
            var username = HttpContext.Session.GetString("PendingUser");
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("ResendVerificationCode failed: No pending user in session.");
                return Json(new
                {
                    success = false,
                    message = "Geçersiz oturum. Lütfen tekrar giriş yapmayı deneyin.",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "Geçersiz oturum." } } }
                });
            }

            // Kullanıcıyı bul
            var user = await _context.Users
                .Where(u => u.Username == username)
                .Select(u => new { u.phoneNumber })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("ResendVerificationCode failed: No user found for username {Username}", username);
                return Json(new
                {
                    success = false,
                    message = "Kullanıcı bulunamadı.",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "Kullanıcı bulunamadı." } } }
                });
            }

            if (string.IsNullOrEmpty(user.phoneNumber))
            {
                _logger.LogWarning("ResendVerificationCode failed: No phone number found for user {Username}", username);
                return Json(new
                {
                    success = false,
                    message = "Bu kullanıcının telefon numarası kayıtlı değil.",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "Telefon numarası kayıtlı değil." } } }
                });
            }

            // Yeni doğrulama kodu oluştur
            string verificationCode = GenerateVerificationCode();
            HttpContext.Session.SetString("VerificationCode", verificationCode);
            _logger.LogInformation("New verification code generated for {Username}: {Code}", username, verificationCode);

            // SMS ile doğrulama kodu gönder
            try
            {
                await SendVerificationSMS(user.phoneNumber, verificationCode);
                _logger.LogInformation("Verification SMS sent to {PhoneNumber}", user.phoneNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification SMS to {PhoneNumber}", user.phoneNumber);
                return Json(new
                {
                    success = false,
                    message = $"SMS gönderimi başarısız: {ex.Message}",
                    errors = new Dictionary<string, string[]> { { "General", new[] { "SMS gönderimi başarısız." } } }
                });
            }

            return Json(new
            {
                success = true,
                message = "Yeni doğrulama kodu telefonunuza gönderildi.",
                redirectUrl = Url.Action("VerifyCode") ?? "/"
            });
        }

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Settings accessed without valid session.");
                return RedirectToAction("Login");
            }

            var user = await _context.Users
                .Where(u => u.Username == username)
                .Select(u => new
                {
                    u.UserID,
                    u.ReceiveEmailNotifications,
                    u.ReceiveSMSNotifications,
                    u.PreferredLanguage,
                    u.ThemePreference,
                    u.SessionTimeoutMinutes
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Settings failed: User not found for {Username}", username);
                return RedirectToAction("Login");
            }

            ViewBag.Settings = new
            {
                user.UserID,
                user.ReceiveEmailNotifications,
                user.ReceiveSMSNotifications,
                user.PreferredLanguage,
                user.ThemePreference,
                user.SessionTimeoutMinutes
            };

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(bool receiveEmailNotifications, bool receiveSMSNotifications, string preferredLanguage, string themePreference, int sessionTimeoutMinutes)
        {
            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Settings update failed: No valid session.");
                return Json(new { success = false, message = "Oturum bulunamadı." });
            }

            var user = await _context.Users
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning("Settings update failed: User not found for {Username}", username);
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });
            }

            // Geçerli seçenekleri kontrol et
            var validLanguages = new[] { "tr", "en" };
            var validThemes = new[] { "light", "dark" };
            var validTimeouts = new[] { 15, 30, 60, 120 };

            if (!validLanguages.Contains(preferredLanguage))
            {
                return Json(new { success = false, message = "Geçersiz dil seçimi." });
            }

            if (!validThemes.Contains(themePreference))
            {
                return Json(new { success = false, message = "Geçersiz tema seçimi." });
            }

            if (!validTimeouts.Contains(sessionTimeoutMinutes))
            {
                return Json(new { success = false, message = "Geçersiz oturum süresi." });
            }

            user.ReceiveEmailNotifications = receiveEmailNotifications;
            user.ReceiveSMSNotifications = receiveSMSNotifications;
            user.PreferredLanguage = preferredLanguage;
            user.ThemePreference = themePreference;
            user.SessionTimeoutMinutes = sessionTimeoutMinutes;

            try
            {
                await _context.SaveChangesAsync();
                // Oturumu güncelle
                HttpContext.Session.SetString("PreferredLanguage", preferredLanguage);
                HttpContext.Session.SetString("ThemePreference", themePreference);
                HttpContext.Session.SetInt32("SessionTimeoutMinutes", sessionTimeoutMinutes);
                _logger.LogInformation("Settings updated for user {Username}", username);
                return Json(new { success = true, message = "Ayarlar başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update settings for {Username}", username);
                return Json(new { success = false, message = $"Güncelleme başarısız: {ex.Message}" });
            }
        }
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> TestSMS(string phoneNumber)
        {
            try
            {
                await SendVerificationSMS(phoneNumber, "123456");
                return Ok("SMS gönderildi.");
            }
            catch (Exception ex)
            {
                return BadRequest($"SMS gönderimi başarısız: {ex.Message}");
            }
        }
    }
}