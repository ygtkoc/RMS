using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalManagementSystem.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using Dastone.Models;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;
using ClosedXML.Excel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Core;
using Dastone.Controllers;
using System.Drawing;
using System.Json;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;



namespace RentalManagementSystem.Controllers
{
    public class VehiclesController : BaseController
    {
        private readonly RentalDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogger<VehiclesController> _logger;

        public VehiclesController(RentalDbContext context, IWebHostEnvironment environment, ILogger<VehiclesController> logger) : base(context)
        {
            _context = context;
            _environment = environment;
            _hostEnvironment = environment;
            _logger = logger;
        }

        public async Task<IActionResult> RentalDetails(int id)
        {
            if (id == 0)
            {
                return NotFound(); // ID belirtilmemişse 404 döndür
            }

            var rental = await _context.Kiralamalar
                .Include(r => r.Arac)
                .Include(r => r.Musteri)
                .Include(r => r.Lokasyon)
                .Include(r => r.RentalDocuments)
                .FirstOrDefaultAsync(r => r.KiralamaID == id);

            if (rental == null)
            {
                return NotFound(); // Kiralama bulunamazsa 404 döndür
            }

            // Rental objesini doğrudan View'a gönderiyoruz
            return View(rental);
        }

        private async Task UpdateActiveCustomersAsync()
        {
            var currentDate = DateTime.Now;
            var vehicles = await _context.Araclar.ToListAsync();
            foreach (var vehicle in vehicles)
            {
                var activeRental = await _context.Kiralamalar
                    .Where(r => r.AracID == vehicle.AracID && (r.BitisTarihi == null || r.BitisTarihi >= DateOnly.FromDateTime(currentDate)))
                    .OrderByDescending(r => r.BaslangicTarihi)
                    .FirstOrDefaultAsync();

                vehicle.AktifMusteriID = activeRental?.MusteriID;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IActionResult> Index(string searchTerm, string sortBy, string activeTab = "rented")
        {
            // Sade temel sorgu (navsız)
            var baseVehicles = _context.Araclar.AsNoTracking().AsQueryable();

            // Arama
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                baseVehicles = baseVehicles.Where(v =>
                    (v.Plaka ?? "").Contains(searchTerm) ||
                    (v.Marka ?? "").Contains(searchTerm) ||
                    (v.Model ?? "").Contains(searchTerm));
                // AktifMusteri.DEFINITION_ gibi Customer alanlarına burada dokunmuyoruz.
            }

            // Sıralama
            baseVehicles = sortBy switch
            {
                "plakaAsc" => baseVehicles.OrderBy(v => v.Plaka),
                "plakaDesc" => baseVehicles.OrderByDescending(v => v.Plaka),
                "markaAsc" => baseVehicles.OrderBy(v => v.Marka),
                "markaDesc" => baseVehicles.OrderByDescending(v => v.Marka),
                "modelAsc" => baseVehicles.OrderBy(v => v.Model),
                "modelDesc" => baseVehicles.OrderByDescending(v => v.Model),
                _ => baseVehicles.OrderBy(v => v.Plaka)
            };

            // Görünüm modeli iskeleti
            var model = new VehiclesDashboardViewModel
            {
                SearchTerm = searchTerm,
                SortBy = sortBy,
                ActiveTab = activeTab
            };

            // Sort seçenekleri
            var sortOptions = new SelectList(new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "Sırala" },
            new SelectListItem { Value = "plakaAsc", Text = "Plaka (A-Z)" },
            new SelectListItem { Value = "plakaDesc", Text = "Plaka (Z-A)" },
            new SelectListItem { Value = "markaAsc", Text = "Marka (A-Z)" },
            new SelectListItem { Value = "markaDesc", Text = "Marka (Z-A)" },
            new SelectListItem { Value = "modelAsc", Text = "Model (A-Z)" },
            new SelectListItem { Value = "modelDesc", Text = "Model (Z-A)" }
        }, "Value", "Text", model.SortBy);

            // Tarih
            var today = DateOnly.FromDateTime(DateTime.Today);

            // 1) Aktif (bitmemiş/bugünden büyük bitiş) kiralamalardaki araç ID’leri
            var activeVehicleIds = await _context.Kiralamalar
                .AsNoTracking()
                .Where(k => k.BitisTarihi == null || k.BitisTarihi > today)
                .Select(k => k.AracID!)     // AracID int? ise ; null olmayacağını biliyoruz.
                .Distinct()
                .ToListAsync();

            // 2) Müsait araçlar: aktif listede olmayanlar
            var availableVehicles = await baseVehicles
                .Where(a => !activeVehicleIds.Contains(a.AracID))
                .Select(a => new Vehicle
                {
                    AracID = a.AracID,
                    Plaka = a.Plaka,
                    Marka = a.Marka,
                    Model = a.Model,
                    ModelYili = a.ModelYili,
                    LokasyonID = a.LokasyonID,
                    Durum = a.Durum
                })
                .ToListAsync();

            // 3) Kirada araçlar: aktif listede olanlar
            var rentedVehicles = await baseVehicles
                .Where(a => activeVehicleIds.Contains(a.AracID))
                .Select(a => new Vehicle
                {
                    AracID = a.AracID,
                    Plaka = a.Plaka,
                    Marka = a.Marka,
                    Model = a.Model,
                    ModelYili = a.ModelYili,
                    LokasyonID = a.LokasyonID,
                    Durum = a.Durum
                })
                .ToListAsync();

            // 4) İkinci el
            var secondHandVehicles = await baseVehicles
                .Where(v => v.Durum == "İkinci El")
                .Select(a => new Vehicle
                {
                    AracID = a.AracID,
                    Plaka = a.Plaka,
                    Marka = a.Marka,
                    Model = a.Model,
                    ModelYili = a.ModelYili,
                    LokasyonID = a.LokasyonID,
                    Durum = a.Durum
                })
                .ToListAsync();

            // Sayılar
            model.RentedVehicles = rentedVehicles;
            model.AvailableVehicles = availableVehicles;
            model.SecondHandVehicles = secondHandVehicles;

            model.TotalRentedVehiclesCount = rentedVehicles.Count;
            model.TotalAvailableVehiclesCount = availableVehicles.Count;
            model.TotalSecondHandVehiclesCount = secondHandVehicles.Count;

            // Partial modeller
            model.RentedVehiclesPartial = new VehiclesListPartialViewModel
            {
                Vehicles = model.RentedVehicles,
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortOptions = sortOptions,
                ActiveTab = "rented"
            };

            model.AvailableVehiclesPartial = new VehiclesListPartialViewModel
            {
                Vehicles = model.AvailableVehicles,
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortOptions = sortOptions,
                ActiveTab = "available"
            };

