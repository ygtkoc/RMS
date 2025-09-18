using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalManagementSystem.Data;
using Dastone.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography; // Şifre hashleme için
using System.Text;

namespace Dastone.Controllers
{
    public class ParametersController : BaseController
    {
        private readonly RentalDbContext _context; // DbContext'inizin adı

        public ParametersController(RentalDbContext context) : base(context)
        {
            _context = context;
        }

        // GET: Parameters/Index - Ana Parametreler Sayfası
        public async Task<IActionResult> Index(string activeTab = "penalty-definitions", int pageNumber = 1, int pageSize = 40, string searchQuery = "")
        {
            var totalPenaltyDefinitionsCount = await _context.CezaTanimlari.CountAsync();
            var totalUsersCount = await _context.Users.CountAsync();
            var totalCarTypeCount = await _context.AracTipiTanimi.CountAsync();
            var totalLocationsCount = await _context.Lokasyonlar.CountAsync();

            var model = new ParametersDashboardViewModel
            {
                ActiveTab = activeTab,
                TotalPenaltyDefinitionsCount = totalPenaltyDefinitionsCount,
                TotalUsersCount = totalUsersCount,
                TotalCarTypeCount = totalCarTypeCount,
                TotalLocationsCount = totalLocationsCount,
                PenaltyDefinitionsPartial = await GetPenaltyDefinitionListPartialViewModel(pageNumber, pageSize, searchQuery),
                UsersPartial = await GetUserListPartialViewModel(pageNumber, pageSize, searchQuery),
                CarTypePartial = await GetCarTypePartialViewModel(pageNumber, pageSize, searchQuery),
                LocationsPartial = await GetLocationPartialViewModel(pageNumber, pageSize, searchQuery)
            };

            return View(model);
        }

        // AJAX: Ceza Tanımı listesi için partial view döndürür
        [HttpGet]
        public async Task<IActionResult> GetPenaltyDefinitionsForTab(int pageNumber = 1, int pageSize = 10, string searchQuery = "")
        {
            var penaltyDefinitionListViewModel = await GetPenaltyDefinitionListPartialViewModel(pageNumber, pageSize, searchQuery);
            return PartialView("_PenaltyDefinitionListPartial", penaltyDefinitionListViewModel);
        }

