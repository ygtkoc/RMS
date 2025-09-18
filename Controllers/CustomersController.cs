using ClosedXML.Excel;
using Dastone.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentalManagementSystem.Data; // DbContext'inizin bulunduğu namespace
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json; // JsonSerializer için
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dastone.Controllers;


namespace RentalManagementSystem.Controllers
{
    public class CustomersController : BaseController
    {
        private readonly RentalDbContext _context;

        public CustomersController(RentalDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string activeTab = "all", int pageNumber = 1, int pageSize = 10, string searchQuery = "")
        {
            var totalCustomersCount = await _context.Customers.CountAsync();
            var individualCustomersCount = await _context.Customers.CountAsync(c => c.ISPERSCOMP == "1"); // Şahsi müşteriler
            var corporateCustomersCount = await _context.Customers.CountAsync(c => c.ISPERSCOMP == "0" || c.ISPERSCOMP == null); // Tüzel müşteriler

            var model = new CustomersDashboardViewModel
            {
                ActiveTab = activeTab,
                TotalCustomersCount = totalCustomersCount,
                IndividualCustomersCount = individualCustomersCount,
                CorporateCustomersCount = corporateCustomersCount,
                AllCustomersPartial = await GetCustomerListPartialViewModel(null, pageNumber, pageSize, searchQuery), // Tüm müşteriler için null
                IndividualCustomersPartial = await GetCustomerListPartialViewModel(true, pageNumber, pageSize, searchQuery),
                CorporateCustomersPartial = await GetCustomerListPartialViewModel(false, pageNumber, pageSize, searchQuery)
            };

            return View(model);
        }

        // AJAX: Müşteri listesi için partial view döndürür (tablar için)
        [HttpGet]
        public async Task<IActionResult> GetCustomersForTab(bool? customerType, int pageNumber = 1, int pageSize = 10, string searchQuery = "")
        {
            var customerListViewModel = await GetCustomerListPartialViewModel(customerType, pageNumber, pageSize, searchQuery);
            return PartialView("_CustomerListPartial", customerListViewModel);
        }