            model.SecondHandVehiclesPartial = new VehiclesListPartialViewModel
            {
                Vehicles = model.SecondHandVehicles,
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortOptions = sortOptions,
                ActiveTab = "secondhand"
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult GetVehicleDetails(int id)
        {
            Console.WriteLine($"GetVehicleDetails çağrıldı, ID: {id} - Time: {DateTime.Now}");
            var vehicle = _context.Araclar
                .Include(v => v.Lokasyon)
                .Include(v => v.AktifMusteri)
                .Include(v => v. AracTipiTanimi)
                .FirstOrDefault(v => v.AracID == id);

            if (vehicle == null)
            {
                Console.WriteLine($"Araç bulunamadı, ID: {id}");
                return NotFound(new { success = false, message = "Belirtilen ID ile araç bulunamadı." });
            }

            var vehicleData = new
            {
                aracID = vehicle.AracID,
                plaka = vehicle.Plaka,
                saseNo = vehicle.SaseNo,
                marka = vehicle.Marka,
                model = vehicle.Model,
                yil = vehicle.ModelYili,
                kiralamaBedeli = vehicle.KiralamaBedeli,
                durum = vehicle.Durum,
                lokasyonAdi = vehicle.Lokasyon?.LokasyonAdi ?? "Belirtilmemiş",
                aktifMusteriAdSoyad = vehicle.AktifMusteri != null ? $"{vehicle.AktifMusteri.DEFINITION_} " : "Yok",
                tescilTarihi = vehicle.TescilTarihi?.ToString("dd/MM/yyyy") ?? "Belirtilmemiş",
                yakitTipi = vehicle.YakitTipi,
                vitesTipi = vehicle.VitesTipi,
                kilometreSayaci = vehicle.KMBilgi,
                cekisTipi = vehicle.CekisTipi,
                renk = vehicle.Renk,
                aracTipi = vehicle.AracTipiTanimi?.AracTipiName ?? "Belirtilmemiş",
                motorGucu = vehicle.MotorGucu
            };

            return Json(new { success = true, data = vehicleData });
        }

        [HttpGet]
        public IActionResult GetVehicleDetailsModal(int id, string activeTab)
        {
            Console.WriteLine($"GetVehicleDetailsModal çağrıldı, ID: {id} - ActiveTab: {activeTab} - Time: {DateTime.Now}");

            // 1) Vehicle + gerekli NAV'lar (Customer NAV'ları hariç!)
            var vehicle = _context.Araclar
                .AsNoTracking()
                .Include(v => v.Lokasyon)
                .Include(v => v.AracTipiTanimi)
                .Include(v => v.Cezalar).ThenInclude(c => c.CezaTanimi)
                .Include(v => v.OtoyolGecisleri).ThenInclude(o => o.Lokasyon)
                .Include(v => v.Kiralamalar).ThenInclude(k => k.Lokasyon)
                // .Include(v => v.AktifMusteri)                // <— KALDIRILDI
                // .Include(v => v.Kiralamalar).ThenInclude(k => k.Musteri) // <— KALDIRILDI
                .FirstOrDefault(v => v.AracID == id);

            if (vehicle == null)
            {
                Console.WriteLine($"Araç bulunamadı, ID: {id}");
                return PartialView("_VehicleDetailsModalError", new { message = "Belirtilen ID ile araç bulunamadı." });
            }

            // 2) Aktif müşteri adını hafif bir sorguyla getir (yalnızca lazım olan alan)
            string? aktifMusteriAdi = null;
            if (vehicle.AktifMusteriID.HasValue)
            {
                aktifMusteriAdi = _context.Customers
                    .AsNoTracking()
                    .Where(c => c.LOGICALREF == vehicle.AktifMusteriID.Value)
                    .Select(c => (c.DEFINITION_ ?? ((c.NAME ?? "") + " " + (c.SURNAME ?? ""))).Trim())
                    .FirstOrDefault();
            }
            ViewBag.AktifMusteriAdi = string.IsNullOrWhiteSpace(aktifMusteriAdi) ? null : aktifMusteriAdi;

            // 3) Kiralamalardaki müşteri isimlerini tek seferde çekip dictionary olarak gönder
            var rentalCustomerIds = vehicle.Kiralamalar
                .Where(k => k.MusteriID.HasValue)
                .Select(k => k.MusteriID!.Value)
                .Distinct()
                .ToList();

            var rentalCustomerMap = new Dictionary<int, string>();
            if (rentalCustomerIds.Count > 0)
            {
                rentalCustomerMap = _context.Customers
                    .AsNoTracking()
                    .Where(c => rentalCustomerIds.Contains(c.LOGICALREF))
                    .Select(c => new
                    {
                        c.LOGICALREF,
                        Ad = (c.DEFINITION_ ?? ((c.NAME ?? "") + " " + (c.SURNAME ?? ""))).Trim()
                    })
                    .ToDictionary(x => x.LOGICALREF, x => x.Ad);
            }
            ViewBag.RentalCustomerMap = rentalCustomerMap;
            // View'da: kiralama satırı için adı = 
            // ViewBag.RentalCustomerMap.ContainsKey(k.MusteriID ?? -1) ? ViewBag.RentalCustomerMap[k.MusteriID.Value] : "-"

            // 4) Diğer listeler (dropdown vs.)
            ViewBag.Lokasyonlar = _context.Lokasyonlar.AsNoTracking().ToList();
            ViewBag.AracTipiTanimi = _context.AracTipiTanimi.AsNoTracking().ToList();

            Console.WriteLine($"Araç bulundu, AracID: {vehicle.AracID} döndürülüyor.");
            return PartialView("_VehicleDetailsModalContent", vehicle);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVehicle(Vehicle arac, IFormFile TrafikBelgesi, IFormFile KaskoBelgesi, IFormFile MTV1Belgesi, IFormFile MTV2Belgesi, IFormFile KabisBelgesi)
        {
            Console.WriteLine($"UpdateVehicle çağrıldı, Model: {(arac != null ? "Var" : "Null")}, Model.AracID: {(arac?.AracID ?? 0)} - Time: {DateTime.Now}");
            Console.WriteLine($"Raw Request Form: {string.Join(", ", Request.Form.Select(kv => $"{kv.Key}: {kv.Value}"))}");

            if (arac == null)
            {
                Console.WriteLine("Hata: Model null olarak geldi.");
                return Json(new { success = false, message = "Geçersiz veri gönderildi.", errors = new[] { "Model verisi eksik." } });
            }

            if (arac.AracID == 0)
            {
                Console.WriteLine("Hata: AracID 0 olarak geldi, geçersiz bir araç kimliği. ModelState: " + string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                Console.WriteLine($"Request.Form['AracID']: {Request.Form["AracID"]}");
                return Json(new { success = false, message = "Araç ID'si geçersiz (0). Lütfen geçerli bir araç seçin.", errors = new[] { "Araç ID'si eksik veya geçersiz." } });
            }

            // Dosya alanlarının zorunlu olmadığını belirt
            ModelState.Remove("TrafikBelgesi");
            ModelState.Remove("KaskoBelgesi");
            ModelState.Remove("MTV1Belgesi");
            ModelState.Remove("MTV2Belgesi");
            ModelState.Remove("KabisBelgesi");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.WriteLine("Model doğrulama hataları: " + string.Join(", ", errors));
                return Json(new { success = false, message = "Geçersiz veri.", errors = errors });
            }

            var vehicle = await _context.Araclar
                .Include(v => v.Lokasyon)
                .Include(v => v.AracTipiTanimi)
                .FirstOrDefaultAsync(v => v.AracID == arac.AracID);

            if (vehicle == null)
            {
                Console.WriteLine($"Hata: AracID {arac.AracID} ile araç bulunamadı.");
                return Json(new { success = false, message = "Araç bulunamadı." });
            }

            // Mevcut değerleri güncelle
            vehicle.Plaka = arac.Plaka;
            vehicle.SaseNo = arac.SaseNo;
            vehicle.Marka = arac.Marka;
            vehicle.Aciklama = arac.Aciklama;
            vehicle.Model = arac.Model;
            vehicle.ModelYili = arac.ModelYili;
            vehicle.TrafikBaslangicTarihi = arac.TrafikBaslangicTarihi;
            vehicle.TrafikBitisTarihi = arac.TrafikBitisTarihi;
            vehicle.YakitTipi = arac.YakitTipi;
            vehicle.VitesTipi = arac.VitesTipi;
            vehicle.KMBilgi = arac.KMBilgi;
            vehicle.CekisTipi = arac.CekisTipi;
            vehicle.Renk = arac.Renk;
            vehicle.MotorGucu = arac.MotorGucu;
            vehicle.KiralamaBedeli = arac.KiralamaBedeli;
            vehicle.Durum = arac.Durum;
            vehicle.LokasyonID = arac.LokasyonID;
            vehicle.AracTipiID = arac.AracTipiID;
            vehicle.AracBedeli = arac.AracBedeli;
            vehicle.AracAlisFiyati = arac.AracAlisFiyati;
            vehicle.KaskoBaslangicTarihi = arac.KaskoBaslangicTarihi;
            vehicle.KaskoBitisTarihi = arac.KaskoBitisTarihi;
            vehicle.MTV1Odendi = arac.MTV1Odendi;
            vehicle.MTV2Odendi = arac.MTV2Odendi;
            vehicle.TescilTarihi = arac.TescilTarihi;
            vehicle.HizmetKodu = arac.HizmetKodu;
            vehicle.KabisGirisTarihi = arac.KabisGirisTarihi;
            vehicle.KabisBitisTarihi = arac.KabisBitisTarihi;
            vehicle.KabisID = arac.KabisID;

            // Dosya yükleme isteğe bağlı, yalnızca yüklendiyse işle
            if (TrafikBelgesi != null) vehicle.TrafikBelgesi = await UploadFile(TrafikBelgesi, "trafik");
            if (KaskoBelgesi != null) vehicle.KaskoBelgesi = await UploadFile(KaskoBelgesi, "kasko");
            if (MTV1Belgesi != null) vehicle.MTV1Belgesi = await UploadFile(MTV1Belgesi, "mtv1");
            if (MTV2Belgesi != null) vehicle.MTV2Belgesi = await UploadFile(MTV2Belgesi, "mtv2");
            if (KabisBelgesi != null) vehicle.KabisBelgesi = await UploadFile(KabisBelgesi, "kabis");

            _context.Araclar.Update(vehicle);
            await _context.SaveChangesAsync();

            Console.WriteLine($"Araç başarıyla güncellendi, AracID: {arac.AracID}");
            return Json(new { success = true, message = "Araç başarıyla güncellendi.", redirectUrl = Url.Action("Index", "Vehicles", new { activeTab = "rented", success = true }) });
        }

        private async Task<string> UploadFile(IFormFile file, string folder)
        {
            try
            {
                if (file != null && file.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
                    Directory.CreateDirectory(uploadsFolder);
                    var filePath = Path.Combine(uploadsFolder, Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    Console.WriteLine($"Dosya yüklendi: {filePath}");
                    return $"/uploads/{folder}/{Path.GetFileName(filePath)}";
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya yükleme hatası: {ex.Message}");
                return null;
            }
        }



        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> CreateVehicleFormPartial()
        {
            var lokasyonlar = await _context.Lokasyonlar.ToListAsync();
            if (!lokasyonlar.Any())
            {
                _context.Lokasyonlar.AddRange(
                    new Lokasyon { LokasyonAdi = "İstanbul", Aciklama = "Merkez" },
                    new Lokasyon { LokasyonAdi = "Ankara", Aciklama = "Şube" }
                );
                try
                {
                    await _context.SaveChangesAsync();
                    lokasyonlar = await _context.Lokasyonlar.ToListAsync();
                    Console.WriteLine($"Eklenen lokasyon sayısı: {lokasyonlar.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}");
                    return StatusCode(500, "Lokasyonlar eklenirken hata oluştu.");
                }
            }
            var aracTipleri = await _context.AracTipiTanimi.ToListAsync();
            if (!aracTipleri.Any())
            {
                try
                {
                    await _context.SaveChangesAsync();
                    aracTipleri = await _context.AracTipiTanimi.ToListAsync();
                    Console.WriteLine($"Eklenen Araç Tipi sayısı: {aracTipleri.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}");
                    return StatusCode(500, "Lokasyonlar eklenirken hata oluştu.");
                }
            }
            ViewBag.AracTipiTanimi = new SelectList(aracTipleri, "AracTipiID", "AracTipiName");
            ViewBag.Lokasyonlar = new SelectList(lokasyonlar, "LokasyonID", "LokasyonAdi");
            return PartialView("_CreateVehicleFormPartial", new Vehicle());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Vehicle vehicle)
        {
            bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (ModelState.IsValid)
            {
                try
                {
                    if (await _context.Araclar.AnyAsync(v => v.Plaka == vehicle.Plaka))
                    {
                        ModelState.AddModelError("Plaka", "Bu plaka zaten kayıtlı.");
                        if (isAjaxRequest)
                        {
                            return Json(new { success = false, message = "Plaka zaten kayıtlı.", errors = GetModelStateErrors() });
                        }
                        ViewBag.Lokasyonlar = new SelectList(await _context.Lokasyonlar.ToListAsync(), "LokasyonID", "LokasyonAdi");
                        return PartialView("_CreateVehicleFormPartial", vehicle);
                    }

                    if (string.IsNullOrEmpty(vehicle.Durum))
                    {
                        vehicle.Durum = "Müsait";
                    }

                    _context.Add(vehicle);
                    await _context.SaveChangesAsync();

                    if (isAjaxRequest)
                    {
                        return Json(new { success = true, message = "Araç başarıyla eklendi!" });
                    }

                    TempData["SuccessMessage"] = "Araç başarıyla eklendi!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Yeni araç oluştururken hata oluştu: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        ModelState.AddModelError("", $"Inner exception: {ex.InnerException.Message}");
                    }

                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = "Araç eklenirken sunucu hatası oluştu.", errors = GetModelStateErrors() });
                    }

                    ViewBag.Lokasyonlar = new SelectList(await _context.Lokasyonlar.ToListAsync(), "LokasyonID", "LokasyonAdi");
                    return PartialView("_CreateVehicleFormPartial", vehicle);
                }
            }

            if (isAjaxRequest)
            {
                return Json(new { success = false, message = "Lütfen formdaki hataları düzeltin.", errors = GetModelStateErrors() });
            }

            ViewBag.Lokasyonlar = new SelectList(await _context.Lokasyonlar.ToListAsync(), "LokasyonID", "LokasyonAdi");
            return PartialView("_CreateVehicleFormPartial", vehicle);
        }

        // ModelState hatalarını JSON'a dönüştürmek için yardımcı metod
        private Dictionary<string, string[]> GetModelStateErrors()
        {
            return ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
            );
        }


        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            await UpdateActiveCustomersAsync();
            ViewBag.Lokasyonlar = new SelectList(await _context.Lokasyonlar.ToListAsync(), "LokasyonID", "LokasyonAdi");
            var vehicle = await _context.Araclar
                .Include(v => v.AktifMusteri)
                .Include(v => v.Kiralamalar)
                    .ThenInclude(r => r.Musteri)
                .Include(v => v.Cezalar)
                .FirstOrDefaultAsync(v => v.AracID == id);