        // Yardımcı Metot: Ceza Tanımı listesi için ViewModel oluşturur
        private async Task<PenaltyDefinitionListPartialViewModel> GetPenaltyDefinitionListPartialViewModel(int pageNumber, int pageSize, string searchQuery)
        {
            IQueryable<CezaTanimi> query = _context.CezaTanimlari;

            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(pd =>
                    pd.CezaKodu.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    pd.KisaAciklama.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (pd.UzunAciklama != null && pd.UzunAciklama.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                );
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var penaltyDefinitions = await query
                .OrderBy(pd => pd.CezaKodu)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PenaltyDefinitionListPartialViewModel
            {
                PenaltyDefinitions = penaltyDefinitions,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                SearchQuery = searchQuery
            };
        }

        private async Task<LocationPartialViewModel> GetLocationPartialViewModel(int pageNumber, int pageSize, string searchQuery)
        {
            if (pageNumber < 1)
            {
                pageNumber = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 10;
            }

            IQueryable<Lokasyon> query = _context.Lokasyonlar.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(l =>
                    l.LokasyonAdi.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (l.Aciklama != null && l.Aciklama.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            if (pageNumber > totalPages)
            {
                pageNumber = totalPages;
            }

            var locations = await query
                .OrderBy(l => l.LokasyonAdi)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new LocationPartialViewModel
            {
                Locations = locations,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                SearchQuery = searchQuery
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetLocationsForTab(int pageNumber = 1, int pageSize = 10, string searchQuery = "")
        {
            var locationPartial = await GetLocationPartialViewModel(pageNumber, pageSize, searchQuery);
            return PartialView("_LocationListPartial", locationPartial);
        }

        [HttpGet]
        public IActionResult CreateLocationFormPartial()
        {
            return PartialView("_CreateLocationFormPartial", new Lokasyon());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLocation([Bind("LokasyonAdi,Aciklama")] Lokasyon lokasyon)
        {
            if (!ModelState.IsValid)
            {
                var modelErrors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());

                return Json(new { success = false, message = "Formda hatalar var.", errors = modelErrors });
            }

            if (await _context.Lokasyonlar.AnyAsync(l => l.LokasyonAdi == lokasyon.LokasyonAdi))
            {
                ModelState.AddModelError(nameof(Lokasyon.LokasyonAdi), "Bu isimde bir lokasyon zaten mevcut.");

                var duplicateErrors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());

                return Json(new { success = false, message = "Formda hatalar var.", errors = duplicateErrors });
            }

            _context.Lokasyonlar.Add(lokasyon);
            await _context.SaveChangesAsync();

            var totalLocations = await _context.Lokasyonlar.CountAsync();

            return Json(new
            {
                success = true,
                message = $"{lokasyon.LokasyonAdi} oluşturuldu.",
                totalCount = totalLocations
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            var location = await _context.Lokasyonlar.FindAsync(id);
            if (location == null)
            {
                return Json(new { success = false, message = "Lokasyon bulunamadı." });
            }

            var hasVehicles = await _context.Araclar.AnyAsync(v => v.LokasyonID == id);
            if (hasVehicles)
            {
                return Json(new { success = false, message = "Bu lokasyona bağlı araçlar olduğu için silinemiyor." });
            }

            var hasRentals = await _context.Kiralamalar.AnyAsync(r => r.LokasyonID == id);
            if (hasRentals)
            {
                return Json(new { success = false, message = "Bu lokasyon kiralama kayıtlarında kullanıldığı için silinemiyor." });
            }

            _context.Lokasyonlar.Remove(location);
            await _context.SaveChangesAsync();

            var totalLocations = await _context.Lokasyonlar.CountAsync();

            return Json(new
            {
                success = true,
                message = $"{location.LokasyonAdi} silindi.",
                totalCount = totalLocations
            });
        }

        // GET: Parameters/CreatePenaltyDefinitionFormPartial
        [HttpGet]
        public IActionResult CreatePenaltyDefinitionFormPartial()
        {
            return PartialView("_CreatePenaltyDefinitionFormPartial", new CezaTanimi());
        }

        // POST: Parameters/CreatePenaltyDefinition
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePenaltyDefinition([Bind("CezaKodu,KisaAciklama,UzunAciklama")] CezaTanimi cezaTanimi)
        {
            if (ModelState.IsValid)
            {
                // Ceza Kodu benzersiz mi kontrol et
                if (await _context.CezaTanimlari.AnyAsync(ct => ct.CezaKodu == cezaTanimi.CezaKodu))
                {
                    ModelState.AddModelError("CezaKodu", "Bu ceza kodu zaten mevcut.");
                }

                if (!ModelState.IsValid) // Model durumunu tekrar kontrol et
                {
                    var errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                    return Json(new { success = false, message = "Formda hatalar var.", errors });
                }

                _context.Add(cezaTanimi);
                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    message = "Ceza tanımı başarıyla eklendi."
                });
            }

            var modelErrors = ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
            );
            return Json(new { success = false, message = "Formda hatalar var.", errors = modelErrors });
        }

        // GET: Parameters/GetPenaltyDefinitionDetailsPartial/5 (Genel Bilgiler Partial)
        [HttpGet]
        public async Task<IActionResult> GetPenaltyDefinitionDetailsPartial(int id)
        {
            var cezaTanimi = await _context.CezaTanimlari.FindAsync(id);
            if (cezaTanimi == null)
            {
                return NotFound();
            }
            return PartialView("_PenaltyDefinitionDetailsPartial", cezaTanimi);
        }

        // GET: Parameters/EditPenaltyDefinitionFormPartial/5 (Düzenle Form Partial)
        [HttpGet]
        public async Task<IActionResult> EditPenaltyDefinitionFormPartial(int id)
        {
            var cezaTanimi = await _context.CezaTanimlari.FindAsync(id);
            if (cezaTanimi == null)
            {
                return NotFound();
            }
            return PartialView("_EditPenaltyDefinitionFormPartial", cezaTanimi);
        }

        // GET: Parameters/DeletePenaltyDefinitionConfirmPartial/5 (Sil Onay Partial)
        [HttpGet]
        public async Task<IActionResult> DeletePenaltyDefinitionConfirmPartial(int id)
        {
            var cezaTanimi = await _context.CezaTanimlari.FindAsync(id);
            if (cezaTanimi == null)
            {
                return NotFound();
            }
            return PartialView("_DeletePenaltyDefinitionConfirmPartial", cezaTanimi);
        }

        // POST: Parameters/UpdatePenaltyDefinition
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePenaltyDefinition([Bind("CezaTanimiID,CezaKodu,KisaAciklama,UzunAciklama")] CezaTanimi cezaTanimi)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Formda hatalar var.", errors });
            }

            try
            {
                if (await _context.CezaTanimlari.AnyAsync(ct => ct.CezaKodu == cezaTanimi.CezaKodu && ct.CezaTanimiID != cezaTanimi.CezaTanimiID))
                {
                    ModelState.AddModelError("CezaKodu", "Bu ceza kodu zaten mevcut.");
                    var errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                    return Json(new { success = false, message = "Formda hatalar var.", errors });
                }

                _context.Update(cezaTanimi);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Ceza tanımı başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Update Penalty Definition: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Güncelleme hatası: {ex.Message}" });
            }
        }

        // GET: Parameters/DeletePenaltyDefinition/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePenaltyDefinition(int id)
        {
            var cezaTanimi = await _context.CezaTanimlari.FindAsync(id);
            if (cezaTanimi == null)
            {
                return Json(new { success = false, message = "Ceza tanımı bulunamadı." });
            }

            try
            {
                _context.CezaTanimlari.Remove(cezaTanimi);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Ceza tanımı başarıyla silindi." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Delete Penalty Definition: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Silme hatası: {ex.Message}" });
            }
        }