        private async Task<CustomerListPartialViewModel> GetCustomerListPartialViewModel(bool? customerType, int pageNumber, int pageSize, string searchQuery)
        {
            IQueryable<Customer> customersQuery = _context.Customers.AsNoTracking();

            searchQuery = searchQuery?.Trim() ?? string.Empty;
            var tabId = "allCustomers";

            // Müşteri türüne göre filtreleme
            if (customerType.HasValue)
            {
                if (customerType.Value)
                {
                    customersQuery = customersQuery.Where(c => c.ISPERSCOMP == "1"); // Şahsi - sadece 1 olanlar
                    tabId = "individualCustomers";
                }
                else
                {
                    customersQuery = customersQuery.Where(c => c.ISPERSCOMP == "0"); // Tüzel - sadece 0 olanlar
                    tabId = "corporateCustomers";
                }
            }

            // Arama sorgusuna göre filtreleme
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                customersQuery = customersQuery.Where(c =>
                    (c.DEFINITION_ != null && c.DEFINITION_.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (c.TCKNO != null && c.TCKNO.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (c.TAXNR != null && c.TAXNR.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (c.CELLPHONE != null && c.CELLPHONE.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (c.EMAILADDR != null && c.EMAILADDR.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (c.NAME != null && c.NAME.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (c.SURNAME != null && c.SURNAME.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                    (c.ADDR1 != null && c.ADDR1.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                );
            }

            var totalCount = await customersQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            Console.WriteLine($"Toplam müşteri sayısı: {totalCount}, Sayfa: {pageNumber}, Boyut: {pageSize}");

            // Sorguyu yazdır
            var sql = customersQuery.ToQueryString();
            Console.WriteLine($"Çalıştırılan SQL: {sql}");

            var customers = await customersQuery
                .Select(c => new Customer // Yeni bir Customer nesnesi oluştur
                {
                    LOGICALREF = c.LOGICALREF, // Tür dönüşümünü güvenli yap
                    ISPERSCOMP = c.ISPERSCOMP, // String olarak kalacak
                    DEFINITION_ = c.DEFINITION_,
                    TCKNO = c.TCKNO,
                    TAXNR = c.TAXNR,
                    TAXOFFICE = c.TAXOFFICE,
                    CELLPHONE = c.CELLPHONE,
                    EMAILADDR = c.EMAILADDR,
                    NAME = c.NAME,
                    SURNAME = c.SURNAME,
                    ADDR1 = c.ADDR1
                })
                .OrderBy(c => c.DEFINITION_)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Debug için log ekle
            foreach (var customer in customers)
            {
                Console.WriteLine($"Müşteri LOGICALREF: {customer.LOGICALREF}, ISPERSCOMP: {customer.ISPERSCOMP}, DEFINITION_: {customer.DEFINITION_}");
            }

            return new CustomerListPartialViewModel
            {
                Customers = customers,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                SearchQuery = searchQuery,
                CustomerType = customerType,
                TabId = tabId
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // Güvenlik için
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return Json(new { success = false, message = "Müşteri bulunamadı." });
            }

            // Opsiyonel: İlişkili verileri kontrol et (silmeden önce)
            if (customer.Kiralamalar.Any() || customer.Cezalar.Any() /* vb. */)
            {
                return Json(new { success = false, message = "Müşterinin ilişkili kayıtları var, silinemez." });
            }

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Müşteri başarıyla silindi." });
        }

        // GET: Customers/CustomerProfile
        public async Task<IActionResult> CustomerProfile(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Kiralamalar)
                    .ThenInclude(r => r.Arac)
                .Include(c => c.Cezalar)
                    .ThenInclude(p => p.Arac)
                .FirstOrDefaultAsync(c => c.LOGICALREF == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        [HttpGet]
        public IActionResult CreateCustomerFormPartial()
        {
            return PartialView("_CreateCustomerFormPartial", new Customer { ISPERSCOMP = "true" }); // Varsayılan olarak Şahsi
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new Customer
            {
                ACTIVE = true, // Varsayılan olarak aktif
                CODE = GenerateCustomerCode(), // Örnek müşteri kodu
                ISPERSCOMP = "1" // Varsayılan olarak Şahsi
            };
            return PartialView("_CreateCustomerFormPartial", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model)
        {
            if (!ModelState.IsValid)
            {
                return PartialView("_CreateCustomerFormPartial", model);
            }

            // Model validation için ek kontroller
            if (model.IsPersonalCompany == true)
            {
                if (string.IsNullOrEmpty(model.TCKNO) || model.TCKNO.Length != 11 || !model.TCKNO.All(char.IsDigit))
                {
                    ModelState.AddModelError("TCKNO", "TC Kimlik No 11 haneli ve sadece rakamlardan oluşmalıdır.");
                    return PartialView("_CreateCustomerFormPartial", model);
                }
                if (string.IsNullOrEmpty(model.NAME) || string.IsNullOrEmpty(model.SURNAME))
                {
                    ModelState.AddModelError("", "Ad ve soyad zorunludur.");
                    return PartialView("_CreateCustomerFormPartial", model);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(model.TAXNR) || model.TAXNR.Length != 10 || !model.TAXNR.All(char.IsDigit))
                {
                    ModelState.AddModelError("TAXNR", "Vergi No 10 haneli ve sadece rakamlardan oluşmalıdır.");
                    return PartialView("_CreateCustomerFormPartial", model);
                }
                if (string.IsNullOrEmpty(model.TAXOFFICE) || string.IsNullOrEmpty(model.NAME))
                {
                    ModelState.AddModelError("", "Unvan ve vergi dairesi zorunludur.");
                    return PartialView("_CreateCustomerFormPartial", model);
                }
            }

            // Telefon numarasının format kontrolü
            if (!string.IsNullOrEmpty(model.CELLPHONE) && !System.Text.RegularExpressions.Regex.IsMatch(model.CELLPHONE, @"^[0-9]{10,20}$"))
            {
                ModelState.AddModelError("CELLPHONE", "Telefon numarası 10-20 haneli ve sadece rakamlardan oluşmalıdır.");
                return PartialView("_CreateCustomerFormPartial", model);
            }

            // E-posta format kontrolü
            if (!string.IsNullOrEmpty(model.EMAILADDR) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.EMAILADDR))
            {
                ModelState.AddModelError("EMAILADDR", "Geçerli bir e-posta adresi giriniz.");
                return PartialView("_CreateCustomerFormPartial", model);
            }
            if (!string.IsNullOrEmpty(model.EMAILADDR2) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.EMAILADDR2))
            {
                ModelState.AddModelError("EMAILADDR2", "Geçerli bir e-posta adresi giriniz.");
                return PartialView("_CreateCustomerFormPartial", model);
            }

            // Otomatik doldurulacak alanlar           
            model.CAPIBLOCK_CREADEDDATE = DateTime.Now; // Kayıt tarihi
            model.LOGICALREF = await GenerateLogicalRefAsync(); // Örnek LOGICALREF

            // ISPERSCOMP string olarak kaydediliyor
            model.ISPERSCOMP = model.IsPersonalCompany == true ? "1" : "0";

            // Yeni müşteri kaydı
            _context.Customers.Add(model); // Tablo adı "Customers" olarak güncellendi
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Müşteri başarıyla oluşturuldu." });
        }

        private string GenerateCustomerCode()
        {
            // Örnek müşteri kodu oluşturma mantığı (örneğin, Müşteri1, Müşteri2 gibi)
            var lastCustomer = _context.Customers.OrderByDescending(c => c.CODE).FirstOrDefault();
            int codeNumber = lastCustomer != null ? int.Parse(Regex.Match(lastCustomer.CODE, @"\d+").Value) + 1 : 1;
            return $"Müşteri{codeNumber}";
        }

        private async Task<int> GenerateLogicalRefAsync()
        {
            // DbSet tutarlılığı için Musteriler kullanıldı ve DB'de max değeri direkt alınır.
            var maxLogicalRef = await _context.Customers.MaxAsync(c => (int?)c.LOGICALREF) ?? 0;
            return maxLogicalRef + 1;
        }

        // GET: Customers/EditCustomerFormPartial - Müşteri Düzenleme Formu (Offcanvas için)
        [HttpGet]
        public async Task<IActionResult> EditCustomerFormPartial(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var customerTypeList = new List<SelectListItem>
    {
        new SelectListItem { Value = "true", Text = "Şahsi", Selected = customer.IsPersonalCompany.HasValue && customer.IsPersonalCompany.Value },
        new SelectListItem { Value = "false", Text = "Tüzel", Selected = customer.IsPersonalCompany.HasValue && !customer.IsPersonalCompany.Value }
    };
            ViewData["CustomerTypeList"] = new SelectList(customerTypeList, "Value", "Text");

            return PartialView("_EditCustomerFormPartial", customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.LOGICALREF)
                return Json(new { success = false, message = "Geçersiz müşteri ID'si." });

            var customerTypeList = new List<SelectListItem>
    {
        new SelectListItem { Value = "true", Text = "Şahsi", Selected = customer.IsPersonalCompany.HasValue && customer.IsPersonalCompany.Value },
        new SelectListItem { Value = "false", Text = "Tüzel", Selected = customer.IsPersonalCompany.HasValue && !customer.IsPersonalCompany.Value }
    };
            ViewData["CustomerTypeList"] = new SelectList(customerTypeList, "Value", "Text");

            if (customer.IsPersonalCompany == false)
            {
                ModelState.Remove("TCKNO");
                customer.TCKNO = string.Empty;
            }
            else if (customer.IsPersonalCompany == true)
            {
                ModelState.Remove("TAXNR");
                ModelState.Remove("TAXOFFICE");
                customer.TAXNR = null;
                customer.TAXOFFICE = null;
            }

            if (string.IsNullOrEmpty(customer.DEFINITION_)) ModelState.AddModelError("DEFINITION_", "Ad zorunludur.");
            if (string.IsNullOrEmpty(customer.CELLPHONE)) ModelState.AddModelError("CELLPHONE", "Telefon numarası zorunludur.");
            if (string.IsNullOrEmpty(customer.EMAILADDR)) ModelState.AddModelError("EMAILADDR", "E-posta adresi zorunludur.");

            if (customer.IsPersonalCompany == true)
            {
                if (string.IsNullOrEmpty(customer.TCKNO))
                    ModelState.AddModelError("TCKNO", "Kimlik numarası zorunludur.");
                else if (customer.TCKNO.Length != 11 || !Regex.IsMatch(customer.TCKNO, "^[0-9]*$"))
                    ModelState.AddModelError("TCKNO", "Kimlik numarası tam 11 rakam olmalıdır.");
            }
            else if (customer.IsPersonalCompany == false)
            {
                if (string.IsNullOrEmpty(customer.TAXNR))
                    ModelState.AddModelError("TAXNR", "Vergi numarası zorunludur.");
                else if (customer.TAXNR.Length != 10 || !Regex.IsMatch(customer.TAXNR, "^[0-9]*$"))
                    ModelState.AddModelError("TAXNR", "Vergi numarası tam 10 rakam olmalıdır.");
                if (string.IsNullOrEmpty(customer.TAXOFFICE))
                    ModelState.AddModelError("TAXOFFICE", "Vergi dairesi zorunludur.");
                else if (customer.TAXOFFICE.Length > 100)
                    ModelState.AddModelError("TAXOFFICE", "Vergi dairesi 100 karakterden uzun olamaz.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                return Json(new { success = false, message = "Formda hatalar var.", errors });
            }

            try
            {
                var existingCustomer = await _context.Customers.FindAsync(id);
                if (existingCustomer == null)
                    return Json(new { success = false, message = "Müşteri bulunamadı." });

                var originalTCKNO = existingCustomer.TCKNO;
                var originalTAXNR = existingCustomer.TAXNR;

                existingCustomer.ISPERSCOMP = customer.IsPersonalCompany.HasValue ? (customer.IsPersonalCompany.Value ? "1" : "0") : null;

                if (customer.IsPersonalCompany == true)
                {
                    existingCustomer.TCKNO = customer.TCKNO;
                    existingCustomer.TAXNR = null;
                    existingCustomer.TAXOFFICE = null;

                    if (!string.IsNullOrEmpty(customer.TCKNO) && customer.TCKNO != originalTCKNO)
                    {
                        if (await _context.Customers.AnyAsync(c => c.TCKNO == customer.TCKNO && c.LOGICALREF != customer.LOGICALREF))
                        {
                            ModelState.AddModelError("TCKNO", "Bu kimlik numarasına sahip bir müşteri zaten mevcut.");
                            var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                            return Json(new { success = false, message = "Formda hatalar var.", errors });
                        }
                    }
                }
                else if (customer.IsPersonalCompany == false)
                {
                    existingCustomer.TAXNR = customer.TAXNR;
                    existingCustomer.TAXOFFICE = customer.TAXOFFICE;
                    existingCustomer.TCKNO = string.Empty;

                    if (!string.IsNullOrEmpty(customer.TAXNR) && customer.TAXNR != originalTAXNR)
                    {
                        if (await _context.Customers.AnyAsync(c => c.TAXNR == customer.TAXNR && c.LOGICALREF != customer.LOGICALREF))
                        {
                            ModelState.AddModelError("TAXNR", "Bu vergi numarasına sahip bir müşteri zaten mevcut.");
                            var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                            return Json(new { success = false, message = "Formda hatalar var.", errors });
                        }
                    }
                }

                existingCustomer.DEFINITION_ = customer.DEFINITION_;
                existingCustomer.EMAILADDR = customer.EMAILADDR;
                existingCustomer.CELLPHONE = customer.CELLPHONE;
                existingCustomer.EMAILADDR2 = customer.EMAILADDR2;
                existingCustomer.ADDR1 = customer.ADDR1;

                _context.Update(existingCustomer);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Müşteri başarıyla güncellendi.", redirectUrl = Url.Action("CustomerProfile", new { id = customer.LOGICALREF, success = true }) });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Edit Customer: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Müşteri güncellenirken bir hata oluştu: {ex.Message}", errors = new Dictionary<string, string[]> { { "Genel", new[] { ex.Message } } } });
            }
        }

        // GET: Customers/DeleteCustomerConfirmPartial - Müşteri Silme Onayı (Offcanvas için)
        [HttpGet]
        public async Task<IActionResult> DeleteCustomerConfirmPartial(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            return PartialView("_DeleteCustomerConfirmPartial", customer);
        }

        // POST: Customers/Delete - Müşteri Silme (AJAX yanıtı döndürecek şekilde)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return Json(new { success = false, message = "Müşteri bulunamadı." });
            }

            try
            {
                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    message = "Müşteri başarıyla silindi.",
                    redirectUrl = Url.Action("Index", "Customers", new { success = true })
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Delete Customer: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new
                {
                    success = false,
                    message = $"Müşteri silinirken bir hata oluştu: {ex.Message}",
                    errors = new Dictionary<string, string[]> { { "Genel", new[] { ex.Message } } }
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerDetailsPartial(int id)
        {
            try
            {
                var customerData = await _context.Customers
                    .AsNoTracking()
                    .Where(c => c.LOGICALREF == id)
                    .Select(c => new
                    {
                        c.LOGICALREF,
                        c.ISPERSCOMP,
                        c.DEFINITION_,
                        c.NAME,
                        c.SURNAME,
                        c.TCKNO,
                        c.TAXNR,
                        c.TAXOFFICE,
                        c.CELLPHONE,
                        c.EMAILADDR,
                        c.EMAILADDR2,
                        c.EMAILADDR3,
                        c.ADDR1,
                        c.CAPIBLOCK_CREADEDDATE,
                        Kiralamalar = c.Kiralamalar
                            .OrderBy(k => k.BaslangicTarihi)
                            .Select(k => new
                            {
                                k.KiralamaID,
                                k.BaslangicTarihi,
                                k.BitisTarihi,
                                k.Durum,
                                Arac = k.Arac == null ? null : new
                                {
                                    k.Arac.AracID,
                                    k.Arac.Plaka
                                },
                                Lokasyon = k.Lokasyon == null ? null : new
                                {
                                    k.Lokasyon.LokasyonID,
                                    k.Lokasyon.LokasyonAdi
                                }
                            }),
                        Araclar = c.Araclar
                            .Select(a => new
                            {
                                a.AracID,
                                a.Plaka,
                                a.Marka,
                                a.Model,
                                a.ModelYili,
                                a.Durum
                            }),
                        Cezalar = c.Cezalar
                            .OrderBy(z => z.CezaTarihi)
                            .Select(z => new
                            {
                                z.CezaID,
                                z.CezaTarihi,
                                z.Tutar,
                                z.Aciklama,
                                z.Odendi,
                                CezaTanimi = z.CezaTanimi == null ? null : new
                                {
                                    z.CezaTanimi.CezaTanimiID,
                                    z.CezaTanimi.KisaAciklama
                                }
                            }),
                        OtoyolGecisleri = c.OtoyolGecisleri
                            .OrderBy(g => g.GecisTarihi)
                            .Select(g => new
                            {
                                g.GecisID,
                                g.GecisTarihi,
                                g.Tutar,
                                g.Aciklama,
                                Lokasyon = g.Lokasyon == null ? null : new
                                {
                                    g.Lokasyon.LokasyonID,
                                    g.Lokasyon.LokasyonAdi
                                }
                            })
                    })
                    .FirstOrDefaultAsync();

                if (customerData == null)
                {
                    return NotFound();
                }

                var customer = new Customer
                {
                    LOGICALREF = customerData.LOGICALREF,
                    ISPERSCOMP = customerData.ISPERSCOMP,
                    DEFINITION_ = customerData.DEFINITION_,
                    NAME = customerData.NAME,
                    SURNAME = customerData.SURNAME,
                    TCKNO = customerData.TCKNO,
                    TAXNR = customerData.TAXNR,
                    TAXOFFICE = customerData.TAXOFFICE,
                    CELLPHONE = customerData.CELLPHONE,
                    EMAILADDR = customerData.EMAILADDR,
                    EMAILADDR2 = customerData.EMAILADDR2,
                    EMAILADDR3 = customerData.EMAILADDR3,
                    ADDR1 = customerData.ADDR1,
                    CAPIBLOCK_CREADEDDATE = customerData.CAPIBLOCK_CREADEDDATE,
                    Kiralamalar = customerData.Kiralamalar?
                        .Select(k => new Rental
                        {
                            KiralamaID = k.KiralamaID,
                            BaslangicTarihi = k.BaslangicTarihi,
                            BitisTarihi = k.BitisTarihi,
                            Durum = k.Durum,
                            Arac = k.Arac == null ? null : new Vehicle
                            {
                                AracID = k.Arac.AracID,
                                Plaka = k.Arac.Plaka
                            },
                            Lokasyon = k.Lokasyon == null ? null : new Lokasyon
                            {
                                LokasyonID = k.Lokasyon.LokasyonID,
                                LokasyonAdi = k.Lokasyon.LokasyonAdi
                            }
                        })
                        .ToList() ?? new List<Rental>(),
                    Araclar = customerData.Araclar?
                        .Select(a => new Vehicle
                        {
                            AracID = a.AracID,
                            Plaka = a.Plaka,
                            Marka = a.Marka,
                            Model = a.Model,
                            ModelYili = a.ModelYili,
                            Durum = a.Durum
                        })
                        .ToList() ?? new List<Vehicle>(),
                    Cezalar = customerData.Cezalar?
                        .Select(z => new Ceza
                        {
                            CezaID = z.CezaID,
                            CezaTarihi = z.CezaTarihi,
                            Tutar = z.Tutar,
                            Aciklama = z.Aciklama,
                            Odendi = z.Odendi,
                            CezaTanimi = z.CezaTanimi == null ? null : new CezaTanimi
                            {
                                CezaTanimiID = z.CezaTanimi.CezaTanimiID,
                                KisaAciklama = z.CezaTanimi.KisaAciklama
                            }
                        })
                        .ToList() ?? new List<Ceza>(),
                    OtoyolGecisleri = customerData.OtoyolGecisleri?
                        .Select(g => new OtoyolGecisi
                        {
                            GecisID = g.GecisID,
                            GecisTarihi = g.GecisTarihi,
                            Tutar = g.Tutar,
                            Aciklama = g.Aciklama,
                            Lokasyon = g.Lokasyon == null ? null : new Lokasyon
                            {
                                LokasyonID = g.Lokasyon.LokasyonID,
                                LokasyonAdi = g.Lokasyon.LokasyonAdi
                            }
                        })
                        .ToList() ?? new List<OtoyolGecisi>()
                };

                return PartialView("_CustomerDetailsModalContentPartial", customer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, stackTrace = ex.StackTrace });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCustomerDetails(int id, [FromForm] Customer model)
        {
            if (id != model.LOGICALREF)
            {
                return BadRequest(new { success = false, message = "Geçersiz ID." });
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound(new { success = false, message = "Müşteri bulunamadı." });
            }

            // Koşullu validation: Gereksiz field'ların hatalarını temizle
            if (model.IsPersonalCompany.HasValue && !model.IsPersonalCompany.Value)
            {
                ModelState.Remove("NAME");
                ModelState.Remove("SURNAME");
                ModelState.Remove("TCKNO");
                if (string.IsNullOrEmpty(model.TAXNR))
                {
                    ModelState.AddModelError("TAXNR", "Vergi No zorunludur.");
                }
            }
            else if (model.IsPersonalCompany.HasValue && model.IsPersonalCompany.Value)
            {
                ModelState.Remove("TAXNR");
                ModelState.Remove("TAXOFFICE");
                if (string.IsNullOrEmpty(model.TCKNO))
                {
                    ModelState.AddModelError("TCKNO", "Kimlik No zorunludur.");
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );
                return Json(new { success = false, message = "Geçersiz bilgiler. Lütfen hataları düzeltin.", errors = errors });
            }

            // Güncellemeleri uygula
            customer.DEFINITION_ = model.DEFINITION_;
            customer.NAME = model.IsPersonalCompany.HasValue && model.IsPersonalCompany.Value ? model.NAME : null;
            customer.SURNAME = model.IsPersonalCompany.HasValue && model.IsPersonalCompany.Value ? model.SURNAME : null;
            customer.TCKNO = model.IsPersonalCompany.HasValue && model.IsPersonalCompany.Value ? model.TCKNO : null;
            customer.TAXNR = model.IsPersonalCompany.HasValue && !model.IsPersonalCompany.Value ? model.TAXNR : null;
            customer.TAXOFFICE = model.IsPersonalCompany.HasValue && !model.IsPersonalCompany.Value ? model.TAXOFFICE : null;
            customer.EMAILADDR = model.EMAILADDR;
            customer.EMAILADDR2 = model.EMAILADDR2;
            customer.EMAILADDR3 = model.EMAILADDR3;
            customer.CELLPHONE = model.CELLPHONE;
            customer.ADDR1 = model.ADDR1;
            customer.ISPERSCOMP = model.ISPERSCOMP;

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Müşteri başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Veritabanı hatası: " + ex.Message });
            }
        }

        // GET: Customers/ExportToExcel
        public async Task<IActionResult> ExportToExcel()
        {
            var customers = await _context.Customers
                .Include(c => c.Kiralamalar)
                    .ThenInclude(r => r.Arac)
                    .ThenInclude(a => a.Lokasyon)
                .Include(c => c.Cezalar)
                .Include(c => c.OtoyolGecisleri)
                .ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Musteriler ve Kiralamalar");

                int col = 1;
                worksheet.Cell(1, col++).Value = "Müşteri Türü";
                worksheet.Cell(1, col++).Value = "Kimlik Numarası";
                worksheet.Cell(1, col++).Value = "Vergi Numarası";
                worksheet.Cell(1, col++).Value = "Vergi Dairesi";
                worksheet.Cell(1, col++).Value = "Adı";
                worksheet.Cell(1, col++).Value = "Soyadı";
                worksheet.Cell(1, col++).Value = "Unvanı";
                worksheet.Cell(1, col++).Value = "Email";
                worksheet.Cell(1, col++).Value = "Email 2";
                worksheet.Cell(1, col++).Value = "Email 3";
                worksheet.Cell(1, col++).Value = "Telefon";
                worksheet.Cell(1, col++).Value = "Adres";
                worksheet.Cell(1, col++).Value = "Kiralamalar (Araç - Tarih Aralığı)";
                worksheet.Cell(1, col++).Value = "Cezalar (Tutar - Tarih)";
                worksheet.Cell(1, col++).Value = "Otoyol Geçişleri (Tutar - Tarih)";

                var headerRange = worksheet.Range($"A1:O1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                int row = 2;
                foreach (var customer in customers)
                {
                    col = 1;
                    worksheet.Cell(row, col++).Value = customer.IsPersonalCompany.HasValue ? (customer.IsPersonalCompany.Value ? "Şahsi" : "Tüzel") : "Bilinmiyor";
                    worksheet.Cell(row, col++).Value = customer.TCKNO ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.TAXNR ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.TAXOFFICE ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.NAME ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.SURNAME ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.DEFINITION_ ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.EMAILADDR ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.EMAILADDR2 ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.EMAILADDR3 ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.CELLPHONE ?? "N/A";
                    worksheet.Cell(row, col++).Value = customer.ADDR1 ?? "N/A";

                    var rentals = customer.Kiralamalar.OrderBy(r => r.BaslangicTarihi).ToList();
                    string rentalDetails = string.Join("\n", rentals.Select(r =>
                        $"{r.Arac?.Plaka} - {r.BaslangicTarihi.ToString("dd/MM/yyyy")} - {r.BitisTarihi?.ToString("dd/MM/yyyy") ?? "Devam Ediyor"} ({r.Lokasyon?.LokasyonAdi ?? "Belirsiz"})"));
                    worksheet.Cell(row, col++).Value = string.IsNullOrEmpty(rentalDetails) ? "Yok" : rentalDetails;
                    worksheet.Cell(row, col - 1).Style.Alignment.WrapText = true;

                    var penalties = customer.Cezalar.OrderBy(p => p.CezaTarihi).ToList();
                    string penaltyDetails = string.Join("\n", penalties.Select(p =>
                        $"{p.Tutar} TL - {p.CezaTarihi.ToString("dd/MM/yyyy")} ({p.CezaTanimi?.KisaAciklama ?? "Belirsiz"})"));
                    worksheet.Cell(row, col++).Value = string.IsNullOrEmpty(penaltyDetails) ? "Yok" : penaltyDetails;
                    worksheet.Cell(row, col - 1).Style.Alignment.WrapText = true;

                    var highwayPasses = customer.OtoyolGecisleri.OrderBy(p => p.GecisTarihi).ToList();
                    string highwayPassDetails = string.Join("\n", highwayPasses.Select(p =>
                        $"{p.Tutar} TL - {p.GecisTarihi.ToString("dd/MM/yyyy HH:mm")} ({p.Lokasyon?.LokasyonAdi ?? "Belirsiz"})"));
                    worksheet.Cell(row, col++).Value = string.IsNullOrEmpty(highwayPassDetails) ? "Yok" : highwayPassDetails;
                    worksheet.Cell(row, col - 1).Style.Alignment.WrapText = true;

                    row++;
                }

                worksheet.Columns().AdjustToContents();
                worksheet.Column(13).Width = 30;
                worksheet.Column(14).Width = 20;
                worksheet.Column(15).Width = 25;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    string excelName = $"MusteriListesi-{DateTime.Now.ToString("yyyyMMddHHmmss")}.xlsx";
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

                //ModelState.AddModelError("", "Lütfen bir Excel dosyası yükleyin.");
                //return RedirectToAction(nameof(Index), new { success = false, message = "Lütfen bir Excel dosyası yükleyin." });
            }

            int imported = 0, updated = 0, skipped = 0;
            var errors = new List<string>();

            try
            {
                using (var stream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheet(1);
                        var rows = worksheet.RowsUsed().Skip(1); // İlk satır başlık

                        foreach (var row in rows)
                        {
                            try
                            {
                                // ACTIVE kontrolü (1 ise atla)
                                var active = row.Cell(2).GetValue<string>()?.Trim();
                                if (active == "1")
                                {
                                    skipped++;
                                    continue;
                                }

                                // Zorunlu alan kontrolü
                                if (string.IsNullOrWhiteSpace(row.Cell(5).GetValue<string>()) || // DEFINITION_ (NAME)
                                    string.IsNullOrWhiteSpace(row.Cell(22).GetValue<string>())) // EMAILADDR
                                {
                                    skipped++;
                                    continue;
                                }

                                var customer = new Customer
                                {
                                    LOGICALREF = row.Cell(1).GetValue<int>(),
                                    ACTIVE = row.Cell(2)?.GetValue<bool?>(),
                                    CARDTYPE = row.Cell(3).GetValue<string?>(),
                                    CODE = row.Cell(4).GetValue<string?>()?.Trim(),
                                    DEFINITION_ = row.Cell(5).GetValue<string?>()?.Trim(),
                                    SPECODE = row.Cell(6).GetValue<string?>()?.Trim(),
                                    CYPHCODE = row.Cell(7).GetValue<string?>()?.Trim(),
                                    ADDR1 = row.Cell(8).GetValue<string?>()?.Trim(),
                                    ADDR2 = row.Cell(9).GetValue<string?>()?.Trim(),
                                    CITY = row.Cell(10).GetValue<string?>()?.Trim(),
                                    COUNTRY = row.Cell(11).GetValue<string>()?.Trim(),
                                    POSTCODE = row.Cell(12).GetValue<string>()?.Trim(),
                                    TELNRS1 = row.Cell(13).GetValue<string>()?.Trim(),
                                    TELNRS2 = row.Cell(14).GetValue<string>()?.Trim(),
                                    FAXNR = row.Cell(15).GetValue<string>()?.Trim(),
                                    TAXNR = row.Cell(16).GetValue<string>()?.Trim(),
                                    TAXOFFICE = row.Cell(17).GetValue<string>()?.Trim(),
                                    INCHARGE = row.Cell(18).GetValue<string>()?.Trim(),
                                    DISCRATE = row.Cell(19).GetValue<int>(),
                                    EXTENREF = row.Cell(20).GetValue<int>(),
                                    PAYMENTREF = row.Cell(21).GetValue<int>(),
                                    EMAILADDR = row.Cell(22).GetValue<string>()?.Trim(),
                                    WEBADDR = row.Cell(23).GetValue<string>()?.Trim(),
                                    WARNMETHOD = row.Cell(24).GetValue<int>(),
                                    WARNEMAILADDR = row.Cell(25).GetValue<string>()?.Trim(),
                                    WARNFAXNR = row.Cell(26).GetValue<string>()?.Trim(),
                                    CLANGUAGE = row.Cell(27).GetValue<string>(),
                                    VATNR = row.Cell(28).GetValue<string>()?.Trim(),
                                    BLOCKED = row.Cell(29).GetValue<bool>(),
                                    BANKBRANCHS1 = row.Cell(30).GetValue<string>()?.Trim(),
                                    BANKBRANCHS2 = row.Cell(31).GetValue<string>()?.Trim(),
                                    BANKBRANCHS3 = row.Cell(32).GetValue<string>()?.Trim(),
                                    BANKBRANCHS4 = row.Cell(33).GetValue<string>()?.Trim(),
                                    BANKBRANCHS5 = row.Cell(34).GetValue<string>()?.Trim(),
                                    BANKBRANCHS6 = row.Cell(35).GetValue<string>()?.Trim(),
                                    BANKBRANCHS7 = row.Cell(36).GetValue<string>()?.Trim(),
                                    BANKACCOUNTS1 = row.Cell(37).GetValue<string>()?.Trim(),
                                    BANKACCOUNTS2 = row.Cell(38).GetValue<string>()?.Trim(),
                                    BANKACCOUNTS3 = row.Cell(39).GetValue<string>()?.Trim(),
                                    BANKACCOUNTS4 = row.Cell(40).GetValue<string>()?.Trim(),
                                    BANKACCOUNTS5 = row.Cell(41).GetValue<string>()?.Trim(),
                                    BANKACCOUNTS6 = row.Cell(42).GetValue<string>()?.Trim(),
                                    BANKACCOUNTS7 = row.Cell(43).GetValue<string>()?.Trim(),
                                    DELIVERYMETHOD = row.Cell(44).GetValue<int?>(),
                                    DELIVERYFIRM = row.Cell(45).GetValue<int?>(),
                                    CCURRENCY = row.Cell(46).GetValue<string>(),
                                    TEXTINC = row.Cell(47).GetValue<int>(),
                                    SITEID = row.Cell(48).GetValue<int>(),
                                    RECSTATUS = row.Cell(49).GetValue<int>(),
                                    ORGLOGICREF = row.Cell(50).GetValue<int>(),
                                    EDINO = row.Cell(51).GetValue<string>()?.Trim(),
                                    TRADINGGRP = row.Cell(52).GetValue<string>()?.Trim(),
                                    CAPIBLOCK_CREATEDBY = row.Cell(53).GetValue<int>(),
                                    CAPIBLOCK_CREADEDDATE = row.Cell(54).GetValue<DateTime?>(),
                                    CAPIBLOCK_CREATEDHOUR = row.Cell(55).GetValue<short>(),
                                    CAPIBLOCK_CREATEDMIN = row.Cell(56).GetValue<short>(),
                                    CAPIBLOCK_CREATEDSEC = row.Cell(57).GetValue<short>(),
                                    CAPIBLOCK_MODIFIEDBY = row.Cell(58).GetValue<int>(),
                                    CAPIBLOCK_MODIFIEDDATE = row.Cell(59).GetValue<DateTime?>(),
                                    CAPIBLOCK_MODIFIEDHOUR = row.Cell(60).GetValue<short>(),
                                    CAPIBLOCK_MODIFIEDMIN = row.Cell(61).GetValue<short>(),
                                    CAPIBLOCK_MODIFIEDSEC = row.Cell(62).GetValue<short>(),
                                    PAYMENTPROC = row.Cell(63).GetValue<int>(),
                                    CRATEDIFFPROC = row.Cell(64).GetValue<int>(),
                                    WFSTATUS = row.Cell(65).GetValue<int>(),
                                    PPGROUPCODE = row.Cell(66).GetValue<string>()?.Trim(),
                                    PPGROUPREF = row.Cell(67).GetValue<int>(),
                                    TAXOFFCODE = row.Cell(68).GetValue<string>()?.Trim(),
                                    TOWNCODE = row.Cell(69).GetValue<string>()?.Trim(),
                                    TOWN = row.Cell(70).GetValue<string>()?.Trim(),
                                    DISTRICTCODE = row.Cell(71).GetValue<string>()?.Trim(),
                                    DISTRICT = row.Cell(72).GetValue<string>()?.Trim(),
                                    CITYCODE = row.Cell(73).GetValue<string>(),
                                    COUNTRYCODE = row.Cell(74).GetValue<string>()?.Trim(),
                                    ORDSENDMETHOD = row.Cell(75).GetValue<int>(),
                                    ORDSENDEMAILADDR = row.Cell(76).GetValue<string>()?.Trim(),
                                    ORDSENDFAXNR = row.Cell(77).GetValue<string>()?.Trim(),
                                    DSPSENDMETHOD = row.Cell(78).GetValue<int>(),
                                    DSPSENDEMAILADDR = row.Cell(79).GetValue<string>()?.Trim(),
                                    DSPSENDFAXNR = row.Cell(80).GetValue<string>()?.Trim(),
                                    INVSENDMETHOD = row.Cell(81).GetValue<int>(),
                                    INVSENDEMAILADDR = row.Cell(82).GetValue<string>()?.Trim(),
                                    INVSENDFAXNR = row.Cell(83).GetValue<string>()?.Trim(),
                                    SUBSCRIBERSTAT = row.Cell(84).GetValue<int>(),
                                    SUBSCRIBEREXT = row.Cell(85).GetValue<int>(),
                                    AUTOPAIDBANK = row.Cell(86).GetValue<int>(),
                                    PAYMENTTYPE = row.Cell(87).GetValue<int>(),
                                    LASTSENDREMLEV = row.Cell(88).GetValue<int>(),
                                    EXTACCESSFLAGS = row.Cell(89).GetValue<int>(),
                                    ORDSENDFORMAT = row.Cell(90).GetValue<int>(),
                                    DSPSENDFORMAT = row.Cell(91).GetValue<int>(),
                                    INVSENDFORMAT = row.Cell(92).GetValue<int>(),
                                    REMSENDFORMAT = row.Cell(93).GetValue<int>(),
                                    STORECREDITCARDNO = row.Cell(94).GetValue<string>()?.Trim(),
                                    CLORDFREQ = row.Cell(95).GetValue<int>(),
                                    ORDDAY = row.Cell(96).GetValue<int>(),
                                    LOGOID = row.Cell(97).GetValue<string>()?.Trim(),
                                    LIDCONFIRMED = row.Cell(98).GetValue<int>(),
                                    EXPREGNO = row.Cell(99).GetValue<string>()?.Trim(),
                                    EXPDOCNO = row.Cell(100).GetValue<string>()?.Trim(),
                                    EXPBUSTYPREF = row.Cell(101).GetValue<int>(),
                                    INVPRINTCNT = row.Cell(102).GetValue<int>(),
                                    PIECEORDINFLICT = row.Cell(103).GetValue<int>(),
                                    COLLECTINVOICING = row.Cell(104).GetValue<int>(),
                                    EBUSDATASENDTYPE = row.Cell(105).GetValue<int>(),
                                    INISTATUSFLAGS = row.Cell(106).GetValue<int>(),
                                    SLSORDERSTATUS = row.Cell(107).GetValue<int>(),
                                    SLSORDERPRICE = row.Cell(108).GetValue<int>(),
                                    LTRSENDMETHOD = row.Cell(109).GetValue<int>(),
                                    LTRSENDEMAILADDR = row.Cell(110).GetValue<string>()?.Trim(),
                                    LTRSENDFAXNR = row.Cell(111).GetValue<string>()?.Trim(),
                                    LTRSENDFORMAT = row.Cell(112).GetValue<int>(),
                                    IMAGEINC = row.Cell(113).GetValue<int>(),
                                    CELLPHONE = row.Cell(114).GetValue<string>()?.Trim(),
                                    SAMEITEMCODEUSE = row.Cell(115).GetValue<int>(),
                                    STATECODE = row.Cell(116).GetValue<string>()?.Trim(),
                                    STATENAME = row.Cell(117).GetValue<string>()?.Trim(),
                                    WFLOWCRDREF = row.Cell(118).GetValue<int>(),
                                    PARENTCLREF = row.Cell(119).GetValue<int>(),
                                    LOWLEVELCODES1 = row.Cell(120).GetValue<string>(),
                                    LOWLEVELCODES2 = row.Cell(121).GetValue<string>(),
                                    LOWLEVELCODES3 = row.Cell(122).GetValue<string>(),
                                    LOWLEVELCODES4 = row.Cell(123).GetValue<string>(),
                                    LOWLEVELCODES5 = row.Cell(124).GetValue<string>(),
                                    LOWLEVELCODES6 = row.Cell(125).GetValue<string>(),
                                    LOWLEVELCODES7 = row.Cell(126).GetValue<string>(),
                                    LOWLEVELCODES8 = row.Cell(127).GetValue<string>(),
                                    LOWLEVELCODES9 = row.Cell(128).GetValue<string>(),
                                    LOWLEVELCODES10 = row.Cell(129).GetValue<string>(),
                                    TELCODES1 = row.Cell(130).GetValue<string>()?.Trim(),
                                    TELCODES2 = row.Cell(131).GetValue<string>()?.Trim(),
                                    FAXCODE = row.Cell(132).GetValue<string>()?.Trim(),
                                    PURCHBRWS = row.Cell(133).GetValue<int>(),
                                    SALESBRWS = row.Cell(134).GetValue<int>(),
                                    IMPBRWS = row.Cell(135).GetValue<int>(),
                                    EXPBRWS = row.Cell(136).GetValue<int>(),
                                    FINBRWS = row.Cell(137).GetValue<int>(),
                                    ORGLOGOID = row.Cell(138).GetValue<int>(),
                                    ADDTOREFLIST = row.Cell(139).GetValue<int>(),
                                    TEXTREFTR = row.Cell(140).GetValue<int>(),
                                    TEXTREFEN = row.Cell(141).GetValue<int>(),
                                    ARPQUOTEINC = row.Cell(142).GetValue<int>(),
                                    CLCRM = row.Cell(143).GetValue<int>(),
                                    GRPFIRMNR = row.Cell(144).GetValue<int>(),
                                    CONSCODEREF = row.Cell(145).GetValue<int>(),
                                    SPECODE2 = row.Cell(146).GetValue<string>()?.Trim(),
                                    SPECODE3 = row.Cell(147).GetValue<string>()?.Trim(),
                                    SPECODE4 = row.Cell(148).GetValue<string>()?.Trim(),
                                    SPECODE5 = row.Cell(149).GetValue<string>()?.Trim(),
                                    OFFSENDMETHOD = row.Cell(150).GetValue<int>(),
                                    OFFSENDEMAILADDR = row.Cell(151).GetValue<string>()?.Trim(),
                                    OFFSENDFAXNR = row.Cell(152).GetValue<string>()?.Trim(),
                                    OFFSENDFORMAT = row.Cell(153).GetValue<int>(),
                                    EBANKNO = row.Cell(154).GetValue<string>()?.Trim(),
                                    LOANGRPCTRL = row.Cell(155).GetValue<int>(),
                                    BANKNAMES1 = row.Cell(156).GetValue<string>()?.Trim(),
                                    BANKNAMES2 = row.Cell(157).GetValue<string>()?.Trim(),
                                    BANKNAMES3 = row.Cell(158).GetValue<string>()?.Trim(),
                                    BANKNAMES4 = row.Cell(159).GetValue<string>()?.Trim(),
                                    BANKNAMES5 = row.Cell(160).GetValue<string>()?.Trim(),
                                    BANKNAMES6 = row.Cell(161).GetValue<string>()?.Trim(),
                                    BANKNAMES7 = row.Cell(162).GetValue<string>()?.Trim(),
                                    LDXFIRMNR = row.Cell(163).GetValue<int>(),
                                    MAPID = row.Cell(164).GetValue<string>()?.Trim(),
                                    LONGITUDE = row.Cell(165).GetValue<decimal>(),
                                    LATITUTE = row.Cell(166).GetValue<decimal>(),
                                    CITYID = row.Cell(167).GetValue<string>(),
                                    TOWNID = row.Cell(168).GetValue<string>(),
                                    BANKIBANS1 = row.Cell(169).GetValue<string>()?.Trim(),
                                    BANKIBANS2 = row.Cell(170).GetValue<string>()?.Trim(),
                                    BANKIBANS3 = row.Cell(171).GetValue<string>()?.Trim(),
                                    BANKIBANS4 = row.Cell(172).GetValue<string>()?.Trim(),
                                    BANKIBANS5 = row.Cell(173).GetValue<string>()?.Trim(),
                                    BANKIBANS6 = row.Cell(174).GetValue<string>()?.Trim(),
                                    BANKIBANS7 = row.Cell(175).GetValue<string>()?.Trim(),
                                    TCKNO = row.Cell(176).GetValue<string>()?.Trim(),
                                    ISPERSCOMP = row.Cell(177).GetValue<string>()?.Trim(),
                                    EXTSENDMETHOD = row.Cell(178).GetValue<int>(),
                                    EXTSENDEMAILADDR = row.Cell(179).GetValue<string>()?.Trim(),
                                    EXTSENDFAXNR = row.Cell(180).GetValue<string>()?.Trim(),
                                    EXTSENDFORMAT = row.Cell(181).GetValue<int>(),
                                    BANKBICS1 = row.Cell(182).GetValue<string>()?.Trim(),
                                    BANKBICS2 = row.Cell(183).GetValue<string>()?.Trim(),
                                    BANKBICS3 = row.Cell(184).GetValue<string>()?.Trim(),
                                    BANKBICS4 = row.Cell(185).GetValue<string>()?.Trim(),
                                    BANKBICS5 = row.Cell(186).GetValue<string>()?.Trim(),
                                    BANKBICS6 = row.Cell(187).GetValue<string>()?.Trim(),
                                    BANKBICS7 = row.Cell(188).GetValue<string>()?.Trim(),
                                    CASHREF = row.Cell(189).GetValue<int>(),
                                    USEDINPERIODS = row.Cell(190).GetValue<int>(),
                                    INCHARGE2 = row.Cell(191).GetValue<string>()?.Trim(),
                                    INCHARGE3 = row.Cell(192).GetValue<string>()?.Trim(),
                                    EMAILADDR2 = row.Cell(193).GetValue<string>()?.Trim(),
                                    EMAILADDR3 = row.Cell(194).GetValue<string>()?.Trim(),
                                    RSKLIMCR = row.Cell(195).GetValue<int>(),
                                    RSKDUEDATECR = row.Cell(196).GetValue<int>(),
                                    RSKAGINGCR = row.Cell(197).GetValue<int>(),
                                    RSKAGINGDAY = row.Cell(198).GetValue<int>(),
                                    ACCEPTEINV = row.Cell(199).GetValue<bool>(),
                                    EINVOICEID = row.Cell(200).GetValue<string>()?.Trim(),
                                    PROFILEID = row.Cell(201).GetValue<string>(),
                                    BANKBCURRENCY1 = row.Cell(202).GetValue<string>(),
                                    BANKBCURRENCY2 = row.Cell(203).GetValue<string>(),
                                    BANKBCURRENCY3 = row.Cell(204).GetValue<string>(),
                                    BANKBCURRENCY4 = row.Cell(205).GetValue<string>(),
                                    BANKBCURRENCY5 = row.Cell(206).GetValue<string>(),
                                    BANKBCURRENCY6 = row.Cell(207).GetValue<string>(),
                                    BANKBCURRENCY7 = row.Cell(208).GetValue<string>(),
                                    PURCORDERSTATUS = row.Cell(209).GetValue<int>(),
                                    PURCORDERPRICE = row.Cell(210).GetValue<int>(),
                                    ISFOREIGN = row.Cell(211).GetValue<int>(),
                                    SHIPBEGTIME1 = row.Cell(212).GetValue<int>(),
                                    SHIPBEGTIME2 = row.Cell(213).GetValue<string>()?.Trim(),
                                    SHIPBEGTIME3 = row.Cell(214).GetValue<string>()?.Trim(),
                                    SHIPENDTIME1 = row.Cell(215).GetValue<int>(),
                                    SHIPENDTIME2 = row.Cell(216).GetValue<string>()?.Trim(),
                                    SHIPENDTIME3 = row.Cell(217).GetValue<string>()?.Trim(),
                                    DBSLIMIT1 = row.Cell(218).GetValue<decimal>(),
                                    DBSLIMIT2 = row.Cell(219).GetValue<decimal>(),
                                    DBSLIMIT3 = row.Cell(220).GetValue<decimal>(),
                                    DBSLIMIT4 = row.Cell(221).GetValue<decimal>(),
                                    DBSLIMIT5 = row.Cell(222).GetValue<decimal>(),
                                    DBSLIMIT6 = row.Cell(223).GetValue<decimal>(),
                                    DBSLIMIT7 = row.Cell(224).GetValue<decimal>(),
                                    DBSTOTAL1 = row.Cell(225).GetValue<decimal>(),
                                    DBSTOTAL2 = row.Cell(226).GetValue<decimal>(),
                                    DBSTOTAL3 = row.Cell(227).GetValue<decimal>(),
                                    DBSTOTAL4 = row.Cell(228).GetValue<decimal>(),
                                    DBSTOTAL5 = row.Cell(229).GetValue<decimal>(),
                                    DBSTOTAL6 = row.Cell(230).GetValue<decimal>(),
                                    DBSTOTAL7 = row.Cell(231).GetValue<decimal>(),
                                    DBSBANKNO1 = row.Cell(232).GetValue<string>()?.Trim(),
                                    DBSBANKNO2 = row.Cell(233).GetValue<string>()?.Trim(),
                                    DBSBANKNO3 = row.Cell(234).GetValue<string>()?.Trim(),
                                    DBSBANKNO4 = row.Cell(235).GetValue<string>()?.Trim(),
                                    DBSBANKNO5 = row.Cell(236).GetValue<string>()?.Trim(),
                                    DBSBANKNO6 = row.Cell(237).GetValue<string>()?.Trim(),
                                    DBSBANKNO7 = row.Cell(238).GetValue<string>()?.Trim(),
                                    DBSRISKCNTRL1 = row.Cell(239).GetValue<int>(),
                                    DBSRISKCNTRL2 = row.Cell(240).GetValue<int>(),
                                    DBSRISKCNTRL3 = row.Cell(241).GetValue<int>(),
                                    DBSRISKCNTRL4 = row.Cell(242).GetValue<int>(),
                                    DBSRISKCNTRL5 = row.Cell(243).GetValue<int>(),
                                    DBSRISKCNTRL6 = row.Cell(244).GetValue<int>(),
                                    DBSRISKCNTRL7 = row.Cell(245).GetValue<int>(),
                                    DBSBANKCURRENCY1 = row.Cell(246).GetValue<int>(),
                                    DBSBANKCURRENCY2 = row.Cell(247).GetValue<int>(),
                                    DBSBANKCURRENCY3 = row.Cell(248).GetValue<int>(),
                                    DBSBANKCURRENCY4 = row.Cell(249).GetValue<int>(),
                                    DBSBANKCURRENCY5 = row.Cell(250).GetValue<int>(),
                                    DBSBANKCURRENCY6 = row.Cell(251).GetValue<int>(),
                                    DBSBANKCURRENCY7 = row.Cell(252).GetValue<int>(),
                                    BANKCORRPACC1 = row.Cell(253).GetValue<string>()?.Trim(),
                                    BANKCORRPACC2 = row.Cell(254).GetValue<string>()?.Trim(),
                                    BANKCORRPACC3 = row.Cell(255).GetValue<string>()?.Trim(),
                                    BANKCORRPACC4 = row.Cell(256).GetValue<string>()?.Trim(),
                                    BANKCORRPACC5 = row.Cell(257).GetValue<string>()?.Trim(),
                                    BANKCORRPACC6 = row.Cell(258).GetValue<string>()?.Trim(),
                                    BANKCORRPACC7 = row.Cell(259).GetValue<string>()?.Trim(),
                                    BANKVOEN1 = row.Cell(260).GetValue<string>()?.Trim(),
                                    BANKVOEN2 = row.Cell(261).GetValue<string>()?.Trim(),
                                    BANKVOEN3 = row.Cell(262).GetValue<string>()?.Trim(),
                                    BANKVOEN4 = row.Cell(263).GetValue<string>()?.Trim(),
                                    BANKVOEN5 = row.Cell(264).GetValue<string>()?.Trim(),
                                    BANKVOEN6 = row.Cell(265).GetValue<string>()?.Trim(),
                                    BANKVOEN7 = row.Cell(266).GetValue<string>()?.Trim(),
                                    EINVOICETYPE = row.Cell(267).GetValue<string>(),
                                    DEFINITION2 = row.Cell(268).GetValue<string>()?.Trim(),
                                    TELEXTNUMS1 = row.Cell(269).GetValue<string>()?.Trim(),
                                    TELEXTNUMS2 = row.Cell(270).GetValue<string>()?.Trim(),
                                    FAXEXTNUM = row.Cell(271).GetValue<string>()?.Trim(),
                                    FACEBOOKURL = row.Cell(272).GetValue<string>()?.Trim(),
                                    TWITTERURL = row.Cell(273).GetValue<string>()?.Trim(),
                                    APPLEID = row.Cell(274).GetValue<string>()?.Trim(),
                                    SKYPEID = row.Cell(275).GetValue<string>()?.Trim(),
                                    GLOBALID = row.Cell(276).GetValue<string>()?.Trim(),
                                    GUID = row.Cell(277).GetValue<string>()?.Trim(),
                                    DUEDATECOUNT = row.Cell(278).GetValue<int>(),
                                    DUEDATELIMIT = row.Cell(279).GetValue<int>(),
                                    DUEDATETRACK = row.Cell(280).GetValue<int>(),
                                    DUEDATECONTROL1 = row.Cell(281).GetValue<int>(),
                                    DUEDATECONTROL2 = row.Cell(282).GetValue<int>(),
                                    DUEDATECONTROL3 = row.Cell(283).GetValue<int>(),
                                    DUEDATECONTROL4 = row.Cell(284).GetValue<int>(),
                                    DUEDATECONTROL5 = row.Cell(285).GetValue<int>(),
                                    DUEDATECONTROL6 = row.Cell(286).GetValue<int>(),
                                    DUEDATECONTROL7 = row.Cell(287).GetValue<int>(),
                                    DUEDATECONTROL8 = row.Cell(288).GetValue<int>(),
                                    DUEDATECONTROL9 = row.Cell(289).GetValue<int>(),
                                    DUEDATECONTROL10 = row.Cell(290).GetValue<int>(),
                                    DUEDATECONTROL11 = row.Cell(291).GetValue<int>(),
                                    DUEDATECONTROL12 = row.Cell(292).GetValue<int>(),
                                    DUEDATECONTROL13 = row.Cell(293).GetValue<int>(),
                                    DUEDATECONTROL14 = row.Cell(294).GetValue<int>(),
                                    DUEDATECONTROL15 = row.Cell(295).GetValue<int>(),
                                    ADRESSNO = row.Cell(296).GetValue<string>()?.Trim(),
                                    POSTLABELCODE = row.Cell(297).GetValue<string>()?.Trim(),
                                    SENDERLABELCODE = row.Cell(298).GetValue<string>()?.Trim(),
                                    CLOSEDATECOUNT = row.Cell(299).GetValue<int>(),
                                    CLOSEDATETRACK = row.Cell(300).GetValue<int>(),
                                    DEGACTIVE = row.Cell(301).GetValue<int>(),
                                    DEGCURR = row.Cell(302).GetValue<int>(),
                                    NAME = row.Cell(303).GetValue<string>()?.Trim(),
                                    SURNAME = row.Cell(304).GetValue<string>()?.Trim(),
                                    LABELINFO = row.Cell(305).GetValue<string>()?.Trim(),
                                    DEFBNACCREF = row.Cell(306).GetValue<int>(),
                                    PROJECTREF = row.Cell(307).GetValue<int>(),
                                    DISCTYPE = row.Cell(308).GetValue<string>(),
                                    SENDMOD = row.Cell(309).GetValue<int>(),
                                    ISPERCURR = row.Cell(310).GetValue<string>(),
                                    CURRATETYPE = row.Cell(311).GetValue<string>(),
                                    INSTEADOFDESP = row.Cell(312).GetValue<int>(),
                                    EINVOICETYP = row.Cell(313).GetValue<string>(),
                                    FBSSENDMETHOD = row.Cell(314).GetValue<int>(),
                                    FBSSENDEMAILADDR = row.Cell(315).GetValue<string>()?.Trim(),
                                    FBSSENDFORMAT = row.Cell(316).GetValue<int>(),
                                    FBSSENDFAXNR = row.Cell(317).GetValue<string>()?.Trim(),
                                    FBASENDMETHOD = row.Cell(318).GetValue<int>(),
                                    FBASENDEMAILADDR = row.Cell(319).GetValue<string>()?.Trim(),
                                    FBASENDFORMAT = row.Cell(320).GetValue<int>(),
                                    FBASENDFAXNR = row.Cell(321).GetValue<string>()?.Trim(),
                                    SECTORMAINREF = row.Cell(322).GetValue<int>(),
                                    SECTORSUBREF = row.Cell(323).GetValue<int>(),
                                    PERSONELCOSTS = row.Cell(324).GetValue<int>(),
                                    EARCEMAILADDR1 = row.Cell(325).GetValue<string>()?.Trim(),
                                    EARCEMAILADDR2 = row.Cell(326).GetValue<string>()?.Trim(),
                                    EARCEMAILADDR3 = row.Cell(327).GetValue<string>()?.Trim(),
                                    FACTORYDIVNR = row.Cell(328).GetValue<int>(),
                                    FACTORYNR = row.Cell(329).GetValue<int>(),
                                    ININVENNR = row.Cell(330).GetValue<int>(),
                                    OUTINVENNR = row.Cell(331).GetValue<int>(),
                                    QTYDEPDURATION = row.Cell(332).GetValue<int>(),
                                    QTYINDEPDURATION = row.Cell(333).GetValue<int>(),
                                    OVERLAPTYPE = row.Cell(334).GetValue<int>(),
                                    OVERLAPAMNT = row.Cell(335).GetValue<decimal>(),
                                    OVERLAPPERC = row.Cell(336).GetValue<decimal>(),
                                    BROKERCOMP = row.Cell(337).GetValue<int>(),
                                    CREATEWHFICHE = row.Cell(338).GetValue<int>(),
                                    EINVCUSTOM = row.Cell(339).GetValue<int>(),
                                    SUBCONT = row.Cell(340).GetValue<int>(),
                                    ORDPRIORITY = row.Cell(341).GetValue<int>(),
                                    ACCEPTEDESP = row.Cell(342).GetValue<int>(),
                                    PROFILEIDDESP = row.Cell(343).GetValue<string?>(),
                                    LABELINFODESP = row.Cell(344).GetValue<string>()?.Trim(),
                                    POSTLABELCODEDESP = row.Cell(345).GetValue<string>()?.Trim(),
                                    SENDERLABELCODEDESP = row.Cell(346).GetValue<string>()?.Trim(),
                                    ACCEPTEINVPUBLIC = row.Cell(347).GetValue<string>(),
                                    PUBLICBNACCREF = row.Cell(348).GetValue<int>(),
                                    PAYMENTPROCBRANCH = row.Cell(349).GetValue<int>(),
                                    KVKKPERMSTATUS = row.Cell(350).GetValue<int>(),
                                    KVKKBEGDATE = row.Cell(351).GetValue<DateTime?>(),
                                    KVKKENDDATE = row.Cell(352).GetValue<DateTime?>(),
                                    KVKKCANCELDATE = row.Cell(353).GetValue<DateTime?>(),
                                    KVKKANONYSTATUS = row.Cell(354).GetValue<int>(),
                                    KVKKANONYDATE = row.Cell(355).GetValue<DateTime?>(),
                                    CLCCANDEDUCT = row.Cell(356).GetValue<int>(),
                                    DRIVERREF = row.Cell(357).GetValue<int>(),
                                    EXIMSENDFAXNR = row.Cell(358).GetValue<string>()?.Trim(),
                                    EXIMSENDMETHOD = row.Cell(359).GetValue<int>(),
                                    EXIMSENDEMAILADDR = row.Cell(360).GetValue<string>()?.Trim(),
                                    EXIMSENDFORMAT = row.Cell(361).GetValue<int>(),
                                    EXIMREGTYPREF = row.Cell(362).GetValue<int>(),
                                    EXIMNTFYCLREF = row.Cell(363).GetValue<int>(),
                                    EXIMCNSLTCLREF = row.Cell(364).GetValue<int>(),
                                    EXIMPAYTYPREF = row.Cell(365).GetValue<int>(),
                                    EXIMBRBANKREF = row.Cell(366).GetValue<int>(),
                                    EXIMCUSTOMREF = row.Cell(367).GetValue<int>(),
                                    EXIMFRGHTCLREF = row.Cell(368).GetValue<int>(),
                                    CLSTYPEFORPPAYDT = row.Cell(369).GetValue<int>(),
                                    MERSISNO = row.Cell(370).GetValue<string>()?.Trim(),
                                    COMMRECORDNO = row.Cell(371).GetValue<string>()?.Trim(),
                                    DISPPRINTCNT = row.Cell(372).GetValue<int>(),
                                    ORDPRINTCNT = row.Cell(373).GetValue<int>(),
                                    CLPTYPEFORPPAYDT = row.Cell(374).GetValue<int>(),
                                    IMCNTRYREF = row.Cell(375).GetValue<int>(),
                                    INCHTELNRS1 = row.Cell(376).GetValue<string>()?.Trim(),
                                    INCHTELNRS2 = row.Cell(377).GetValue<string>()?.Trim(),
                                    INCHTELNRS3 = row.Cell(378).GetValue<string>()?.Trim(),
                                    INCHTELCODES1 = row.Cell(379).GetValue<string>()?.Trim(),
                                    INCHTELCODES2 = row.Cell(380).GetValue<string>()?.Trim(),
                                    INCHTELCODES3 = row.Cell(381).GetValue<string>()?.Trim(),
                                    INCHTELEXTNUMS1 = row.Cell(382).GetValue<string>()?.Trim(),
                                    EXCNTRYTYP = row.Cell(383).GetValue<int>(),
                                    EXCNTRYREF = row.Cell(384).GetValue<int>(),
                                    IMCNTRYTYP = row.Cell(385).GetValue<int>(),
                                    INCHTELEXTNUMS2 = row.Cell(386).GetValue<string>()?.Trim(),
                                    INCHTELEXTNUMS3 = row.Cell(387).GetValue<string>()?.Trim(),
                                    NOTIFYCRDREF = row.Cell(388).GetValue<int>(),
                                    WHATSAPPID = row.Cell(389).GetValue<string>()?.Trim(),
                                    LINKEDINURL = row.Cell(390).GetValue<string>()?.Trim(),
                                    INSTAGRAMURL = row.Cell(391).GetValue<string>()?.Trim(),
                                };

                                // ISPERSCOMP alanını normalize et
                                if (!string.IsNullOrEmpty(customer.ISPERSCOMP))
                                {
                                    var v = customer.ISPERSCOMP.Trim().ToLowerInvariant();
                                    if (v == "true" || v == "1" || v == "evet" || v == "e") customer.ISPERSCOMP = "1";
                                    else if (v == "false" || v == "0" || v == "hayır" || v == "h") customer.ISPERSCOMP = "0";
                                    else customer.ISPERSCOMP = null;
                                }

                                // Aynı TCKNO veya TAXNR ile müşteri var mı kontrol et
                                Customer? existing = null;
                                if (!string.IsNullOrEmpty(customer.TCKNO))
                                    existing = await _context.Customers.FirstOrDefaultAsync(c => c.TCKNO == customer.TCKNO);
                                else if (!string.IsNullOrEmpty(customer.TAXNR))
                                    existing = await _context.Customers.FirstOrDefaultAsync(c => c.TAXNR == customer.TAXNR);

                                if (existing != null)
                                {
                                    // Güncelle
                                    existing.LOGICALREF = customer.LOGICALREF;
                                    existing.ACTIVE = customer.ACTIVE;
                                    existing.CARDTYPE = customer.CARDTYPE;
                                    existing.CODE = customer.CODE;
                                    existing.DEFINITION_ = customer.DEFINITION_;
                                    existing.SPECODE = customer.SPECODE;
                                    existing.CYPHCODE = customer.CYPHCODE;
                                    existing.ADDR1 = customer.ADDR1;
                                    existing.ADDR2 = customer.ADDR2;
                                    existing.CITY = customer.CITY;
                                    existing.COUNTRY = customer.COUNTRY;
                                    existing.POSTCODE = customer.POSTCODE;
                                    existing.TELNRS1 = customer.TELNRS1;
                                    existing.TELNRS2 = customer.TELNRS2;
                                    existing.FAXNR = customer.FAXNR;
                                    existing.TAXNR = customer.TAXNR;
                                    existing.TAXOFFICE = customer.TAXOFFICE;
                                    existing.INCHARGE = customer.INCHARGE;
                                    existing.DISCRATE = customer.DISCRATE;
                                    existing.EXTENREF = customer.EXTENREF;
                                    existing.PAYMENTREF = customer.PAYMENTREF;
                                    existing.EMAILADDR = customer.EMAILADDR;
                                    existing.WEBADDR = customer.WEBADDR;
                                    existing.WARNMETHOD = customer.WARNMETHOD;
                                    existing.WARNEMAILADDR = customer.WARNEMAILADDR;
                                    existing.WARNFAXNR = customer.WARNFAXNR;
                                    existing.CLANGUAGE = customer.CLANGUAGE;
                                    existing.VATNR = customer.VATNR;
                                    existing.BLOCKED = customer.BLOCKED;
                                    existing.BANKBRANCHS1 = customer.BANKBRANCHS1;
                                    existing.BANKBRANCHS2 = customer.BANKBRANCHS2;
                                    existing.BANKBRANCHS3 = customer.BANKBRANCHS3;
                                    existing.BANKBRANCHS4 = customer.BANKBRANCHS4;
                                    existing.BANKBRANCHS5 = customer.BANKBRANCHS5;
                                    existing.BANKBRANCHS6 = customer.BANKBRANCHS6;
                                    existing.BANKBRANCHS7 = customer.BANKBRANCHS7;
                                    existing.BANKACCOUNTS1 = customer.BANKACCOUNTS1;
                                    existing.BANKACCOUNTS2 = customer.BANKACCOUNTS2;
                                    existing.BANKACCOUNTS3 = customer.BANKACCOUNTS3;
                                    existing.BANKACCOUNTS4 = customer.BANKACCOUNTS4;
                                    existing.BANKACCOUNTS5 = customer.BANKACCOUNTS5;
                                    existing.BANKACCOUNTS6 = customer.BANKACCOUNTS6;
                                    existing.BANKACCOUNTS7 = customer.BANKACCOUNTS7;
                                    existing.DELIVERYMETHOD = customer.DELIVERYMETHOD;
                                    existing.DELIVERYFIRM = customer.DELIVERYFIRM;
                                    existing.CCURRENCY = customer.CCURRENCY;
                                    existing.TEXTINC = customer.TEXTINC;
                                    existing.SITEID = customer.SITEID;
                                    existing.RECSTATUS = customer.RECSTATUS;
                                    existing.ORGLOGICREF = customer.ORGLOGICREF;
                                    existing.EDINO = customer.EDINO;
                                    existing.TRADINGGRP = customer.TRADINGGRP;
                                    existing.CAPIBLOCK_CREATEDBY = customer.CAPIBLOCK_CREATEDBY;
                                    existing.CAPIBLOCK_CREADEDDATE = customer.CAPIBLOCK_CREADEDDATE;
                                    existing.CAPIBLOCK_CREATEDHOUR = customer.CAPIBLOCK_CREATEDHOUR;
                                    existing.CAPIBLOCK_CREATEDMIN = customer.CAPIBLOCK_CREATEDMIN;
                                    existing.CAPIBLOCK_CREATEDSEC = customer.CAPIBLOCK_CREATEDSEC;
                                    existing.CAPIBLOCK_MODIFIEDBY = customer.CAPIBLOCK_MODIFIEDBY;
                                    existing.CAPIBLOCK_MODIFIEDDATE = customer.CAPIBLOCK_MODIFIEDDATE;
                                    existing.CAPIBLOCK_MODIFIEDHOUR = customer.CAPIBLOCK_MODIFIEDHOUR;
                                    existing.CAPIBLOCK_MODIFIEDMIN = customer.CAPIBLOCK_MODIFIEDMIN;
                                    existing.CAPIBLOCK_MODIFIEDSEC = customer.CAPIBLOCK_MODIFIEDSEC;
                                    existing.PAYMENTPROC = customer.PAYMENTPROC;
                                    existing.CRATEDIFFPROC = customer.CRATEDIFFPROC;
                                    existing.WFSTATUS = customer.WFSTATUS;
                                    existing.PPGROUPCODE = customer.PPGROUPCODE;
                                    existing.PPGROUPREF = customer.PPGROUPREF;
                                    existing.TAXOFFCODE = customer.TAXOFFCODE;
                                    existing.TOWNCODE = customer.TOWNCODE;
                                    existing.TOWN = customer.TOWN;
                                    existing.DISTRICTCODE = customer.DISTRICTCODE;
                                    existing.DISTRICT = customer.DISTRICT;
                                    existing.CITYCODE = customer.CITYCODE;
                                    existing.COUNTRYCODE = customer.COUNTRYCODE;
                                    existing.ORDSENDMETHOD = customer.ORDSENDMETHOD;
                                    existing.ORDSENDEMAILADDR = customer.ORDSENDEMAILADDR;
                                    existing.ORDSENDFAXNR = customer.ORDSENDFAXNR;
                                    existing.DSPSENDMETHOD = customer.DSPSENDMETHOD;
                                    existing.DSPSENDEMAILADDR = customer.DSPSENDEMAILADDR;
                                    existing.DSPSENDFAXNR = customer.DSPSENDFAXNR;
                                    existing.INVSENDMETHOD = customer.INVSENDMETHOD;
                                    existing.INVSENDEMAILADDR = customer.INVSENDEMAILADDR;
                                    existing.INVSENDFAXNR = customer.INVSENDFAXNR;
                                    existing.SUBSCRIBERSTAT = customer.SUBSCRIBERSTAT;
                                    existing.SUBSCRIBEREXT = customer.SUBSCRIBEREXT;
                                    existing.AUTOPAIDBANK = customer.AUTOPAIDBANK;
                                    existing.PAYMENTTYPE = customer.PAYMENTTYPE;
                                    existing.LASTSENDREMLEV = customer.LASTSENDREMLEV;
                                    existing.EXTACCESSFLAGS = customer.EXTACCESSFLAGS;
                                    existing.ORDSENDFORMAT = customer.ORDSENDFORMAT;
                                    existing.DSPSENDFORMAT = customer.DSPSENDFORMAT;
                                    existing.INVSENDFORMAT = customer.INVSENDFORMAT;
                                    existing.REMSENDFORMAT = customer.REMSENDFORMAT;
                                    existing.STORECREDITCARDNO = customer.STORECREDITCARDNO;
                                    existing.CLORDFREQ = customer.CLORDFREQ;
                                    existing.ORDDAY = customer.ORDDAY;
                                    existing.LOGOID = customer.LOGOID;
                                    existing.LIDCONFIRMED = customer.LIDCONFIRMED;
                                    existing.EXPREGNO = customer.EXPREGNO;
                                    existing.EXPDOCNO = customer.EXPDOCNO;
                                    existing.EXPBUSTYPREF = customer.EXPBUSTYPREF;
                                    existing.INVPRINTCNT = customer.INVPRINTCNT;
                                    existing.PIECEORDINFLICT = customer.PIECEORDINFLICT;
                                    existing.COLLECTINVOICING = customer.COLLECTINVOICING;
                                    existing.EBUSDATASENDTYPE = customer.EBUSDATASENDTYPE;
                                    existing.INISTATUSFLAGS = customer.INISTATUSFLAGS;
                                    existing.SLSORDERSTATUS = customer.SLSORDERSTATUS;
                                    existing.SLSORDERPRICE = customer.SLSORDERPRICE;
                                    existing.LTRSENDMETHOD = customer.LTRSENDMETHOD;
                                    existing.LTRSENDEMAILADDR = customer.LTRSENDEMAILADDR;
                                    existing.LTRSENDFAXNR = customer.LTRSENDFAXNR;
                                    existing.LTRSENDFORMAT = customer.LTRSENDFORMAT;
                                    existing.IMAGEINC = customer.IMAGEINC;
                                    existing.CELLPHONE = customer.CELLPHONE;
                                    existing.SAMEITEMCODEUSE = customer.SAMEITEMCODEUSE;
                                    existing.STATECODE = customer.STATECODE;
                                    existing.STATENAME = customer.STATENAME;
                                    existing.WFLOWCRDREF = customer.WFLOWCRDREF;
                                    existing.PARENTCLREF = customer.PARENTCLREF;
                                    existing.LOWLEVELCODES1 = customer.LOWLEVELCODES1;
                                    existing.LOWLEVELCODES2 = customer.LOWLEVELCODES2;
                                    existing.LOWLEVELCODES3 = customer.LOWLEVELCODES3;
                                    existing.LOWLEVELCODES4 = customer.LOWLEVELCODES4;
                                    existing.LOWLEVELCODES5 = customer.LOWLEVELCODES5;
                                    existing.LOWLEVELCODES6 = customer.LOWLEVELCODES6;
                                    existing.LOWLEVELCODES7 = customer.LOWLEVELCODES7;
                                    existing.LOWLEVELCODES8 = customer.LOWLEVELCODES8;
                                    existing.LOWLEVELCODES9 = customer.LOWLEVELCODES9;
                                    existing.LOWLEVELCODES10 = customer.LOWLEVELCODES10;
                                    existing.TELCODES1 = customer.TELCODES1;
                                    existing.TELCODES2 = customer.TELCODES2;
                                    existing.FAXCODE = customer.FAXCODE;
                                    existing.PURCHBRWS = customer.PURCHBRWS;
                                    existing.SALESBRWS = customer.SALESBRWS;
                                    existing.IMPBRWS = customer.IMPBRWS;
                                    existing.EXPBRWS = customer.EXPBRWS;
                                    existing.FINBRWS = customer.FINBRWS;
                                    existing.ORGLOGOID = customer.ORGLOGOID;
                                    existing.ADDTOREFLIST = customer.ADDTOREFLIST;
                                    existing.TEXTREFTR = customer.TEXTREFTR;
                                    existing.TEXTREFEN = customer.TEXTREFEN;
                                    existing.ARPQUOTEINC = customer.ARPQUOTEINC;
                                    existing.CLCRM = customer.CLCRM;
                                    existing.GRPFIRMNR = customer.GRPFIRMNR;
                                    existing.CONSCODEREF = customer.CONSCODEREF;
                                    existing.SPECODE2 = customer.SPECODE2;
                                    existing.SPECODE3 = customer.SPECODE3;
                                    existing.SPECODE4 = customer.SPECODE4;
                                    existing.SPECODE5 = customer.SPECODE5;
                                    existing.OFFSENDMETHOD = customer.OFFSENDMETHOD;
                                    existing.OFFSENDEMAILADDR = customer.OFFSENDEMAILADDR;
                                    existing.OFFSENDFAXNR = customer.OFFSENDFAXNR;
                                    existing.OFFSENDFORMAT = customer.OFFSENDFORMAT;
                                    existing.EBANKNO = customer.EBANKNO;
                                    existing.LOANGRPCTRL = customer.LOANGRPCTRL;
                                    existing.BANKNAMES1 = customer.BANKNAMES1;
                                    existing.BANKNAMES2 = customer.BANKNAMES2;
                                    existing.BANKNAMES3 = customer.BANKNAMES3;
                                    existing.BANKNAMES4 = customer.BANKNAMES4;
                                    existing.BANKNAMES5 = customer.BANKNAMES5;
                                    existing.BANKNAMES6 = customer.BANKNAMES6;
                                    existing.BANKNAMES7 = customer.BANKNAMES7;
                                    existing.LDXFIRMNR = customer.LDXFIRMNR;
                                    existing.MAPID = customer.MAPID;
                                    existing.LONGITUDE = customer.LONGITUDE;
                                    existing.LATITUTE = customer.LATITUTE;
                                    existing.CITYID = customer.CITYID;
                                    existing.TOWNID = customer.TOWNID;
                                    existing.BANKIBANS1 = customer.BANKIBANS1;
                                    existing.BANKIBANS2 = customer.BANKIBANS2;
                                    existing.BANKIBANS3 = customer.BANKIBANS3;
                                    existing.BANKIBANS4 = customer.BANKIBANS4;
                                    existing.BANKIBANS5 = customer.BANKIBANS5;
                                    existing.BANKIBANS6 = customer.BANKIBANS6;
                                    existing.BANKIBANS7 = customer.BANKIBANS7;
                                    existing.TCKNO = customer.TCKNO;
                                    existing.ISPERSCOMP = customer.ISPERSCOMP;
                                    existing.EXTSENDMETHOD = customer.EXTSENDMETHOD;
                                    existing.EXTSENDEMAILADDR = customer.EXTSENDEMAILADDR;
                                    existing.EXTSENDFAXNR = customer.EXTSENDFAXNR;
                                    existing.EXTSENDFORMAT = customer.EXTSENDFORMAT;
                                    existing.BANKBICS1 = customer.BANKBICS1;
                                    existing.BANKBICS2 = customer.BANKBICS2;
                                    existing.BANKBICS3 = customer.BANKBICS3;
                                    existing.BANKBICS4 = customer.BANKBICS4;
                                    existing.BANKBICS5 = customer.BANKBICS5;
                                    existing.BANKBICS6 = customer.BANKBICS6;
                                    existing.BANKBICS7 = customer.BANKBICS7;
                                    existing.CASHREF = customer.CASHREF;
                                    existing.USEDINPERIODS = customer.USEDINPERIODS;
                                    existing.INCHARGE2 = customer.INCHARGE2;
                                    existing.INCHARGE3 = customer.INCHARGE3;
                                    existing.EMAILADDR2 = customer.EMAILADDR2;
                                    existing.EMAILADDR3 = customer.EMAILADDR3;
                                    existing.RSKLIMCR = customer.RSKLIMCR;
                                    existing.RSKDUEDATECR = customer.RSKDUEDATECR;
                                    existing.RSKAGINGCR = customer.RSKAGINGCR;
                                    existing.RSKAGINGDAY = customer.RSKAGINGDAY;
                                    existing.ACCEPTEINV = customer.ACCEPTEINV;
                                    existing.EINVOICEID = customer.EINVOICEID;
                                    existing.PROFILEID = customer.PROFILEID;
                                    existing.BANKBCURRENCY1 = customer.BANKBCURRENCY1;
                                    existing.BANKBCURRENCY2 = customer.BANKBCURRENCY2;
                                    existing.BANKBCURRENCY3 = customer.BANKBCURRENCY3;
                                    existing.BANKBCURRENCY4 = customer.BANKBCURRENCY4;
                                    existing.BANKBCURRENCY5 = customer.BANKBCURRENCY5;
                                    existing.BANKBCURRENCY6 = customer.BANKBCURRENCY6;
                                    existing.BANKBCURRENCY7 = customer.BANKBCURRENCY7;
                                    existing.PURCORDERSTATUS = customer.PURCORDERSTATUS;
                                    existing.PURCORDERPRICE = customer.PURCORDERPRICE;
                                    existing.ISFOREIGN = customer.ISFOREIGN;
                                    existing.SHIPBEGTIME1 = customer.SHIPBEGTIME1;
                                    existing.SHIPBEGTIME2 = customer.SHIPBEGTIME2;
                                    existing.SHIPBEGTIME3 = customer.SHIPBEGTIME3;
                                    existing.SHIPENDTIME1 = customer.SHIPENDTIME1;
                                    existing.SHIPENDTIME2 = customer.SHIPENDTIME2;
                                    existing.SHIPENDTIME3 = customer.SHIPENDTIME3;
                                    existing.DBSLIMIT1 = customer.DBSLIMIT1;
                                    existing.DBSLIMIT2 = customer.DBSLIMIT2;
                                    existing.DBSLIMIT3 = customer.DBSLIMIT3;
                                    existing.DBSLIMIT4 = customer.DBSLIMIT4;
                                    existing.DBSLIMIT5 = customer.DBSLIMIT5;
                                    existing.DBSLIMIT6 = customer.DBSLIMIT6;
                                    existing.DBSLIMIT7 = customer.DBSLIMIT7;
                                    existing.DBSTOTAL1 = customer.DBSTOTAL1;
                                    existing.DBSTOTAL2 = customer.DBSTOTAL2;
                                    existing.DBSTOTAL3 = customer.DBSTOTAL3;
                                    existing.DBSTOTAL4 = customer.DBSTOTAL4;
                                    existing.DBSTOTAL5 = customer.DBSTOTAL5;
                                    existing.DBSTOTAL6 = customer.DBSTOTAL6;
                                    existing.DBSTOTAL7 = customer.DBSTOTAL7;
                                    existing.DBSBANKNO1 = customer.DBSBANKNO1;
                                    existing.DBSBANKNO2 = customer.DBSBANKNO2;
                                    existing.DBSBANKNO3 = customer.DBSBANKNO3;
                                    existing.DBSBANKNO4 = customer.DBSBANKNO4;
                                    existing.DBSBANKNO5 = customer.DBSBANKNO5;
                                    existing.DBSBANKNO6 = customer.DBSBANKNO6;
                                    existing.DBSBANKNO7 = customer.DBSBANKNO7;
                                    existing.DBSRISKCNTRL1 = customer.DBSRISKCNTRL1;
                                    existing.DBSRISKCNTRL2 = customer.DBSRISKCNTRL2;
                                    existing.DBSRISKCNTRL3 = customer.DBSRISKCNTRL3;
                                    existing.DBSRISKCNTRL4 = customer.DBSRISKCNTRL4;
                                    existing.DBSRISKCNTRL5 = customer.DBSRISKCNTRL5;
                                    existing.DBSRISKCNTRL6 = customer.DBSRISKCNTRL6;
                                    existing.DBSRISKCNTRL7 = customer.DBSRISKCNTRL7;
                                    existing.DBSBANKCURRENCY1 = customer.DBSBANKCURRENCY1;
                                    existing.DBSBANKCURRENCY2 = customer.DBSBANKCURRENCY2;
                                    existing.DBSBANKCURRENCY3 = customer.DBSBANKCURRENCY3;
                                    existing.DBSBANKCURRENCY4 = customer.DBSBANKCURRENCY4;
                                    existing.DBSBANKCURRENCY5 = customer.DBSBANKCURRENCY5;
                                    existing.DBSBANKCURRENCY6 = customer.DBSBANKCURRENCY6;
                                    existing.DBSBANKCURRENCY7 = customer.DBSBANKCURRENCY7;
                                    existing.BANKCORRPACC1 = customer.BANKCORRPACC1;
                                    existing.BANKCORRPACC2 = customer.BANKCORRPACC2;
                                    existing.BANKCORRPACC3 = customer.BANKCORRPACC3;
                                    existing.BANKCORRPACC4 = customer.BANKCORRPACC4;
                                    existing.BANKCORRPACC5 = customer.BANKCORRPACC5;
                                    existing.BANKCORRPACC6 = customer.BANKCORRPACC6;
                                    existing.BANKCORRPACC7 = customer.BANKCORRPACC7;
                                    existing.BANKVOEN1 = customer.BANKVOEN1;
                                    existing.BANKVOEN2 = customer.BANKVOEN2;
                                    existing.BANKVOEN3 = customer.BANKVOEN3;
                                    existing.BANKVOEN4 = customer.BANKVOEN4;
                                    existing.BANKVOEN5 = customer.BANKVOEN5;
                                    existing.BANKVOEN6 = customer.BANKVOEN6;
                                    existing.BANKVOEN7 = customer.BANKVOEN7;
                                    existing.EINVOICETYPE = customer.EINVOICETYPE;
                                    existing.DEFINITION2 = customer.DEFINITION2;
                                    existing.TELEXTNUMS1 = customer.TELEXTNUMS1;
                                    existing.TELEXTNUMS2 = customer.TELEXTNUMS2;
                                    existing.FAXEXTNUM = customer.FAXEXTNUM;
                                    existing.FACEBOOKURL = customer.FACEBOOKURL;
                                    existing.TWITTERURL = customer.TWITTERURL;
                                    existing.APPLEID = customer.APPLEID;
                                    existing.SKYPEID = customer.SKYPEID;
                                    existing.GLOBALID = customer.GLOBALID;
                                    existing.GUID = customer.GUID;
                                    existing.DUEDATECOUNT = customer.DUEDATECOUNT;
                                    existing.DUEDATELIMIT = customer.DUEDATELIMIT;
                                    existing.DUEDATETRACK = customer.DUEDATETRACK;
                                    existing.DUEDATECONTROL1 = customer.DUEDATECONTROL1;
                                    existing.DUEDATECONTROL2 = customer.DUEDATECONTROL2;
                                    existing.DUEDATECONTROL3 = customer.DUEDATECONTROL3;
                                    existing.DUEDATECONTROL4 = customer.DUEDATECONTROL4;
                                    existing.DUEDATECONTROL5 = customer.DUEDATECONTROL5;
                                    existing.DUEDATECONTROL6 = customer.DUEDATECONTROL6;
                                    existing.DUEDATECONTROL7 = customer.DUEDATECONTROL7;
                                    existing.DUEDATECONTROL8 = customer.DUEDATECONTROL8;
                                    existing.DUEDATECONTROL9 = customer.DUEDATECONTROL9;
                                    existing.DUEDATECONTROL10 = customer.DUEDATECONTROL10;
                                    existing.DUEDATECONTROL11 = customer.DUEDATECONTROL11;
                                    existing.DUEDATECONTROL12 = customer.DUEDATECONTROL12;
                                    existing.DUEDATECONTROL13 = customer.DUEDATECONTROL13;
                                    existing.DUEDATECONTROL14 = customer.DUEDATECONTROL14;
                                    existing.DUEDATECONTROL15 = customer.DUEDATECONTROL15;
                                    existing.ADRESSNO = customer.ADRESSNO;
                                    existing.POSTLABELCODE = customer.POSTLABELCODE;
                                    existing.SENDERLABELCODE = customer.SENDERLABELCODE;
                                    existing.CLOSEDATECOUNT = customer.CLOSEDATECOUNT;
                                    existing.CLOSEDATETRACK = customer.CLOSEDATETRACK;
                                    existing.DEGACTIVE = customer.DEGACTIVE;
                                    existing.DEGCURR = customer.DEGCURR;
                                    existing.NAME = customer.NAME;
                                    existing.SURNAME = customer.SURNAME;
                                    existing.LABELINFO = customer.LABELINFO;
                                    existing.DEFBNACCREF = customer.DEFBNACCREF;
                                    existing.PROJECTREF = customer.PROJECTREF;
                                    existing.DISCTYPE = customer.DISCTYPE;
                                    existing.SENDMOD = customer.SENDMOD;
                                    existing.ISPERCURR = customer.ISPERCURR;
                                    existing.CURRATETYPE = customer.CURRATETYPE;
                                    existing.INSTEADOFDESP = customer.INSTEADOFDESP;
                                    existing.EINVOICETYP = customer.EINVOICETYP;
                                    existing.FBSSENDMETHOD = customer.FBSSENDMETHOD;
                                    existing.FBSSENDEMAILADDR = customer.FBSSENDEMAILADDR;
                                    existing.FBSSENDFORMAT = customer.FBSSENDFORMAT;
                                    existing.FBSSENDFAXNR = customer.FBSSENDFAXNR;
                                    existing.FBASENDMETHOD = customer.FBASENDMETHOD;
                                    existing.FBASENDEMAILADDR = customer.FBASENDEMAILADDR;
                                    existing.FBASENDFORMAT = customer.FBASENDFORMAT;
                                    existing.FBASENDFAXNR = customer.FBASENDFAXNR;
                                    existing.SECTORMAINREF = customer.SECTORMAINREF;
                                    existing.SECTORSUBREF = customer.SECTORSUBREF;
                                    existing.PERSONELCOSTS = customer.PERSONELCOSTS;
                                    existing.EARCEMAILADDR1 = customer.EARCEMAILADDR1;
                                    existing.EARCEMAILADDR2 = customer.EARCEMAILADDR2;
                                    existing.EARCEMAILADDR3 = customer.EARCEMAILADDR3;
                                    existing.FACTORYDIVNR = customer.FACTORYDIVNR;
                                    existing.FACTORYNR = customer.FACTORYNR;
                                    existing.ININVENNR = customer.ININVENNR;
                                    existing.OUTINVENNR = customer.OUTINVENNR;
                                    existing.QTYDEPDURATION = customer.QTYDEPDURATION;
                                    existing.QTYINDEPDURATION = customer.QTYINDEPDURATION;
                                    existing.OVERLAPTYPE = customer.OVERLAPTYPE;
                                    existing.OVERLAPAMNT = customer.OVERLAPAMNT;
                                    existing.OVERLAPPERC = customer.OVERLAPPERC;
                                    existing.BROKERCOMP = customer.BROKERCOMP;
                                    existing.CREATEWHFICHE = customer.CREATEWHFICHE;
                                    existing.EINVCUSTOM = customer.EINVCUSTOM;
                                    existing.SUBCONT = customer.SUBCONT;
                                    existing.ORDPRIORITY = customer.ORDPRIORITY;
                                    existing.ACCEPTEDESP = customer.ACCEPTEDESP;
                                    existing.PROFILEIDDESP = customer.PROFILEIDDESP;
                                    existing.LABELINFODESP = customer.LABELINFODESP;
                                    existing.POSTLABELCODEDESP = customer.POSTLABELCODEDESP;
                                    existing.SENDERLABELCODEDESP = customer.SENDERLABELCODEDESP;
                                    existing.ACCEPTEINVPUBLIC = customer.ACCEPTEINVPUBLIC;
                                    existing.PUBLICBNACCREF = customer.PUBLICBNACCREF;
                                    existing.PAYMENTPROCBRANCH = customer.PAYMENTPROCBRANCH;
                                    existing.KVKKPERMSTATUS = customer.KVKKPERMSTATUS;
                                    existing.KVKKBEGDATE = customer.KVKKBEGDATE;
                                    existing.KVKKENDDATE = customer.KVKKENDDATE;
                                    existing.KVKKCANCELDATE = customer.KVKKCANCELDATE;
                                    existing.KVKKANONYSTATUS = customer.KVKKANONYSTATUS;
                                    existing.KVKKANONYDATE = customer.KVKKANONYDATE;
                                    existing.CLCCANDEDUCT = customer.CLCCANDEDUCT;
                                    existing.DRIVERREF = customer.DRIVERREF;
                                    existing.EXIMSENDFAXNR = customer.EXIMSENDFAXNR;
                                    existing.EXIMSENDMETHOD = customer.EXIMSENDMETHOD;
                                    existing.EXIMSENDEMAILADDR = customer.EXIMSENDEMAILADDR;
                                    existing.EXIMSENDFORMAT = customer.EXIMSENDFORMAT;
                                    existing.EXIMREGTYPREF = customer.EXIMREGTYPREF;
                                    existing.EXIMNTFYCLREF = customer.EXIMNTFYCLREF;
                                    existing.EXIMCNSLTCLREF = customer.EXIMCNSLTCLREF;
                                    existing.EXIMPAYTYPREF = customer.EXIMPAYTYPREF;
                                    existing.EXIMBRBANKREF = customer.EXIMBRBANKREF;
                                    existing.EXIMCUSTOMREF = customer.EXIMCUSTOMREF;
                                    existing.EXIMFRGHTCLREF = customer.EXIMFRGHTCLREF;
                                    existing.CLSTYPEFORPPAYDT = customer.CLSTYPEFORPPAYDT;
                                    existing.MERSISNO = customer.MERSISNO;
                                    existing.COMMRECORDNO = customer.COMMRECORDNO;
                                    existing.DISPPRINTCNT = customer.DISPPRINTCNT;
                                    existing.ORDPRINTCNT = customer.ORDPRINTCNT;
                                    existing.CLPTYPEFORPPAYDT = customer.CLPTYPEFORPPAYDT;
                                    existing.IMCNTRYREF = customer.IMCNTRYREF;
                                    existing.INCHTELNRS1 = customer.INCHTELNRS1;
                                    existing.INCHTELNRS2 = customer.INCHTELNRS2;
                                    existing.INCHTELNRS3 = customer.INCHTELNRS3;
                                    existing.INCHTELCODES1 = customer.INCHTELCODES1;
                                    existing.INCHTELCODES2 = customer.INCHTELCODES2;
                                    existing.INCHTELCODES3 = customer.INCHTELCODES3;
                                    existing.INCHTELEXTNUMS1 = customer.INCHTELEXTNUMS1;
                                    existing.EXCNTRYTYP = customer.EXCNTRYTYP;
                                    existing.EXCNTRYREF = customer.EXCNTRYREF;
                                    existing.IMCNTRYTYP = customer.IMCNTRYTYP;
                                    existing.INCHTELEXTNUMS2 = customer.INCHTELEXTNUMS2;
                                    existing.INCHTELEXTNUMS3 = customer.INCHTELEXTNUMS3;
                                    existing.NOTIFYCRDREF = customer.NOTIFYCRDREF;
                                    existing.WHATSAPPID = customer.WHATSAPPID;
                                    existing.LINKEDINURL = customer.LINKEDINURL;
                                    existing.INSTAGRAMURL = customer.INSTAGRAMURL;

                                    _context.Customers.Update(existing);
                                    updated++;
                                }
                                else
                                {
                                    _context.Customers.Add(customer);
                                    imported++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"Satır {row.RowNumber()}: {ex.Message}");
                                skipped++;
                                continue;
                            }
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                string resultMsg = $"Yüklenen: {imported}, Güncellenen: {updated}, Atlanan: {skipped}";
                if (errors.Count > 0)
                    resultMsg += $" | Hatalar: {string.Join("; ", errors)}";

                return Json(new { success = true, message = resultMsg });

                //return RedirectToAction(nameof(Index), new { success = true, message = resultMsg });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"İçe aktarım başarısız: {ex.Message}" });

                //ModelState.AddModelError("", $"İçe aktarım sırasında bir hata oluştu: {ex.Message}");
                //return RedirectToAction(nameof(Index), new { success = false, message = $"İçe aktarım başarısız: {ex.Message}" });
            }
        }
    }
}