            if (vehicle == null)
            {
                return NotFound();
            }
            return View(vehicle);
        }

        [HttpPost]
        public async Task<IActionResult> Details(int id, IFormFile? trafikBelgesiFile, IFormFile? kaskoBelgesiFile, IFormFile? mtv1BelgesiFile, IFormFile? mtv2BelgesiFile, bool mtv1Odendi, bool mtv2Odendi)
        {
            var vehicle = await _context.Araclar
                .Include(v => v.AktifMusteri)
                .FirstOrDefaultAsync(v => v.AracID == id);

            if (vehicle == null)
            {
                return NotFound();
            }

            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            if (trafikBelgesiFile != null && trafikBelgesiFile.Length > 0)
            {
                string filePath = Path.Combine(uploadsFolder, $"trafik_{id}_{trafikBelgesiFile.FileName}");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await trafikBelgesiFile.CopyToAsync(stream);
                }
                vehicle.TrafikBelgesi = $"/uploads/trafik_{id}_{trafikBelgesiFile.FileName}";
            }

            if (kaskoBelgesiFile != null && kaskoBelgesiFile.Length > 0)
            {
                string filePath = Path.Combine(uploadsFolder, $"kasko_{id}_{kaskoBelgesiFile.FileName}");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await kaskoBelgesiFile.CopyToAsync(stream);
                }
                vehicle.KaskoBelgesi = $"/uploads/kasko_{id}_{kaskoBelgesiFile.FileName}";
            }

            if (mtv1BelgesiFile != null && mtv1BelgesiFile.Length > 0)
            {
                string filePath = Path.Combine(uploadsFolder, $"mtv1_{id}_{mtv1BelgesiFile.FileName}");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await mtv1BelgesiFile.CopyToAsync(stream);
                }
                vehicle.MTV1Belgesi = $"/uploads/mtv1_{id}_{mtv1BelgesiFile.FileName}";
            }

            if (mtv2BelgesiFile != null && mtv2BelgesiFile.Length > 0)
            {
                string filePath = Path.Combine(uploadsFolder, $"mtv2_{id}_{mtv2BelgesiFile.FileName}");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await mtv2BelgesiFile.CopyToAsync(stream);
                }
                vehicle.MTV2Belgesi = $"/uploads/mtv2_{id}_{mtv2BelgesiFile.FileName}";
            }

            vehicle.MTV1Odendi = !string.IsNullOrEmpty(vehicle.MTV1Belgesi) && mtv1Odendi;
            vehicle.MTV2Odendi = !string.IsNullOrEmpty(vehicle.MTV2Belgesi) && mtv2Odendi;

            await _context.SaveChangesAsync();
            await UpdateActiveCustomersAsync();

            return RedirectToAction("Details", new { id = vehicle.AracID });
        }

        // Yeni HTTP GET metodu: Düzenleme formunu bir PartialView olarak döndürür
        [HttpGet]
        public async Task<IActionResult> EditVehicleFormPartial(int id) // İsim değişikliği
        {
            var vehicle = await _context.Araclar
                .Include(v => v.Lokasyon) // Lokasyon bilgisini de formda göstermek için
                .FirstOrDefaultAsync(v => v.AracID == id);

            if (vehicle == null)
            {
                // Araç bulunamazsa boş bir PartialView veya hata mesajı döndürülebilir.
                // JavaScript tarafı bunu yakalayıp kullanıcıya gösterecek.
                return NotFound("Araç bilgileri bulunamadı.");
            }

            // Lokasyonlar için SelectList'i ViewBag'e ekle
            ViewBag.Lokasyonlar = new SelectList(await _context.Lokasyonlar.ToListAsync(), "LokasyonID", "LokasyonAdi", vehicle.LokasyonID);

            return PartialView("_EditVehicleFormPartial", vehicle);
        }

        // HTTP POST metodu: Araç bilgilerini günceller ve JSON yanıt döndürür
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Vehicle vehicle, IFormFile? trafikBelgesiFile, IFormFile? kaskoBelgesiFile, IFormFile? mtv1BelgesiFile, IFormFile? mtv2BelgesiFile) // bool'lar Vehicle modelinden gelmeli
        {
            // MTV1Odendi ve MTV2Odendi Vehicle modelinin kendisinde olmalı
            // Eğer modelinizde yoksa, buraya parametre olarak eklemeye devam edebilirsiniz:
            // bool mtv1Odendi, bool mtv2Odendi
            // ve aşağıdaki atamalarda kullanabilirsiniz.

            if (id != vehicle.AracID)
            {
                return BadRequest(new { success = false, message = "Geçersiz araç ID'si." });
            }

            // ModelState kontrolü: Hatalar varsa istemciye JSON olarak gönder
            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Lütfen formdaki hataları düzeltin.", errors = errors });
            }

            try
            {
                var existingVehicle = await _context.Araclar
                    .FirstOrDefaultAsync(v => v.AracID == id);

                if (existingVehicle == null)
                {
                    return NotFound(new { success = false, message = "Güncellenecek araç bulunamadı." });
                }

                // Plaka çakışması kontrolü
                if (await _context.Araclar.AnyAsync(v => v.Plaka == vehicle.Plaka && v.AracID != vehicle.AracID))
                {
                    return Json(new { success = false, message = "Bu plaka zaten başka bir araca kayıtlı.", errors = new { Plaka = new[] { "Bu plaka zaten başka bir araca kayıtlı." } } });
                }

                // Gelen modelden property'leri güncelle
                existingVehicle.Plaka = vehicle.Plaka;
                existingVehicle.SaseNo = vehicle.SaseNo;
                existingVehicle.Aciklama = vehicle.Aciklama;
                existingVehicle.Marka = vehicle.Marka;
                existingVehicle.Model = vehicle.Model;
                existingVehicle.ModelYili = vehicle.ModelYili;
                existingVehicle.TrafikBaslangicTarihi = vehicle.TrafikBaslangicTarihi;
                existingVehicle.TrafikBitisTarihi = vehicle.TrafikBitisTarihi;
                existingVehicle.KaskoBaslangicTarihi = vehicle.KaskoBaslangicTarihi;
                existingVehicle.KaskoBitisTarihi = vehicle.KaskoBitisTarihi;
                existingVehicle.TescilTarihi = vehicle.TescilTarihi;
                existingVehicle.LokasyonID = vehicle.LokasyonID;
                existingVehicle.KiralamaBedeli = vehicle.KiralamaBedeli;
                existingVehicle.AracBedeli = vehicle.AracBedeli;
                existingVehicle.AracAlisFiyati = vehicle.AracAlisFiyati;
                existingVehicle.HizmetKodu = vehicle.HizmetKodu;
                existingVehicle.MTV1Odendi = vehicle.MTV1Odendi; // checkbox değerleri doğrudan modelden alınır
                existingVehicle.MTV2Odendi = vehicle.MTV2Odendi; // checkbox değerleri doğrudan modelden alınır
                existingVehicle.RuhsatNo = vehicle.RuhsatNo;

                // Dosya yükleme işlemleri
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                if (trafikBelgesiFile != null && trafikBelgesiFile.Length > 0)
                {
                    string filePath = Path.Combine(uploadsFolder, $"trafik_{id}_{trafikBelgesiFile.FileName}");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await trafikBelgesiFile.CopyToAsync(stream);
                    }
                    existingVehicle.TrafikBelgesi = $"/uploads/trafik_{id}_{trafikBelgesiFile.FileName}";
                }

                if (kaskoBelgesiFile != null && kaskoBelgesiFile.Length > 0)
                {
                    string filePath = Path.Combine(uploadsFolder, $"kasko_{id}_{kaskoBelgesiFile.FileName}");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await kaskoBelgesiFile.CopyToAsync(stream);
                    }
                    existingVehicle.KaskoBelgesi = $"/uploads/kasko_{id}_{kaskoBelgesiFile.FileName}";
                }

                if (mtv1BelgesiFile != null && mtv1BelgesiFile.Length > 0)
                {
                    string filePath = Path.Combine(uploadsFolder, $"mtv1_{id}_{mtv1BelgesiFile.FileName}");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await mtv1BelgesiFile.CopyToAsync(stream);
                    }
                    existingVehicle.MTV1Belgesi = $"/uploads/mtv1_{id}_{mtv1BelgesiFile.FileName}";
                }

                if (mtv2BelgesiFile != null && mtv2BelgesiFile.Length > 0)
                {
                    string filePath = Path.Combine(uploadsFolder, $"mtv2_{id}_{mtv2BelgesiFile.FileName}");
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await mtv2BelgesiFile.CopyToAsync(stream);
                    }
                    existingVehicle.MTV2Belgesi = $"/uploads/mtv2_{id}_{mtv2BelgesiFile.FileName}";
                }

                await _context.SaveChangesAsync();
                // await UpdateActiveCustomersAsync(); // Bu metot uygulamanızın genel akışına göre çağrılabilir

                return Json(new { success = true, message = "Araç başarıyla güncellendi.", redirectUrl = Url.Action("Index", "Vehicles", new { success = true }) });
            }
            catch (Exception ex)
            {
                // Daha detaylı hata loglaması yapın
                Console.WriteLine($"Araç güncellenirken hata oluştu: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }

                // Hata durumunda JSON yanıtı döndürün
                return Json(new { success = false, message = $"Araç güncellenirken bir hata oluştu: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var vehicle = await _context.Araclar
                .Include(v => v.AktifMusteri)
                .FirstOrDefaultAsync(v => v.AracID == id);

            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var vehicle = await _context.Araclar.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }

            try
            {
                _context.Araclar.Remove(vehicle);
                await _context.SaveChangesAsync();
                await UpdateActiveCustomersAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Aracı silerken bir hata oluştu. Hata: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", $"Inner exception: {ex.InnerException.Message}");
                }
                return View(vehicle);
            }
        }

        [HttpGet]
        public async Task<IActionResult> AddPenalty(int id)
        {
            var vehicle = await _context.Araclar
                                .FirstOrDefaultAsync(v => v.AracID == id);

            if (vehicle == null)
            {
                return NotFound();
            }

            var viewModel = new CezaViewModel
            {
                AracID = id,
                SelectedVehicleDisplayPenalty = $"{vehicle.Marka} {vehicle.Model} ({vehicle.Plaka})",
                CezaTarihi = DateTime.Today, // Varsayılan tarih
                Odendi = false // Varsayılan değer
            };

            // Opsiyonel: Eğer araçla ilişkili son kiralamadan müşteri çekmek isterseniz
            var latestRental = await _context.Kiralamalar
                                .Where(r => r.AracID == id)
                                .OrderByDescending(r => r.BaslangicTarihi)
                                .Include(r => r.Musteri)
                                .FirstOrDefaultAsync();

            if (latestRental != null)
            {
                viewModel.MusteriID = (int)latestRental.MusteriID;
                viewModel.SelectedCustomerDisplayPenalty = $"{latestRental.Musteri.DEFINITION_} ({latestRental.Musteri.TCKNO})";
            }
            else
            {
                // Eğer kiralama yoksa müşteri ID'yi 0 veya boş bırakabiliriz,
                // veya kullanıcıdan müşteri seçmesini bekleyebiliriz.
                // ViewModel'de [Required] olduğu için, kullanıcı seçmediğinde hata alacaktır.
                viewModel.MusteriID = 0;
                viewModel.SelectedCustomerDisplayPenalty = "Müşteri Seçilmedi";
            }

            return View(viewModel);
        }

        // AddPenalty (POST) - Yeni Ceza Ekleme İşlemi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPenalty(CezaViewModel viewModel)
        {
            // ModelState.IsValid kontrolünden önce özel validasyonlar
            // Eğer ViewModel'de [Required] varsa bu kontroller gereksiz olabilir,
            // ama ID'nin veritabanında varlığını kontrol etmek her zaman iyidir.
            if (!await _context.Araclar.AnyAsync(a => a.AracID == viewModel.AracID))
            {
                ModelState.AddModelError("AracID", "Geçersiz araç seçimi.");
            }
            if (!await _context.Customers.AnyAsync(m => m.LOGICALREF == viewModel.MusteriID))
            {
                ModelState.AddModelError("MusteriID", "Geçersiz müşteri seçimi.");
            }
            if (!await _context.CezaTanimlari.AnyAsync(ct => ct.CezaTanimiID == viewModel.CezaTanimiID))
            {
                ModelState.AddModelError("CezaTanimiID", "Geçersiz ceza tanımı seçimi.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var ceza = new Ceza
                    {
                        AracID = viewModel.AracID,
                        MusteriID = viewModel.MusteriID,
                        CezaTanimiID = viewModel.CezaTanimiID,
                        CezaTarihi = viewModel.CezaTarihi,
                        Tutar = viewModel.Tutar,
                        Aciklama = viewModel.Aciklama,
                        CezaYeri = viewModel.CezaYeri,
                        Odendi = viewModel.Odendi
                        // CezaBelgesi burada null olacak, eğer dosya yüklendiyse güncellenecek
                    };

                    // Dosya yükleme işlemini ele al
                    if (viewModel.CezaBelgesiFile != null && viewModel.CezaBelgesiFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "cezalar"); // Alt klasör olarak "cezalar"
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }
                        var uniqueFileName = $"ceza_{Guid.NewGuid()}_{viewModel.CezaBelgesiFile.FileName}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await viewModel.CezaBelgesiFile.CopyToAsync(stream);
                        }
                        ceza.CezaBelgesi = $"/uploads/cezalar/{uniqueFileName}"; // Veritabanına göreli yolu kaydet
                    }

                    _context.Cezalar.Add(ceza);
                    await _context.SaveChangesAsync();

                    // Başarılı olursa, araca ait detay sayfasına yönlendir, ceza sekmesi açık olsun
                    return Json(new { success = true, message = "Ceza başarıyla oluşturuldu.", redirectUrl = Url.Action("Index", "Vehicles", new { id = ceza.AracID, activeTab = "rented" }) });
                }
                catch (Exception ex)
                {
                    // Hata durumunda model state'i döndürmek yerine JSON hatası döndür
                    Console.WriteLine($"Ceza oluşturulurken hata: {ex.Message}\nStackTrace: {ex.StackTrace}");
                    var errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                    if (!errors.ContainsKey("Genel"))
                    {
                        errors.Add("Genel", new[] { $"Ceza oluşturulurken beklenmeyen bir hata oluştu: {ex.Message}" });
                    }
                    return Json(new { success = false, message = "Ceza oluşturulurken bir hata oluştu.", errors = errors });
                }
            }
            else
            {
                // Model validasyonu başarısız oldu, hataları JSON olarak döndür
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Lütfen formdaki hataları düzeltin.", errors = errors });
            }
        }

        // CreatePenaltyFormPartial (GET) - Ajax ile yüklenen kısmi form
        [HttpGet]
        public IActionResult CreatePenaltyFormPartial()
        {
            try
            {
                var viewModel = new CezaViewModel
                {
                    CezaTarihi = DateTime.Today // Varsayılan olarak bugünün tarihini atayın
                };
                return PartialView("_CreatePenaltyFormPartial", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreatePenaltyFormPartial: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { success = false, message = $"Ceza formu yüklenemedi: {ex.Message}" });
            }
        }

        // EditPenalty (GET) - Ceza Düzenleme Formu
        [HttpGet]
        public async Task<IActionResult> EditPenalty(int id)
        {
            var penalty = await _context.Cezalar
                                .Include(c => c.Arac)
                                .Include(c => c.Musteri)
                                .Include(c => c.CezaTanimi) // Ceza Tanımı bilgilerini de dahil et
                                .FirstOrDefaultAsync(c => c.CezaID == id);

            if (penalty == null)
            {
                return NotFound();
            }

            var viewModel = new CezaViewModel
            {
                CezaID = penalty.CezaID,
                AracID = (int)penalty.AracID,
                SelectedVehicleDisplayPenalty = $"{penalty.Arac.Marka} {penalty.Arac.Model} ({penalty.Arac.Plaka})",
                MusteriID = (int)penalty.MusteriID,
                SelectedCustomerDisplayPenalty = $"{penalty.Musteri.DEFINITION_} ({penalty.Musteri.TCKNO})",
                CezaTanimiID = penalty.CezaTanimiID,
                SelectedPenaltyDefinitionDisplay = $"{penalty.CezaTanimi.CezaKodu} - {penalty.CezaTanimi.KisaAciklama}",
                CezaTarihi = penalty.CezaTarihi,
                Tutar = penalty.Tutar,
                Aciklama = penalty.Aciklama,
                CezaYeri = penalty.CezaYeri,
                Odendi = penalty.Odendi,
                ExistingCezaBelgesi = penalty.CezaBelgesi // Mevcut belge yolunu ViewModel'e atayın
            };

            return View(viewModel);
        }

        // EditPenalty (POST) - Ceza Düzenleme İşlemi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPenalty(int id, CezaViewModel viewModel)
        {
            if (id != viewModel.CezaID)
            {
                return BadRequest();
            }

            // ViewModeldeki validasyonlar çalışacak.
            // Ayrıca DB'den kontrol
            if (!await _context.Araclar.AnyAsync(a => a.AracID == viewModel.AracID))
            {
                ModelState.AddModelError("AracID", "Geçersiz araç seçimi.");
            }
            if (!await _context.Customers.AnyAsync(m => m.LOGICALREF == viewModel.MusteriID))
            {
                ModelState.AddModelError("MusteriID", "Geçersiz müşteri seçimi.");
            }
            if (!await _context.CezaTanimlari.AnyAsync(ct => ct.CezaTanimiID == viewModel.CezaTanimiID))
            {
                ModelState.AddModelError("CezaTanimiID", "Geçersiz ceza tanımı seçimi.");
            }

            if (ModelState.IsValid)
            {
                var existingPenalty = await _context.Cezalar.FindAsync(id);
                if (existingPenalty == null)
                {
                    return NotFound();
                }

                try
                {
                    // ViewModel'den gelen değerleri mevcut entity'e aktar
                    existingPenalty.AracID = viewModel.AracID; // Araç değişebiliyorsa
                    existingPenalty.MusteriID = viewModel.MusteriID; // Müşteri değişebiliyorsa
                    existingPenalty.CezaTanimiID = viewModel.CezaTanimiID;
                    existingPenalty.CezaTarihi = viewModel.CezaTarihi;
                    existingPenalty.Tutar = viewModel.Tutar;
                    existingPenalty.Aciklama = viewModel.Aciklama;
                    existingPenalty.CezaYeri = viewModel.CezaYeri;
                    existingPenalty.Odendi = viewModel.Odendi;

                    // Dosya yükleme veya güncelleme
                    if (viewModel.CezaBelgesiFile != null && viewModel.CezaBelgesiFile.Length > 0)
                    {
                        // Eski dosyayı silmek isterseniz burada silme işlemi yapabilirsiniz.
                        // if (!string.IsNullOrEmpty(existingPenalty.CezaBelgesi))
                        // {
                        //     var oldFilePath = Path.Combine(_environment.WebRootPath, existingPenalty.CezaBelgesi.TrimStart('/'));
                        //     if (System.IO.File.Exists(oldFilePath))
                        //     {
                        //         System.IO.File.Delete(oldFilePath);
                        //     }
                        // }

                        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "cezalar");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }
                        var uniqueFileName = $"ceza_{Guid.NewGuid()}_{viewModel.CezaBelgesiFile.FileName}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await viewModel.CezaBelgesiFile.CopyToAsync(stream);
                        }
                        existingPenalty.CezaBelgesi = $"/uploads/cezalar/{uniqueFileName}";
                    }
                    // Eğer yeni dosya yüklenmediyse, mevcut dosya yolu değişmez.

                    _context.Update(existingPenalty);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("AllPenalties"); // Tüm cezaları listeleme sayfasına yönlendir
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Ceza güncellerken bir hata oluştu. Hata: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        ModelState.AddModelError("", $"Inner exception: {ex.InnerException.Message}");
                    }
                    // Hata durumunda View'ı ViewModel ile döndürmek gerekir
                    // Display alanlarını tekrar doldurun yoksa viewda boş görünürler
                    var vehicle = await _context.Araclar.FindAsync(viewModel.AracID);
                    if (vehicle != null) viewModel.SelectedVehicleDisplayPenalty = $"{vehicle.Marka} {vehicle.Marka} ({vehicle.Plaka})";
                    var customer = await _context.Customers.FindAsync(viewModel.MusteriID);
                    if (customer != null) viewModel.SelectedCustomerDisplayPenalty = $"{customer.DEFINITION_} ({customer.TCKNO})";
                    var penaltyDef = await _context.CezaTanimlari.FindAsync(viewModel.CezaTanimiID);
                    if (penaltyDef != null) viewModel.SelectedPenaltyDefinitionDisplay = $"{penaltyDef.CezaKodu} - {penaltyDef.KisaAciklama}";

                    return View(viewModel);
                }
            }
            // Model state geçerli değilse, hataları içeren ViewModel'i View'a geri gönder
            return View(viewModel);
        }

        // UpdatePenaltyStatus - Durum güncelleme (ViewModel'e ihtiyaç duymaz, zaten basit bir işlem)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePenaltyStatus(int id, bool odendi)
        {
            var penalty = await _context.Cezalar.FindAsync(id);
            if (penalty == null)
            {
                return NotFound();
            }

            penalty.Odendi = odendi;
            await _context.SaveChangesAsync();

            return RedirectToAction("AllPenalties");
        }

        // AllPenalties - Tüm cezaları listeleme (ViewModel'e ihtiyaç duymaz, sadece veri okuyor)
        [HttpGet]
        public async Task<IActionResult> AllPenalties()
        {
            ViewBag.Penalties = await _context.Cezalar
                                    .Include(c => c.Arac)
                                    .Include(c => c.Musteri)
                                    .Include(c => c.CezaTanimi) // Ceza tanımını da dahil et
                                    .ToListAsync();

            ViewBag.HighwayPasses = await _context.OtoyolGecisleri
                                    .Include(h => h.Arac)
                                    .Include(h => h.Musteri)
                                    .ToListAsync();

            ViewBag.ActiveTab = "penalties"; // Varsayılan sekme
            return View();
        }

        // GetPenaltyDefinitions metodu ParametersController'da olmalı, burada olmamalı.
        // Eğer hala buradaysa, yukarıdaki JavaScript'ten gelen çağrılar için doğru bir şekilde CezaTanimiID'yi döndürdüğünden emin olun (yani "id" olarak).
        /*
        [HttpGet]
        public async Task<IActionResult> GetPenaltyDefinitions(string query)
        {
            var definitions = _context.CezaTanimlari.AsQueryable();
            if (!string.IsNullOrEmpty(query))
            {
                definitions = definitions.Where(pd => pd.CezaKodu.Contains(query) || pd.KisaAciklama.Contains(query));
            }
            var result = await definitions
                .Select(pd => new { id = pd.CezaTanimiID, cezaKodu = pd.CezaKodu, kisaAciklama = pd.KisaAciklama }) // CezaTanimiID olarak döndürün
                .ToListAsync();
            return Json(result);
        }
        */


        public async Task<IActionResult> ActiveRentals()
        {
            var currentDate = DateOnly.FromDateTime(DateTime.Now); // DateOnly olarak al

            // Aktif kiralamalar: Bitiş tarihi null olanlar veya bitiş tarihi bugünden sonra olanlar
            var activeRentals = await _context.Kiralamalar
                .Include(r => r.Arac)
                    .ThenInclude(a => a.Lokasyon) // Arac'ın Lokasyon'unu da dahil et
                .Include(r => r.Musteri)
                // DateOnly karşılaştırması
                .Where(r => r.BitisTarihi == null || r.BitisTarihi >= currentDate)
                .OrderByDescending(r => r.BaslangicTarihi) // Daha düzenli bir görünüm için sıralama
                .ToListAsync();

            // Tüm kiralamalar: Geçmiş ve Aktif
            var allRentals = await _context.Kiralamalar
                .Include(r => r.Arac)
                    .ThenInclude(a => a.Lokasyon)
                .Include(r => r.Musteri)
                .OrderByDescending(r => r.BaslangicTarihi) // Tüm kiralamaları başlangıç tarihine göre sırala
                .ToListAsync();

            // ViewBag üzerinden tüm kiralamaları gönder
            ViewBag.AllRentals = allRentals;

            // Lokasyonlar SelectList için (eğer sayfada kullanılıyorsa)
            ViewBag.Lokasyonlar = new SelectList(await _context.Lokasyonlar.ToListAsync(), "LokasyonID", "LokasyonAdi");

            // View'a sadece aktif kiralamaları gönder
            return View(activeRentals);
        }

        // Mevcut CreateRentalFormPartial metodu güncellendi
        // Mevcut CreateRentalFormPartial metodu güncellendi
        [HttpGet]
        public IActionResult CreateRentalFormPartial()
        {
            try
            {
                if (_context == null)
                    throw new InvalidOperationException("Database context is not initialized.");

                // ViewModel kullanarak View'a veri gönderiyoruz
                var model = new CreateRentalViewModel
                {
                    BaslangicTarihi = DateOnly.FromDateTime(DateTime.Today), // Varsayılan olarak bugünün tarihi
                    AvailableVehicles = new List<SearchItem>(), // Başlangıçta boş listeler
                    AvailableCustomers = new List<SearchItem>()
                };

                Console.WriteLine($"CreateRentalFormPartial called at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                return PartialView("_CreateRentalFormPartial", model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateRentalFormPartial: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { success = false, message = $"Form yüklenemedi: {ex.Message}" });
            }
        }


        // GET: Araçları modal için filtrele - KiralamaBedeli kullanıldı
        [HttpGet]
        public async Task<IActionResult> GetVehicles(string query)
        {
            var vehicles = await _context.Araclar
                .Where(v =>
                           (string.IsNullOrEmpty(query) ||
                            v.Plaka.Contains(query) || v.Marka.Contains(query) || v.Model.Contains(query)))
                .Select(v => new
                {
                    id = v.AracID,
                    marka = v.Marka,
                    model = v.Model,
                    plaka = v.Plaka,
                    kiralamaBedeli = v.KiralamaBedeli
                }).ToListAsync();
            return Json(vehicles);
        }

        [HttpGet]
public async Task<IActionResult> GetCustomers(string query)
{
    const int defaultPageSize = 100;

    // Arama filtresi: şahıs için (NAME/SURNAME/TCKNO), tüzel için (DEFINITION_/TAXNR)
    var q = _context.Customers.AsNoTracking()
        .Where(c =>
            string.IsNullOrEmpty(query) ||
            ((c.ISPERSCOMP == "1" && (
                (c.NAME ?? "").Contains(query) ||
                (c.SURNAME ?? "").Contains(query) ||
                (c.TCKNO ?? "").Contains(query)
            ))
            ||
            (c.ISPERSCOMP == "0" && (
                (c.DEFINITION_ ?? "").Contains(query) ||
                (c.TAXNR ?? "").Contains(query)
            ))
            ||
            // ISPERSCOMP boş/yanlışsa her ihtimale karşı geniş arama
            ((c.NAME ?? "").Contains(query) ||
             (c.SURNAME ?? "").Contains(query) ||
             (c.TCKNO ?? "").Contains(query) ||
             (c.DEFINITION_ ?? "").Contains(query) ||
             (c.TAXNR ?? "").Contains(query))

        ));
            

    // Sıralama / sınırlama
    if (string.IsNullOrEmpty(query))
        q = q.OrderByDescending(c => c.LOGICALREF).Take(defaultPageSize);
    else
        q = q.OrderBy(c =>
            c.ISPERSCOMP == "1"
                ? ((c.NAME ?? "") + " " + (c.SURNAME ?? ""))
                : (c.DEFINITION_ ?? "")
        );

    var customers = await q.Select(c => new
    {
        id = c.LOGICALREF,

        // Görünen isim: şahıs => Ad Soyad, tüzel => Ünvan (DEFINITION_)
        ad = c.ISPERSCOMP == "1"
                ? ((c.NAME ?? "") + " " + (c.SURNAME ?? "")).Trim()
                : (c.DEFINITION_ ?? "N/A"),

        // UI şablonunda var diye boş bırakıyoruz (tüzel için gerek yok)
        soyad = "",

        // Kimlik no: şahıs => TCKNO, tüzel => TAXNR
        kimlikNo = c.ISPERSCOMP == "1"
                ? (c.TCKNO ?? "N/A")
                : (c.TAXNR ?? "N/A"),

        // İstersen doğrudan kullanabileceğin birleşik gösterim
        display = c.ISPERSCOMP == "1"
                ? ($"{(c.NAME ?? "").Trim()} {(c.SURNAME ?? "").Trim()} ({(c.TCKNO ?? "N/A")})").Trim()
                : ($"{(c.DEFINITION_ ?? "N/A")} ({(c.TAXNR ?? "N/A")})")
    }).ToListAsync();
            

    return Json(customers);
}











        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRental([Bind("AracID,MusteriID,BaslangicTarihi,BitisTarihi,KiralamaSozlesmeleriDosyalari")] CreateRentalViewModel model)
        {
            try
            {
                // Tarihlerin parse edilmesi
                if (!DateOnly.TryParse(Request.Form["BaslangicTarihi"], out DateOnly startDate))
                {
                    ModelState.AddModelError("BaslangicTarihi", "Geçersiz başlangıç tarihi formatı.");
                }
                else
                {
                    model.BaslangicTarihi = startDate;
                }

                DateOnly? endDate = null;
                if (!string.IsNullOrEmpty(Request.Form["BitisTarihi"]))
                {
                    if (DateOnly.TryParse(Request.Form["BitisTarihi"], out DateOnly parsedEndDate))
                    {
                        endDate = parsedEndDate;
                    }
                    else
                    {
                        ModelState.AddModelError("BitisTarihi", "Geçersiz bitiş tarihi formatı.");
                    }
                }
                model.BitisTarihi = endDate;

                // Validasyonlar
                if (!model.AracID.HasValue || model.AracID.Value <= 0 || !await _context.Araclar.AnyAsync(a => a.AracID == model.AracID.Value))
                {
                    ModelState.AddModelError("AracID", "Lütfen geçerli bir araç seçin.");
                }

                if (!model.MusteriID.HasValue || model.MusteriID.Value <= 0 || !await _context.Customers.AnyAsync(m => m.LOGICALREF == model.MusteriID.Value))
                {
                    ModelState.AddModelError("MusteriID", "Lütfen geçerli bir müşteri seçin.");
                }

                if (model.BaslangicTarihi != default && endDate.HasValue && model.BaslangicTarihi > endDate)
                {
                    ModelState.AddModelError("BitisTarihi", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
                }

                // Çakışma kontrolü
                if (model.AracID.HasValue && model.AracID.Value > 0 && model.BaslangicTarihi != default)
                {
                    var overlappingRentals = await _context.Kiralamalar
                        .Where(r => r.AracID == model.AracID.Value &&
                                   (r.BaslangicTarihi <= (model.BitisTarihi ?? DateOnly.MaxValue) &&
                                    (r.BitisTarihi >= model.BaslangicTarihi || !r.BitisTarihi.HasValue)))
                        .ToListAsync();

                    if (overlappingRentals.Any())
                    {
                        ModelState.AddModelError("", "Bu araç bu tarihler aralığında zaten kiralanmış.");
                    }
                }

                if (ModelState.IsValid)
                {
                    var rental = new Rental
                    {
                        AracID = model.AracID,
                        MusteriID = model.MusteriID,
                        BaslangicTarihi = model.BaslangicTarihi,
                        BitisTarihi = model.BitisTarihi,
                        KayitTarihi = DateTime.Now
                    };

                    _context.Kiralamalar.Add(rental);
                    await _context.SaveChangesAsync();

                    var vehicleToUpdate = await _context.Araclar.FindAsync(model.AracID.Value);
                    if (vehicleToUpdate != null)
                    {
                        vehicleToUpdate.Durum = "Kiralanmış";
                        vehicleToUpdate.AktifMusteriID = model.MusteriID;
                        _context.Update(vehicleToUpdate);
                        await _context.SaveChangesAsync();
                    }

                    // Dosya yükleme
                    if (model.KiralamaSozlesmeleriDosyalari != null && model.KiralamaSozlesmeleriDosyalari.Any())
                    {
                        string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "rentals", "contracts");
                        Directory.CreateDirectory(uploadsFolder);

                        rental.RentalDocuments = rental.RentalDocuments ?? new List<RentalDocument>();

                        foreach (var file in model.KiralamaSozlesmeleriDosyalari)
                        {
                            if (file.Length > 0)
                            {
                                string uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                                using (var fileStream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(fileStream);
                                }

                                rental.RentalDocuments.Add(new RentalDocument
                                {
                                    RentalID = rental.KiralamaID,
                                    FilePath = $"/rentals/contracts/{uniqueFileName}",
                                    OriginalFileName = file.FileName,
                                    UploadDate = DateTime.Now
                                });
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                    rental.Durum = "Aktif";
                    await _context.SaveChangesAsync();
                    return Json(new
                    {
                        success = true,
                        message = "Kiralama başarıyla oluşturuldu.",
                        redirectUrl = Url.Action("Index", "Vehicles", new { activeTab = "rented", success = true })
                    });
                }

                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Formda hatalar var.", errors });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Hata oluştu: {ex.Message}",
                    errors = new Dictionary<string, string[]> { { "Genel", new[] { ex.Message } } }
                });
            }
        }


        [HttpPost]
        public async Task<IActionResult> UpdateRental(Rental model)
        {
            if (ModelState.IsValid)
            {
                var rental = await _context.Kiralamalar
                    .Include(r => r.Arac)
                    .FirstOrDefaultAsync(r => r.KiralamaID == model.KiralamaID);

                if (rental == null)
                {
                    return Json(new { success = false, message = "Kiralama bulunamadı." });
                }

                // Kiralama bitiş tarihini güncelle
                rental.BaslangicTarihi = model.BaslangicTarihi;
                rental.BitisTarihi = model.BitisTarihi;
                rental.KayitTarihi = DateTime.Now;
                rental.LokasyonID = model.LokasyonID;
                rental.Durum = model.Durum;

                // Kiralama bitiş kontrolü
                if (rental.BitisTarihi.HasValue && (rental.BitisTarihi.Value <= DateOnly.FromDateTime(DateTime.Today) || model.Durum == "Tamamlandı"))
                {
                    // Kiralama bittiğinde aracın AktifMusteriID'sini temizle
                    var vehicle = await _context.Araclar.FindAsync(rental.AracID);
                    if (vehicle != null && vehicle.AktifMusteriID == rental.MusteriID)
                    {
                        vehicle.AktifMusteriID = null;
                        vehicle.Durum = "Müsait"; 
                        await _context.SaveChangesAsync(); // Aracı güncelle
                    }
                }

                _context.Kiralamalar.Update(rental);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Kiralama başarıyla güncellendi." });
            }
            return Json(new { success = false, message = "Geçersiz veri.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
        }

        public async Task<IActionResult> ExportToExcel()
        {
            var vehicles = await _context.Araclar
                .Include(v => v.AktifMusteri)
                .Include(v => v.Kiralamalar)
                    .ThenInclude(r => r.Musteri)
                .Include(v => v.Cezalar)
                .Include(v => v.Lokasyon)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Araçlar ve Kiralamalar");

                int col = 1;
                worksheet.Cell(1, col++).Value = "Plaka";
                worksheet.Cell(1, col++).Value = "Şasi Numarası";
                worksheet.Cell(1, col++).Value = "Araç Açıklaması";
                worksheet.Cell(1, col++).Value = "Marka";
                worksheet.Cell(1, col++).Value = "Model";
                worksheet.Cell(1, col++).Value = "Model Yılı";
                worksheet.Cell(1, col++).Value = "Trafik Başlangıç";
                worksheet.Cell(1, col++).Value = "Trafik Bitiş";
                worksheet.Cell(1, col++).Value = "Kasko Başlangıç";
                worksheet.Cell(1, col++).Value = "Kasko Bitiş";
                worksheet.Cell(1, col++).Value = "MTV1 Ödendi";
                worksheet.Cell(1, col++).Value = "MTV2 Ödendi";
                worksheet.Cell(1, col++).Value = "Aktif Müşteri";
                worksheet.Cell(1, col++).Value = "Kiralamalar (Müşteri - Tarih Aralığı)";
                worksheet.Cell(1, col++).Value = "Tescil Tarihi";
                worksheet.Cell(1, col++).Value = "Lokasyon";
                worksheet.Cell(1, col++).Value = "Kiralama Bedeli";
                worksheet.Cell(1, col++).Value = "Araç Bedeli";
                worksheet.Cell(1, col++).Value = "Araç Alış Fiyatı";
                worksheet.Cell(1, col++).Value = "Hizmet Kodu";
                worksheet.Cell(1, col++).Value = "Yakıt Tipi";
                worksheet.Cell(1, col++).Value = "Vites Tipi";
                worksheet.Cell(1, col++).Value = "Kilometre";
                worksheet.Cell(1, col++).Value = "Çekiş Tipi";
                worksheet.Cell(1, col++).Value = "Renk";
                worksheet.Cell(1, col++).Value = "Motor Gücü";
                worksheet.Cell(1, col++).Value = "Araç Tipi";
                worksheet.Cell(1, col++).Value = "Kabis ID";
                worksheet.Cell(1, col++).Value = "Kabis Giriş Tarihi";
                worksheet.Cell(1, col++).Value = "Kabis Bitiş Tarihi";

                var headerRange = worksheet.Range($"A1:AD1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                int row = 2;
                foreach (var vehicle in vehicles)
                {
                    col = 1;
                    worksheet.Cell(row, col++).Value = vehicle.Plaka ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.SaseNo ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.Aciklama ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.Marka ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.Model ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.ModelYili;
                    worksheet.Cell(row, col++).Value = vehicle.TrafikBaslangicTarihi?.ToString("dd/MM/yyyy") ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.TrafikBitisTarihi?.ToString("dd/MM/yyyy") ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.KaskoBaslangicTarihi?.ToString("dd/MM/yyyy") ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.KaskoBitisTarihi?.ToString("dd/MM/yyyy") ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.MTV1Odendi ? "Evet" : "Hayır";
                    worksheet.Cell(row, col++).Value = vehicle.MTV2Odendi ? "Evet" : "Hayır";
                    worksheet.Cell(row, col++).Value = vehicle.AktifMusteri != null ? $"{vehicle.AktifMusteri.DEFINITION_} " : "Yok";


                    var rentals = vehicle.Kiralamalar.OrderBy(r => r.BaslangicTarihi).ToList();
                    string rentalDetails = string.Join("\n", rentals.Select(r =>
                        $"{r.Musteri?.DEFINITION_}  - {r.BaslangicTarihi.ToString("dd/MM/yyyy")} - {r.BitisTarihi?.ToString("dd/MM/yyyy") ?? "Devam Ediyor"}"));
                    worksheet.Cell(row, col++).Value = string.IsNullOrEmpty(rentalDetails) ? "Yok" : rentalDetails;
                    worksheet.Cell(row, col - 1).Style.Alignment.WrapText = true;

                    worksheet.Cell(row, col++).Value = vehicle.TescilTarihi?.ToString("dd/MM/yyyy") ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.Lokasyon != null ? $"{vehicle.Lokasyon.LokasyonAdi}" : "Belirtilmedi";
                    worksheet.Cell(row, col++).Value = vehicle.KiralamaBedeli;
                    worksheet.Cell(row, col++).Value = vehicle.AracBedeli;
                    worksheet.Cell(row, col++).Value = vehicle.AracAlisFiyati;
                    worksheet.Cell(row, col++).Value = vehicle.HizmetKodu ?? "N/A";
                    worksheet.Cell(row, col++).Value = vehicle.YakitTipi;
                    worksheet.Cell(row, col++).Value = vehicle.VitesTipi;
                    worksheet.Cell(row, col++).Value = vehicle.KMBilgi;
                    worksheet.Cell(row, col++).Value = vehicle.CekisTipi ?? "";
                    worksheet.Cell(row, col++).Value = vehicle.Renk ?? "";
                    worksheet.Cell(row, col++).Value = vehicle.MotorGucu ?? "";
                    worksheet.Cell(row, col++).Value = vehicle.AracTipiTanimi != null ? $"{vehicle.AracTipiTanimi.AracTipiName}" : "Belirtilmedi";
                    worksheet.Cell(row, col++).Value = vehicle.KabisID ?? "";
                    worksheet.Cell(row, col++).Value = vehicle.KabisGirisTarihi;
                    worksheet.Cell(row, col++).Value = vehicle.KabisBitisTarihi;

                    row++;
                }

                worksheet.Columns().AdjustToContents();
                worksheet.Column(13).Width = 40;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    string excelName = $"AracListesi-{DateTime.Now.ToString("yyyyMMddHHmmss")}.xlsx";
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImportFromExcel(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                return Json(new { success = false, message = "Lütfen bir Excel dosyası yükleyin." });
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var rows = worksheet.RowsUsed().Skip(1); // Başlık satırını atla
                        var errors = new List<string>();
                        int rowIndex = 2;

                        foreach (var row in rows)
                        {
                            string plaka = row.Cell(1).GetValue<string>()?.Trim() ?? string.Empty;
                            string saseNo = row.Cell(2).GetValue<string>()?.Trim() ?? "N/A";
                            string aciklama = row.Cell(3).GetValue<string>()?.Trim() ?? "N/A";
                            string marka = row.Cell(4).GetValue<string>()?.Trim() ?? string.Empty;
                            string model = row.Cell(5).GetValue<string>()?.Trim() ?? string.Empty;
                            int? modelYili = row.Cell(6).GetValue<int?>();
                            DateTime? trafikBaslangic = row.Cell(7).TryGetValue<DateTime>(out var val7) ? val7 : null;
                            DateTime? trafikBitis = row.Cell(8).TryGetValue<DateTime>(out var val8) ? val8 : null;
                            DateTime? kaskoBaslangic = row.Cell(9).TryGetValue<DateTime>(out var val9) ? val9 : null;
                            DateTime? kaskoBitis = row.Cell(10).TryGetValue<DateTime>(out var val10) ? val10 : null;
                            string mtv1OdendiStr = row.Cell(11).GetValue<string>()?.Trim()?.ToLower() ?? "hayır";
                            string mtv2OdendiStr = row.Cell(12).GetValue<string>()?.Trim()?.ToLower() ?? "hayır";
                            string aktifMusteri = row.Cell(13).GetValue<string>()?.Trim() ?? "Yok";
                            string kiralamalar = row.Cell(14).GetValue<string>()?.Trim() ?? "Yok";
                            DateTime? tescilTarihi = row.Cell(15).TryGetValue<DateTime>(out var val15) ? val15 : null;
                            string lokasyonAdi = row.Cell(16).GetValue<string>()?.Trim() ?? "Belirtilmedi";
                            decimal? kiralamaBedeli = row.Cell(17).GetValue<decimal?>();
                            decimal? aracBedeli = row.Cell(18).GetValue<decimal?>();
                            decimal? aracAlisFiyati = row.Cell(19).GetValue<decimal?>();
                            string hizmetKodu = row.Cell(20).GetValue<string>()?.Trim() ?? "N/A";
                            string yakitTipi = row.Cell(21).GetValue<string>()?.Trim() ?? null;
                            string vitesTipi = row.Cell(22).GetValue<string>()?.Trim() ?? null;
                            int? kmBilgi = row.Cell(23).GetValue<int?>();
                            string cekisTipi = row.Cell(24).GetValue<string>()?.Trim() ?? null;
                            string renk = row.Cell(25).GetValue<string>()?.Trim() ?? null;
                            string motorGucu = row.Cell(26).GetValue<string>()?.Trim() ?? null;
                            string aracTipiAdi = row.Cell(27).GetValue<string>()?.Trim() ?? null;
                            string kabisID = row.Cell(28).GetValue<string>()?.Trim() ?? null;
                            DateTime? kabisGirisTarihi = row.Cell(29).TryGetValue<DateTime>(out var val29) ? val29 : null;
                            DateTime? kabisBitisTarihi = row.Cell(30).TryGetValue<DateTime>(out var val30) ? val30 : null;

                            // Zorunlu alan kontrolleri
                            if (string.IsNullOrWhiteSpace(plaka))
                            {
                                errors.Add($"Satır {rowIndex}: Plaka zorunludur.");
                                continue;
                            }
                            if (string.IsNullOrWhiteSpace(marka))
                            {
                                errors.Add($"Satır {rowIndex}: Marka zorunludur.");
                                continue;
                            }
                            if (string.IsNullOrWhiteSpace(model))
                            {
                                errors.Add($"Satır {rowIndex}: Model zorunludur.");
                                continue;
                            }
                            if (!modelYili.HasValue)
                            {
                                errors.Add($"Satır {rowIndex}: Model Yılı zorunludur.");
                                continue;
                            }

                            // Lokasyon ID
                            int? lokasyonID = null;
                            if (!string.IsNullOrEmpty(lokasyonAdi) && lokasyonAdi != "Belirtilmedi")
                            {
                                var lokasyon = await _context.Lokasyonlar.FirstOrDefaultAsync(l => l.LokasyonAdi == lokasyonAdi);
                                if (lokasyon == null)
                                {
                                    var yeniLokasyon = new Lokasyon { LokasyonAdi = lokasyonAdi, Aciklama = "Excel’den eklendi" };
                                    _context.Lokasyonlar.Add(yeniLokasyon);
                                    await _context.SaveChangesAsync();
                                    lokasyonID = yeniLokasyon.LokasyonID;
                                }
                                else
                                {
                                    lokasyonID = lokasyon.LokasyonID;
                                }
                            }

                            // Araç Tipi ID
                            int? aracTipiID = null;
                            if (!string.IsNullOrEmpty(aracTipiAdi) && aracTipiAdi != "Belirtilmedi")
                            {
                                var aracTipi = await _context.AracTipiTanimi.FirstOrDefaultAsync(at => at.AracTipiName == aracTipiAdi);
                                if (aracTipi == null)
                                {
                                    var yeniAracTipi = new AracTipiTanimi { AracTipiName = aracTipiAdi, AracTipiAciklama = "Excel’den eklendi" };
                                    _context.AracTipiTanimi.Add(yeniAracTipi);
                                    await _context.SaveChangesAsync();
                                    aracTipiID = yeniAracTipi.AracTipiID;
                                }
                                else
                                {
                                    aracTipiID = aracTipi.AracTipiID;
                                }
                            }

                            // Aktif Müşteri ve Kiralamalar (şimdilik null veya varsayılan bırak)
                            int? aktifMusteriID = null; // Daha sonra müşteri eşleştirme eklenebilir
                            var kiralamaListesi = new List<Rental>(); // Kiralama bilgisi eklenmedi, "Yok" ise boş bırak

                            var vehicle = new Vehicle
                            {
                                Plaka = plaka,
                                SaseNo = saseNo,
                                Aciklama = aciklama,
                                Marka = marka,
                                Model = model,
                                ModelYili = modelYili.Value,
                                TrafikBaslangicTarihi = trafikBaslangic,
                                TrafikBitisTarihi = trafikBitis,
                                KaskoBaslangicTarihi = kaskoBaslangic,
                                KaskoBitisTarihi = kaskoBitis,
                                MTV1Odendi = mtv1OdendiStr == "evet",
                                MTV2Odendi = mtv2OdendiStr == "evet",
                                AktifMusteriID = aktifMusteriID,
                                Kiralamalar = kiralamaListesi,
                                TescilTarihi = tescilTarihi,
                                LokasyonID = lokasyonID,
                                KiralamaBedeli = kiralamaBedeli,
                                AracBedeli = aracBedeli,
                                AracAlisFiyati = aracAlisFiyati,
                                HizmetKodu = hizmetKodu,
                                YakitTipi = yakitTipi,
                                VitesTipi = vitesTipi,
                                KMBilgi = kmBilgi,
                                CekisTipi = cekisTipi,
                                Renk = renk,
                                MotorGucu = motorGucu,
                                AracTipiID = aracTipiID,
                                KabisID = kabisID,
                                KabisGirisTarihi = kabisGirisTarihi,
                                KabisBitisTarihi = kabisBitisTarihi
                            };

                            var existingVehicle = await _context.Araclar.FirstOrDefaultAsync(v => v.Plaka == vehicle.Plaka);
                            if (existingVehicle != null)
                            {
                                // Mevcut aracı güncelle
                                _context.Entry(existingVehicle).CurrentValues.SetValues(vehicle);
                            }
                            else
                            {
                                _context.Araclar.Add(vehicle);
                            }

                            rowIndex++;
                        }

                        await _context.SaveChangesAsync();
                        await UpdateActiveCustomersAsync();

                        if (errors.Any())
                        {
                            return Json(new { success = false, message = $"İçe aktarımda bazı hatalar oluştu: {string.Join("; ", errors)}", errors = errors });
                        }

                        return Json(new { success = true, message = "Araçlar başarıyla içe aktarıldı.", redirectUrl = Url.Action("Index", "Vehicles", new { success = true }) });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Import hatası: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"İçe aktarım hatası: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPenaltiesToExcel()
        {
            var penalties = await _context.Cezalar
                .Include(c => c.Arac)
                .Include(c => c.Musteri)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Tüm Cezalar");

                int col = 1;
                worksheet.Cell(1, col++).Value = "Ceza Tarihi";
                worksheet.Cell(1, col++).Value = "Araç Plakası";
                worksheet.Cell(1, col++).Value = "Müşteri Kimlik";
                worksheet.Cell(1, col++).Value = "Müşteri Adı";
                worksheet.Cell(1, col++).Value = "Müşteri Soyadı";
                worksheet.Cell(1, col++).Value = "Ücret";
                worksheet.Cell(1, col++).Value = "Adres";
                worksheet.Cell(1, col++).Value = "Açıklama";
                worksheet.Cell(1, col++).Value = "Ödendi";
                worksheet.Cell(1, col++).Value = "Ceza Belgesi";

                var headerRange = worksheet.Range($"A1:J1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                int row = 2;
                foreach (var penalty in penalties)
                {
                    col = 1;
                    worksheet.Cell(row, col++).Value = penalty.CezaTarihi.ToString("dd.MM.yyyy");
                    worksheet.Cell(row, col++).Value = penalty.Arac?.Plaka ?? "N/A";
                    worksheet.Cell(row, col++).Value = penalty.Musteri?.TCKNO ?? "N/A";
                    worksheet.Cell(row, col++).Value = penalty.Musteri?.DEFINITION_ ?? "N/A";
                    worksheet.Cell(row, col++).Value = penalty.Tutar.ToString("F2");
                    worksheet.Cell(row, col++).Value = penalty.CezaYeri ?? "N/A";
                    worksheet.Cell(row, col++).Value = penalty.Aciklama ?? "N/A";
                    worksheet.Cell(row, col++).Value = penalty.Odendi ? "Evet" : "Hayır";
                    worksheet.Cell(row, col++).Value = penalty.CezaBelgesi ?? "Belge Yok";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    string excelName = $"CezaListesi-{DateTime.Now.ToString("yyyyMMddHHmmss")}.xlsx";
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> Lokasyonlar()
        {
            var lokasyonlar = await _context.Lokasyonlar
                .Include(l => l.Araclar)
                .ToListAsync();

            var lokasyonViewModels = lokasyonlar.Select(l => new LokasyonViewModel
            {
                LokasyonID = l.LokasyonID,
                LokasyonAdi = l.LokasyonAdi,
                Aciklama = l.Aciklama,
                AracSayisi = l.Araclar?.Count ?? 0
            }).ToList();

            return View(lokasyonViewModels);
        }

        [HttpGet]
        public IActionResult GetAraclarByLokasyon(int lokasyonId)
        {
            var araclar = _context.Araclar
                .Where(a => a.LokasyonID == lokasyonId)
                .Select(a => new
                {
                    aracId = a.AracID, // İşlemler için AracID eklendi
                    plaka = a.Plaka,
                    marka = a.Marka,
                    model = a.Model,
                    yil = a.ModelYili
                })
                .ToList();

            return Json(araclar);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAracLokasyon(int aracId, int yeniLokasyonId)
        {
            var arac = await _context.Araclar.FindAsync(aracId);
            if (arac == null)
            {
                return NotFound();
            }

            arac.LokasyonID = yeniLokasyonId;
            _context.Update(arac);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // Aracı lokasyondan çıkaran endpoint
        [HttpPost]
        public async Task<IActionResult> RemoveAracFromLokasyon(int aracId)
        {
            var arac = await _context.Araclar.FindAsync(aracId);
            if (arac == null)
            {
                return NotFound();
            }

            arac.LokasyonID = null; // Lokasyonu null yaparak aracı lokasyondan çıkar
            _context.Update(arac);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet]
        public IActionResult CreateLokasyon()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLokasyon(Lokasyon lokasyon)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Aynı isimde lokasyon kontrolü
                    if (await _context.Lokasyonlar.AnyAsync(l => l.LokasyonAdi == lokasyon.LokasyonAdi))
                    {
                        ModelState.AddModelError("LokasyonAdi", "Bu isimde bir lokasyon zaten mevcut.");
                        return View(lokasyon);
                    }

                    lokasyon.Araclar = new List<Vehicle>();
                    _context.Add(lokasyon);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Lokasyonlar));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred while saving the location: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        ModelState.AddModelError("", $"Inner exception: {ex.InnerException.Message}");
                    }
                }
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            ViewBag.ErrorMessage = "Please correct the errors and try again. Errors: " + string.Join(", ", errors);
            return View(lokasyon);
        }

        [HttpGet]
        public async Task<IActionResult> EditLokasyon(int id)
        {
            var lokasyon = await _context.Lokasyonlar.FindAsync(id);
            if (lokasyon == null)
            {
                return NotFound();
            }
            return View(lokasyon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLokasyon(int id, Lokasyon lokasyon)
        {
            if (id != lokasyon.LokasyonID)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Aynı isimde başka bir lokasyon kontrolü
                    if (await _context.Lokasyonlar.AnyAsync(l => l.LokasyonAdi == lokasyon.LokasyonAdi && l.LokasyonID != lokasyon.LokasyonID))
                    {
                        ModelState.AddModelError("LokasyonAdi", "Bu isimde başka bir lokasyon zaten mevcut.");
                        return View(lokasyon);
                    }

                    var existingLokasyon = await _context.Lokasyonlar.FindAsync(id);
                    if (existingLokasyon == null)
                    {
                        return NotFound();
                    }

                    existingLokasyon.LokasyonAdi = lokasyon.LokasyonAdi;
                    existingLokasyon.Aciklama = lokasyon.Aciklama;

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Lokasyonlar));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred while updating the location: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        ModelState.AddModelError("", $"Inner exception: {ex.InnerException.Message}");
                    }
                }
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            ViewBag.ErrorMessage = "Please correct the errors and try again. Errors: " + string.Join(", ", errors);
            return View(lokasyon);
        }

        [HttpGet]
        public async Task<IActionResult> DeleteLokasyon(int id)
        {
            var lokasyon = await _context.Lokasyonlar
                .Include(l => l.Araclar)
                .FirstOrDefaultAsync(l => l.LokasyonID == id);

            if (lokasyon == null)
            {
                return NotFound();
            }

            return View(lokasyon);
        }

        [HttpPost, ActionName("DeleteLokasyon")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLokasyonConfirmed(int id)
        {
            var lokasyon = await _context.Lokasyonlar
                .Include(l => l.Araclar)
                .FirstOrDefaultAsync(l => l.LokasyonID == id);

            if (lokasyon == null)
            {
                return NotFound();
            }

            try
            {
                // Lokasyona bağlı araçların LokasyonID'sini null yap
                if (lokasyon.Araclar != null)
                {
                    foreach (var arac in lokasyon.Araclar)
                    {
                        arac.LokasyonID = null;
                    }
                }

                _context.Lokasyonlar.Remove(lokasyon);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Lokasyonlar));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while deleting the location: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", $"Inner exception: {ex.InnerException.Message}");
                }
                return View(lokasyon);
            }
        }



        [HttpGet]
        public IActionResult CreateTollPassFormPartial()
        {
            try
            {
                // Geçiş tarihi varsayılan olarak bugünün tarihi olsun
                var otoyolGecisi = new OtoyolGecisi
                {
                    GecisTarihi = DateTime.Today,
                    Odendi = false,
                    AracID = null,   // Açıkça null olarak ayarlandı
                    MusteriID = null // Açıkça null olarak ayarlandı
                };
                return PartialView("_CreateTollPassFormPartial", otoyolGecisi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateTollPassFormPartial: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new { success = false, message = $"Otoyol geçişi formu yüklenemedi: {ex.Message}" });
            }
        }

        // Güncellendi: AddNewHighwayPass POST metodu (CreateTollPass olarak yeniden adlandırıldı ve AJAX yanıtı döndürecek şekilde)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTollPass([Bind("AracID,MusteriID,GecisTarihi,Tutar,GecisYeri,Aciklama,Odendi")] OtoyolGecisi gecis, IFormFile? gecisBelgesiFile)
        {
            try
            {
                // AracID ve MusteriID için manuel validasyon
                if (!gecis.AracID.HasValue || gecis.AracID.Value <= 0)
                {
                    ModelState.AddModelError("AracID", "Lütfen geçerli bir araç seçin.");
                }
                if (!gecis.MusteriID.HasValue || gecis.MusteriID.Value <= 0)
                {
                    ModelState.AddModelError("MusteriID", "Lütfen geçerli bir müşteri seçin.");
                }

                // Geçiş Tarihi ile ilgili kiralama kontrolü
                if (gecis.AracID.HasValue && gecis.AracID.Value > 0 && gecis.GecisTarihi != default)
                {
                    var rental = await _context.Kiralamalar
                        .Where(r => r.AracID == gecis.AracID.Value &&
                                    r.BaslangicTarihi <= DateOnly.FromDateTime(gecis.GecisTarihi) &&
                                    (r.BitisTarihi == null || r.BitisTarihi >= DateOnly.FromDateTime(gecis.GecisTarihi)))
                        .OrderByDescending(r => r.BaslangicTarihi)
                        .FirstOrDefaultAsync();

                    if (rental == null)
                    {
                        ModelState.AddModelError("", "Seçilen geçiş tarihinde bu araca ait aktif bir kiralama bulunamadı.");
                    }
                    else
                    {
                        // Kiralama bulunduysa, gecis.MusteriID'yi kiralamadaki müşteri ID'si ile doldur
                        gecis.MusteriID = rental.MusteriID;
                    }
                }
                else if (gecis.AracID.HasValue && gecis.AracID.Value > 0 && gecis.GecisTarihi == default)
                {
                    ModelState.AddModelError("GecisTarihi", "Geçiş tarihi zorunludur.");
                }

                if (ModelState.IsValid)
                {
                    if (gecisBelgesiFile != null && gecisBelgesiFile.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "otoyol-gecisleri"); // Otoyol geçişleri için ayrı klasör
                        Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = $"gecis_{Guid.NewGuid()}_{Path.GetFileName(gecisBelgesiFile.FileName)}";
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await gecisBelgesiFile.CopyToAsync(stream);
                        }
                        gecis.GecisBelgesi = $"/uploads/otoyol-gecisleri/{uniqueFileName}";
                    }

                    _context.OtoyolGecisleri.Add(gecis);
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        message = "Otoyol geçişi başarıyla eklendi.",
                        redirectUrl = Url.Action("Index", "Vehicles", new { activeTab = "rented", success = true }) // Yönlendirme URL'si
                    });
                }

                // Model validasyon hatalarını JSON olarak döndür
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Formda hatalar var.", errors });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreateTollPass: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new
                {
                    success = false,
                    message = $"Hata oluştu: {ex.Message}",
                    errors = new Dictionary<string, string[]> { { "Genel", new[] { ex.Message } } }
                });
            }
        }
        [HttpGet]
        public async Task<IActionResult> EditHighwayPass(int id)
        {
            var pass = await _context.OtoyolGecisleri
                .Include(h => h.Arac)
                .Include(h => h.Musteri)
                .FirstOrDefaultAsync(h => h.GecisID == id);

            if (pass == null)
            {
                return NotFound();
            }

            return View(pass);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHighwayPass(int id, OtoyolGecisi pass, IFormFile? gecisBelgesi)
        {
            if (id != pass.GecisID)
            {
                return BadRequest();
            }

            // ModelState hatalarını kontrol et ve debug için yazdır
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Select(kvp => new { Field = kvp.Key, Errors = kvp.Value.Errors })
                    .Where(x => x.Errors.Any())
                    .Select(x => $"{x.Field}: {string.Join(", ", x.Errors.Select(e => e.ErrorMessage))}");
                ViewBag.ErrorMessage = "Doğrulama Hatası: " + string.Join("; ", errors);

                // Hata durumunda ilişkili verileri yükle
                var errorPass = await _context.OtoyolGecisleri
                    .Include(h => h.Arac)
                    .Include(h => h.Musteri)
                    .FirstOrDefaultAsync(h => h.GecisID == id);
                return View(errorPass);
            }

            try
            {
                var existingGecis = await _context.OtoyolGecisleri.FindAsync(id);
                if (existingGecis == null)
                {
                    return NotFound();
                }

                // Mevcut verileri güncelle (AracID ve MusteriID'yi değiştirmiyoruz)
                existingGecis.GecisTarihi = pass.GecisTarihi;
                existingGecis.Tutar = pass.Tutar;
                existingGecis.GecisYeri = pass.GecisYeri;
                existingGecis.Aciklama = pass.Aciklama;
                existingGecis.Odendi = pass.Odendi;

                // Yeni belge yükleme
                if (gecisBelgesi != null && gecisBelgesi.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }
                    if (!string.IsNullOrEmpty(existingGecis.GecisBelgesi))
                    {
                        string oldFilePath = Path.Combine(_environment.WebRootPath, existingGecis.GecisBelgesi.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }
                    string uniqueFileName = $"OtoyolGecis_{Guid.NewGuid()}_{gecisBelgesi.FileName}";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await gecisBelgesi.CopyToAsync(stream);
                    }
                    existingGecis.GecisBelgesi = $"/uploads/{uniqueFileName}";
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("AllPenalties");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Otoyol geçişi güncellenirken bir hata oluştu. Hata: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", $"İç hata: {ex.InnerException.Message}");
                }

                // Hata durumunda ilişkili verileri yükle
                var errorPass = await _context.OtoyolGecisleri
                    .Include(h => h.Arac)
                    .Include(h => h.Musteri)
                    .FirstOrDefaultAsync(h => h.GecisID == id);
                return View(errorPass);
            }
        }


        [HttpPost]
        public IActionResult UpdateActiveTab(string activeTab)
        {
            TempData["ActiveTab"] = activeTab; // TempData ile tab durumunu sakla
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> ExportHighwayPassesToExcel()
        {
            var highwayPasses = await _context.OtoyolGecisleri
                .Include(h => h.Arac)
                .Include(h => h.Musteri)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Tüm Otoyol Geçişleri");

                int col = 1;
                worksheet.Cell(1, col++).Value = "Geçiş Tarihi";
                worksheet.Cell(1, col++).Value = "Araç Plakası";
                worksheet.Cell(1, col++).Value = "Müşteri Kimlik";
                worksheet.Cell(1, col++).Value = "Müşteri Adı";
                worksheet.Cell(1, col++).Value = "Müşteri Soyadı";
                worksheet.Cell(1, col++).Value = "Ücret";
                worksheet.Cell(1, col++).Value = "Geçiş Yeri";
                worksheet.Cell(1, col++).Value = "Açıklama";
                worksheet.Cell(1, col++).Value = "Ödendi";
                worksheet.Cell(1, col++).Value = "Geçiş Belgesi";

                var headerRange = worksheet.Range($"A1:J1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                int row = 2;
                foreach (var pass in highwayPasses)
                {
                    col = 1;
                    worksheet.Cell(row, col++).Value = pass.GecisTarihi.ToString("dd.MM.yyyy");
                    worksheet.Cell(row, col++).Value = pass.Arac?.Plaka ?? "N/A";
                    worksheet.Cell(row, col++).Value = pass.Musteri?.TCKNO ?? "N/A";
                    worksheet.Cell(row, col++).Value = pass.Musteri?.DEFINITION_ ?? "N/A";
                    worksheet.Cell(row, col++).Value = pass.Tutar.ToString("F2");
                    worksheet.Cell(row, col++).Value = pass.GecisYeri ?? "N/A";
                    worksheet.Cell(row, col++).Value = pass.Aciklama ?? "N/A";
                    worksheet.Cell(row, col++).Value = pass.Odendi ? "Evet" : "Hayır";
                    worksheet.Cell(row, col++).Value = pass.GecisBelgesi ?? "Belge Yok";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    string excelName = $"OtoyolGecisListesi-{DateTime.Now.ToString("yyyyMMddHHmmss")}.xlsx";
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
                }

            }

        }

        // GET: Rentals/EditRental/5
        [HttpGet]
        public async Task<IActionResult> EditRental(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Kiralama detaylarını RentalDocuments ile birlikte çek
            var rental = await _context.Kiralamalar
                                       .Include(r => r.RentalDocuments)
                                       .FirstOrDefaultAsync(m => m.KiralamaID == id);

            if (rental == null)
            {
                return NotFound();
            }

            // Dropdown'lar için verileri hazırla (CreateRental'daki gibi)
            await PopulateViewBagForRentalCreateEdit();

            return View(rental);
        }

        // POST: Rentals/EditRental/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRental(int id,
            [Bind("KiralamaID,AracID,MusteriID,BaslangicTarihi,BitisTarihi")] Rental rental,
            List<IFormFile> NewKiralamaSozlesmeleriDosyalari) // Yeni eklenecek dosyalar için
        {
            if (id != rental.KiralamaID)
            {
                return NotFound();
            }

            // Mevcut kiralama objesini veritabanından çek (dosyaları dahil etmiyoruz, çünkü güncellenebilir değiliz, sadece yeni ekleyeceğiz)
            var existingRental = await _context.Kiralamalar
                                                .Include(r => r.RentalDocuments) // Mevcut belgeleri de çek
                                                .FirstOrDefaultAsync(r => r.KiralamaID == id);

            if (existingRental == null)
            {
                return NotFound();
            }

            // 1. Gelen verileri mevcut kiralama objesine aktar
            existingRental.AracID = rental.AracID;
            existingRental.MusteriID = rental.MusteriID;

            // Tarih formatlarını ele al (CreateRental'daki gibi)
            string startDateStr = Request.Form["BaslangicTarihi"];
            string endDateStr = Request.Form["BitisTarihi"];

            if (!DateOnly.TryParse(startDateStr, out DateOnly startDate))
            {
                ModelState.AddModelError("BaslangicTarihi", "Geçersiz başlangıç tarihi formatı.");
            }
            else
            {
                existingRental.BaslangicTarihi = startDate;
            }

            DateOnly? endDate = null;
            if (!string.IsNullOrEmpty(endDateStr))
            {
                if (DateOnly.TryParse(endDateStr, out DateOnly parsedEndDate))
                {
                    endDate = parsedEndDate;
                }
                else
                {
                    ModelState.AddModelError("BitisTarihi", "Geçersiz bitiş tarihi formatı.");
                }
            }
            existingRental.BitisTarihi = endDate;

            if (existingRental.BaslangicTarihi != default && endDate.HasValue && existingRental.BaslangicTarihi > endDate)
            {
                ModelState.AddModelError("BitisTarihi", "Bitiş tarihi başlangıç tarihinden önce olamaz.");
            }

            // Tarih ve araç/müşteri geçerliliği kontrolleri (CreateRental'daki gibi)
            if (existingRental.AracID <= 0 || await _context.Araclar.FindAsync(existingRental.AracID) == null)
            {
                ModelState.AddModelError("AracID", "Lütfen geçerli bir araç seçin.");
            }

            if (existingRental.MusteriID <= 0 || await _context.Customers.FindAsync(existingRental.MusteriID) == null)
            {
                ModelState.AddModelError("MusteriID", "Lütfen geçerli bir müşteri seçin.");
            }

            // Çakışma kontrolü (Güncellenen kiralama hariç diğer kiralamalarla kontrol et)
            if (existingRental.AracID > 0 && existingRental.BaslangicTarihi != default)
            {
                var overlappingRentals = await _context.Kiralamalar
                                                       .Where(r => r.AracID == existingRental.AracID)
                                                       .Where(r => r.KiralamaID != existingRental.KiralamaID) // Kendisi hariç!
                                                       .Where(r => r.BaslangicTarihi <= existingRental.BaslangicTarihi && (!r.BitisTarihi.HasValue || r.BitisTarihi >= existingRental.BaslangicTarihi)
                                                                || (existingRental.BitisTarihi.HasValue && r.BaslangicTarihi <= existingRental.BitisTarihi && (!r.BitisTarihi.HasValue || r.BitisTarihi >= existingRental.BitisTarihi)))
                                                       .ToListAsync();

                if (overlappingRentals.Any())
                {
                    await PopulateViewBagForRentalCreateEdit();
                    ViewBag.ErrorMessage = "Bu araç bu tarihler aralığında başka bir kiralamada bulunuyor.";
                    return View(existingRental);
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Kiralama detaylarını güncelle
                    _context.Update(existingRental);
                    await _context.SaveChangesAsync();

                    // Yeni Kiralama Sözleşmesi Dosyalarını Kaydetme Mantığı
                    if (NewKiralamaSozlesmeleriDosyalari != null && NewKiralamaSozlesmeleriDosyalari.Any())
                    {
                        string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "rentals", "contracts");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        // Eğer koleksiyon başlatılmamışsa başlat (yeni belgeler eklemek için)
                        if (existingRental.RentalDocuments == null)
                        {
                            existingRental.RentalDocuments = new List<RentalDocument>();
                        }

                        foreach (var file in NewKiralamaSozlesmeleriDosyalari)
                        {
                            if (file.Length > 0)
                            {
                                string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                                using (var fileStream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(fileStream);
                                }

                                // Yeni RentalDocument nesnesi oluştur ve mevcut kiralama objesinin koleksiyonuna ekle
                                existingRental.RentalDocuments.Add(new RentalDocument
                                {
                                    RentalID = existingRental.KiralamaID,
                                    FilePath = "/rentals/contracts/" + uniqueFileName, // Web'den erişilebilir yol
                                    OriginalFileName = file.FileName,
                                    UploadDate = DateTime.Now
                                });
                            }
                        }
                        await _context.SaveChangesAsync(); // Yeni eklenen RentalDocuments'ı kaydet
                    }

                    await UpdateActiveCustomersAsync(); // Müşteri aktivitesini güncellediğiniz metodunuz
                    return RedirectToAction(nameof(ActiveRentals)); // Başarılıysa kiralama listesine yönlendir
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await RentalExists(rental.KiralamaID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Kiralama güncellenirken bir hata oluştu: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        ModelState.AddModelError("", $"İç hata: {ex.InnerException.Message}");
                    }
                }
            }

            // Eğer ModelState geçerli değilse veya hata oluştuysa View'ı tekrar göster
            await PopulateViewBagForRentalCreateEdit();
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            ViewBag.ErrorMessage = "Lütfen hataları düzeltin ve tekrar deneyin. Hatalar: " + string.Join(", ", errors);
            return View(existingRental); // existingRental'ı geri gönderiyoruz ki formda mevcut veriler görünsün
        }

        private async Task<bool> RentalExists(int id)
        {
            return await _context.Kiralamalar.AnyAsync(e => e.KiralamaID == id);
        }

        // Yardımcı metot (daha düzenli kod için)
        private async Task PopulateViewBagForRentalCreateEdit()
        {
            var vehicles = await _context.Araclar.ToListAsync();
            var customers = await _context.Customers.ToListAsync();

            ViewBag.VehiclesJson = System.Text.Json.JsonSerializer.Serialize(vehicles.Select(v => new
            {
                id = v.AracID,
                text = $"{v.Marka} {v.Model} - {v.Plaka}"
            }));

            ViewBag.CustomersJson = System.Text.Json.JsonSerializer.Serialize(customers.Select(c => new
            {
                id = c.LOGICALREF,
                text = $"{c.DEFINITION_} - {c.TCKNO}"
            }));
        }
        public async Task<IActionResult> DeleteRental(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rental = await _context.Kiralamalar
                .Include(r => r.Arac)
                .Include(r => r.Musteri)
                .FirstOrDefaultAsync(m => m.KiralamaID == id);

            if (rental == null)
            {
                return NotFound();
            }

            // Eğer ihtiyaç olursa, View'e ek bilgiler gönderebiliriz
            // ViewBag.ErrorMessage = "Silmek istediğiniz kiralama bulunamadı.";
            // ViewBag.SuccessMessage = "Kiralama başarıyla silindi.";

            return View(rental);
        }

        // POST: Rentals/DeleteRental/5
        [HttpPost, ActionName("DeleteRental")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRentalConfirmed(int id)
        {
            var rental = await _context.Kiralamalar
                                       .Include(r => r.RentalDocuments) // Belgeleri de dahil et
                                       .FirstOrDefaultAsync(r => r.KiralamaID == id);

            if (rental == null)
            {
                // Kiralama bulunamazsa hata mesajı gösterilebilir veya NotFound döndürülebilir
                TempData["ErrorMessage"] = "Silinecek kiralama bulunamadı.";
                return RedirectToAction(nameof(ActiveRentals)); // Aktif kiralamalar sayfasına yönlendir
            }

            // 1. İlişkili belgeleri fiziksel olarak sil
            foreach (var doc in rental.RentalDocuments)
            {
                var filePath = Path.Combine(_hostEnvironment.WebRootPath, doc.FilePath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // 2. Belgeleri veritabanından sil (Parent kiralama silinince cascade delete ayarı varsa bu kısım gerekli olmayabilir,
            // ancak manuel silmek daha güvenli olabilir).
            _context.KiralamaSozlesmeleri.RemoveRange(rental.RentalDocuments);

            rental.Durum = "Müsait";
            _context.Kiralamalar.Remove(rental);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Kiralama başarıyla silindi.";
            return RedirectToAction(nameof(ActiveRentals)); // Silme sonrası aktif kiralamalar sayfasına yönlendir
        }

        // Eğer henüz eklemediyseniz, kiralamanın belgelerini silme metodunu da ekleyelim (önceki cevabımızda bahsetmiştik)
        [HttpPost]
        public async Task<IActionResult> DeleteRentalDocument(int id)
        {
            var document = await _context.KiralamaSozlesmeleri.FindAsync(id);
            if (document == null)
            {
                return Json(new { success = false, message = "Belge bulunamadı." });
            }

            try
            {
                var filePath = Path.Combine(_hostEnvironment.WebRootPath, document.FilePath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                _context.KiralamaSozlesmeleri.Remove(document);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ImportFromExcelPartial()
        {
            return PartialView("_ImportFromExcelPartial");
        }

    }







    // Lokasyonlar sayfasında kullanmak için bir ViewModel
    public class LokasyonViewModel
    {
        public int LokasyonID { get; set; }
        public string LokasyonAdi { get; set; }
        public string? Aciklama { get; set; }
        public int AracSayisi { get; set; }
    }
}