        // --- Kullanıcı (Danışman) Metotları ---

        // AJAX: Kullanıcı listesi için partial view döndürür
        [HttpGet]
        public async Task<IActionResult> GetUsersForTab(int pageNumber = 1, int pageSize = 10, string searchQuery = "")
        {
            var userListViewModel = await GetUserListPartialViewModel(pageNumber, pageSize, searchQuery);
            return PartialView("_UserListPartial", userListViewModel);
        }

        // GET: Parameters/GetUserDetailsPartial/5
        [HttpGet]
        public async Task<IActionResult> GetUserDetailsPartial(int id)
        {
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null)
            {
                return NotFound();
            }
            return PartialView("_UserDetailsPartial", user);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserDetailsModalPartial(int id)
        {
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null)
            {
                return NotFound();
            }
            ViewBag.Roles = await _context.Roles.ToListAsync();
            return PartialView("_UserDetailsModalPartial", user);
        }


        // Yardımcı Metot: Kullanıcı listesi için ViewModel oluşturur
        private async Task<UserListPartialViewModel> GetUserListPartialViewModel(int pageNumber, int pageSize, string searchQuery)
        {
            IQueryable<User> query = _context.Users.Include(u => u.Role);

            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(u =>
                    u.Username.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (u.FirstName != null && u.FirstName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (u.LastName != null && u.LastName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (u.Role != null && u.Role.RoleName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                );
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var users = await query
                .OrderBy(u => u.Username)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var roles = await _context.Roles.ToListAsync(); // Roller de çekilmeli

            return new UserListPartialViewModel
            {
                Users = users,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                SearchQuery = searchQuery,
                AvailableRoles = roles
            };
        }

        // GET: Parameters/CreateUserFormPartial
        [HttpGet]
        public async Task<IActionResult> CreateUserFormPartial()
        {
            ViewBag.Roles = await _context.Roles.ToListAsync(); // Rolleri view'a gönder
            return PartialView("_CreateUserFormPartial", new User { CreatedAt = DateTime.Now });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser([Bind("Username,Password,Email,PhoneNumber,FirstName,LastName,RoleId,IsTwoFactorEnabled")] User user)
        {
            // RoleID'nin sıfır veya geçersiz bir ID olup olmadığını kontrol edin.
            if (user.RoleId <= 0)
            {
                ModelState.AddModelError("RoleId", "Rol alanı zorunludur.");
            }

            // Şifre alanı boş bırakılamaz kontrolü
            if (string.IsNullOrEmpty(user.Password))
            {
                ModelState.AddModelError("Password", "Şifre alanı boş bırakılamaz.");
            }

            // İlk ModelState kontrolü
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Formda hatalar var.", errors });
            }

            // Kullanıcı adı veya e-posta benzersiz mi kontrol et
            if (await _context.Users.AnyAsync(u => u.Username == user.Username))
            {
                ModelState.AddModelError("Username", "Bu kullanıcı adı zaten mevcut.");
            }
            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
            {
                ModelState.AddModelError("Email", "Bu e-posta adresi zaten mevcut.");
            }

            // Tüm özel validasyonlar sonrası ModelState kontrolü
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Formda hatalar var.", errors });
            }

            // En büyük UserID'yi bul ve +1 ekle
            var maxUserId = await _context.Users.MaxAsync(u => (int?)u.UserID) ?? 0;
            user.UserID = maxUserId + 1;

            // Diğer kullanıcı özelliklerini ayarla
            user.CreatedAt = DateTime.Now;
            user.IsTwoFactorEnabled = true;
            user.SessionTimeoutMinutes = 30; // Varsayılan oturum süresi
            user.ReceiveEmailNotifications = false;
            user.PreferredLanguage = "tr";
            user.ReceiveSMSNotifications = false;
            user.ThemePreference = "light";

            try
            {
                _context.Add(user);
                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    message = "Kullanıcı başarıyla eklendi.",
                    redirectUrl = Url.Action("Index", "Parameters", new { activeTab = "user-definitions", success = true })
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kullanıcı oluşturulurken bir hata oluştu: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return Json(new
                {
                    success = false,
                    message = "Kullanıcı oluşturulurken beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.",
                    errors = new Dictionary<string, string[]> { { "Genel", new[] { "Bir hata oluştu: " + ex.Message } } }
                });
            }
        }

        // Örnek HashPassword metodu (gerçek uygulamada daha güvenli olmalı)
        private string HashPassword(string password)
        {
            // Gerçek projede BCrypt.Net veya ASP.NET Core Identity'nin PasswordHasher'ını kullanın.
            // Bu sadece bir örnek:
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        // GET: Parameters/EditUserFormPartial/5
        [HttpGet]
        public async Task<IActionResult> EditUserFormPartial(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            ViewBag.Roles = await _context.Roles.ToListAsync();
            return PartialView("_EditUserFormPartial", user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser([Bind("UserID,Username,Password,Email,FirstName,LastName,RoleID,CreatedAt")] User user)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Formda hatalar var.", errors });
            }

            try
            {
                if (await _context.Users.AnyAsync(u => u.Username == user.Username && u.UserID != user.UserID))
                {
                    ModelState.AddModelError("Username", "Bu kullanıcı adı zaten mevcut.");
                    var errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                    return Json(new { success = false, message = "Formda hatalar var.", errors });
                }

                _context.Update(user);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Kullanıcı başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Update User: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Güncelleme hatası: {ex.Message}" });
            }
        }

        // GET: Parameters/DeleteUserConfirmPartial/5
        [HttpGet]
        public async Task<IActionResult> DeleteUserConfirmPartial(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return PartialView("_DeleteUserConfirmPartial", user);
        }

        // POST: Parameters/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "Kullanıcı bulunamadı." });
            }

            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Kullanıcı başarıyla silindi." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Delete User: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Silme hatası: {ex.Message}" });
            }
        }

        // --- Araç Tipi Tanımları Metotları ---

        // Yardımcı Metot: Araç Tipi listesi için ViewModel oluşturur
        private async Task<CarTypePartialViewModel> GetCarTypePartialViewModel(int pageNumber, int pageSize, string searchQuery)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10; // Minimum bir değer atayalım

            IQueryable<AracTipiTanimi> query = _context.AracTipiTanimi.AsNoTracking(); // Performans için AsNoTracking

            if (!string.IsNullOrEmpty(searchQuery))
            {
                query = query.Where(at => at.AracTipiName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                          (at.AracTipiAciklama != null && at.AracTipiAciklama.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var carTypes = await query
                .OrderBy(at => at.AracTipiID) // Sıralama eklendi
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new CarTypePartialViewModel
            {
                AracTipiListesi = carTypes,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                SearchQuery = searchQuery
            };
        }

        // AJAX: Araç Tipi listesi için partial view döndürür
        [HttpGet]
        public async Task<IActionResult> GetCarTypesForTab(int pageNumber = 1, int pageSize = 10, string searchQuery = "")
        {
            try
            {
                var carTypeViewModel = await GetCarTypePartialViewModel(pageNumber, pageSize, searchQuery);
                return PartialView("_CarTypeListPartial", carTypeViewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCarTypesForTab: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { success = false, message = "Veriler yüklenirken bir hata oluştu." });
            }
        }

        // GET: Parameters/CreateCarTypeFormPartial
        [HttpGet]
        public IActionResult CreateCarTypeFormPartial()
        {
            try
            {
                return PartialView("_CreateCarTypeFormPartial", new AracTipiTanimi());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateCarTypeFormPartial: ${ex.Message}\nStackTrace: ${ex.StackTrace}");
                return StatusCode(500, new { success = false, message = $"Form yükleme hatası: ${ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCarType([Bind("AracTipiName,AracTipiAciklama")] AracTipiTanimi aracTipi)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Formda hatalar var.", errors });
            }

            try
            {
                // Benzersizlik kontrolü: Önce listeyi client-side getir
                var existingTypes = await _context.AracTipiTanimi
                    .Select(at => at.AracTipiName.ToLower())  // ToLower'ı server-side yap, desteklenir
                    .ToListAsync();

                if (existingTypes.Any(name => name == aracTipi.AracTipiName.ToLower()))
                {
                    ModelState.AddModelError("AracTipiName", "Bu araç tipi adı zaten mevcut.");
                    var errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                    return Json(new { success = false, message = "Formda hatalar var.", errors });
                }

                _context.Add(aracTipi);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Araç tipi başarıyla oluşturuldu.", redirectUrl = Url.Action("Index", "Parameters", new { activeTab = "car-type-definitions", success = true }) });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateCarType: {ex.Message}\nStackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return Json(new { success = false, message = $"Oluşturma hatası: {ex.Message}", innerException = ex.InnerException?.Message });
            }
        }

        // Araç tipi düzenleme Action
        [HttpPut]
        public async Task<IActionResult> UpdateCarType([FromBody] AracTipiTanimi aracTipi)
        {
            try
            {
                if (aracTipi == null || string.IsNullOrWhiteSpace(aracTipi.AracTipiName) || aracTipi.AracTipiID <= 0)
                {
                    return Json(new { success = false, message = "Geçersiz veri. Araç Tipi Adı veya ID eksik.", errors = new[] { "AracTipiName", "AracTipiID" } });
                }

                var carTypeToUpdate = await _context.AracTipiTanimi.FindAsync(aracTipi.AracTipiID);
                if (carTypeToUpdate == null)
                {
                    return Json(new { success = false, message = "Araç tipi bulunamadı." });
                }

                // Benzersizlik kontrolü (kendi hariç)
                if (await _context.AracTipiTanimi.AnyAsync(at => at.AracTipiName.ToLower() == aracTipi.AracTipiName.ToLower() && at.AracTipiID != aracTipi.AracTipiID))
                {
                    return Json(new { success = false, message = "Bu araç tipi adı zaten mevcut.", errors = new[] { "AracTipiName" } });
                }

                carTypeToUpdate.AracTipiName = aracTipi.AracTipiName;
                carTypeToUpdate.AracTipiAciklama = aracTipi.AracTipiAciklama;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Araç tipi başarıyla güncellendi.",
                    redirectUrl = Url.Action("Index", "Parameters", new { activeTab = "car-type-definitions", success = true })
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "Eşzamanlılık hatası. Lütfen tekrar deneyin." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateCarType: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Araç tipi güncellenirken bir hata oluştu: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCarType(int id)
        {
            var aracTipi = await _context.AracTipiTanimi
                .Include(at => at.Araclar)  // İlişkili araçları yükle
                .FirstOrDefaultAsync(at => at.AracTipiID == id);

            if (aracTipi == null)
            {
                return Json(new { success = false, message = "Araç tipi bulunamadı." });
            }

            if (aracTipi.Araclar != null && aracTipi.Araclar.Any())
            {
                // İlişkili araçlar varsa, modal için veri dön
                var relatedVehicles = aracTipi.Araclar.Select(v => new
                {
                    VehicleId = v.AracID,  // Araç ID'si (modeline göre değiştir)
                    VehicleName = v.Marka + " " + v.Model + " - " + v.Plaka ?? "Araç " + v.AracID,  // Araç adı (modeline göre)
                    CurrentTypeId = v.AracTipiID
                }).ToList();

                return Json(new
                {
                    success = false,
                    hasRelated = true,
                    message = "İlişkili araçlar var. Lütfen modalda yönetin.",
                    relatedVehicles
                });
            }

            try
            {
                _context.AracTipiTanimi.Remove(aracTipi);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Araç tipi başarıyla silindi.", redirectUrl = Url.Action("Index", "Parameters", new { activeTab = "car-type-definitions", success = true })});
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Silme hatası: {ex.Message}" });
            }
        }

        // Yeni: Modal partial view için ilişkili araçları dön (ama JS'te doğrudan JSON'dan modal oluşturacağız, partial opsiyonel)
        [HttpGet]
        public async Task<IActionResult> GetRelatedVehiclesPartial(int aracTipiId)
        {
            var aracTipi = await _context.AracTipiTanimi
                .Include(at => at.Araclar)
                .FirstOrDefaultAsync(at => at.AracTipiID == aracTipiId);

            if (aracTipi == null || aracTipi.Araclar == null || !aracTipi.Araclar.Any())
            {
                return NotFound();
            }

            var model = new RelatedVehiclesViewModel
            {
                AracTipiId = aracTipiId,
                RelatedVehicles = aracTipi.Araclar.ToList(),
                AllVehicleTypes = await _context.AracTipiTanimi.Where(at => at.AracTipiID != aracTipiId).ToListAsync()  // Diğer tipler dropdown için
            };

            return PartialView("_RelatedVehiclesModalPartial", model);
        }

        // Yeni: Araç tipini güncelle (değiştir veya sıfırla)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVehicleType(int vehicleId, int? newTypeId)
        {
            var vehicle = await _context.Araclar.FindAsync(vehicleId);  // Vehicle DbSet'ine göre değiştir
            if (vehicle == null)
            {
                return Json(new { success = false, message = "Araç bulunamadı." });
            }

            vehicle.AracTipiID = newTypeId;  // Null olabilir (sıfırla için)
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = newTypeId.HasValue ? "Araç tipi değiştirildi." : "Araç tipi sıfırlandı." });
        }

        [HttpGet]
        public async Task<IActionResult> GetPenaltyDefinitions(string query)
        {
            Console.WriteLine($"GetPenaltyDefinitions aksiyonuna gelen sorgu: {query}");

            IQueryable<CezaTanimi> penaltyDefinitions = _context.CezaTanimlari; // DbContext'teki Ceza Tanımları DbSet'inizin adı

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.ToLower();

                penaltyDefinitions = penaltyDefinitions.Where(pd =>
                    pd.CezaKodu.ToLower().Contains(query) ||
                    pd.KisaAciklama.ToLower().Contains(query) ||
                    (pd.UzunAciklama != null && pd.UzunAciklama.ToLower().Contains(query)) // Uzun açıklama nullable olabilir
                );
            }

            var result = await penaltyDefinitions
                .Select(pd => new
                {
                    id = pd.CezaTanimiID, // Modelinizdeki ID property'si neyse (örn: Id, CezaTanimiID)
                    cezaKodu = pd.CezaKodu,
                    kisaAciklama = pd.KisaAciklama,
                    uzunAciklama = pd.UzunAciklama,
                    // Frontend'de gösterilecek metin
                    text = $"{pd.CezaKodu} - {pd.KisaAciklama}"
                })
                .ToListAsync();

            Console.WriteLine($"GetPenaltyDefinitions aksiyonundan dönen ceza tanımı sayısı: {result.Count}");
            return Json(result);
        }
    }
}