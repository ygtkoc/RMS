using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalManagementSystem.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dastone.Models;
using Microsoft.AspNetCore.Authorization;
using Dastone.Controllers;
using Microsoft.Extensions.Logging; // ILogger için gerekli

namespace RentalManagementSystem.Controllers
{
    public class HomeController : BaseController
    {
        private readonly RentalDbContext _context;
        private readonly ILogger<HomeController> _logger; // readonly olarak işaretledim

        public HomeController(RentalDbContext context, ILogger<HomeController> logger) : base(context)
        {
            _context = context;
            _logger = logger;
        }
        public async Task<IActionResult> Index()
        {
            var currentDate = DateOnly.FromDateTime(DateTime.Today); 
            var currentDateTime = DateTime.Now; // Bugünün tam tarih/saatini DateTime olarak al

            var viewModel = new DashboardViewModel();

            // 1. Kiralama İstatistikleri
            viewModel.TotalRentalCount = await _context.Kiralamalar.CountAsync();

            viewModel.ActiveRentalCount = await _context.Kiralamalar
                .Where(r => !r.BitisTarihi.HasValue || r.BitisTarihi > currentDate)
                .CountAsync();

            viewModel.CompletedRentalCount = await _context.Kiralamalar
                .Where(r => r.BitisTarihi.HasValue && r.BitisTarihi < currentDate)
                .CountAsync();

            viewModel.TodayStartingRentalCount = await _context.Kiralamalar
                .Where(r => r.BaslangicTarihi == currentDate)
                .CountAsync();

            viewModel.TodayEndingRentalCount = await _context.Kiralamalar
                .Where(r => r.BitisTarihi == currentDate)
                .CountAsync();

            // Aktif Kiralamalar (Son 3) - Detaylı liste için
            // Bu kısım view'de bir tablo veya liste olarak gösterilebilir
            //ViewBag.LatestActiveRentals = await _context.Kiralamalar
            //    .Include(r => r.Arac)
            //    .Include(r => r.Musteri)
            //    .Include(r => r.Lokasyon)
            //    .Where(r => !r.BitisTarihi.HasValue || r.BitisTarihi > currentDate)
            //    .OrderBy(r => r.BaslangicTarihi) // Yaklaşan kiralamalar için mantıklı bir sıralama
            //    .Take(3)
            //    .ToListAsync();

            // 2. Araç Envanteri
            viewModel.TotalVehicleCount = await _context.Araclar.CountAsync();

            // AktifMusteriID alanını kullanarak kiralıkta olan araçları bulma
            viewModel.RentedVehicleCount = await _context.Araclar
                                                .Where(v => v.AktifMusteriID != null)
                                                .CountAsync();
            // Not: AktifMusteriID'nin her zaman güncel olduğundan emin olun.
            // Alternatif olarak, aktif kiralaması olan araç ID'lerini çekip Distinct kullanabilirsiniz:
            // var rentedVehicleIds = await _context.Kiralamalar
            //     .Where(r => !r.BitisTarihi.HasValue || r.BitisTarihi > currentDate)
            //     .Select(r => r.AracID)
            //     .Distinct()
            //     .ToListAsync();
            // viewModel.RentedVehicleCount = rentedVehicleIds.Count;


            viewModel.AvailableVehicleCount = viewModel.TotalVehicleCount - viewModel.RentedVehicleCount;

            // Lokasyona Göre Araç Dağılımı
            viewModel.VehiclesByLocation = await _context.Araclar
                .Include(v => v.Lokasyon)
                .GroupBy(v => v.Lokasyon.LokasyonAdi)
                .Select(g => new DashboardViewModel.LocationVehicleCount { LocationName = g.Key, Count = g.Count() })
                .ToListAsync();

            // En Popüler Araç Modelleri (En çok kiralanan 5 araç)
            viewModel.MostRentedVehicles = await _context.Kiralamalar
                .Include(r => r.Arac)
                .GroupBy(r => r.Arac.Marka + " " + r.Arac.Model)
                .Select(g => new DashboardViewModel.MostRentedVehicle { VehicleName = g.Key, RentalCount = g.Count() })
                .OrderByDescending(x => x.RentalCount)
                .Take(5)
                .ToListAsync();

            // 3. Müşteri İstatistikleri
            viewModel.TotalCustomerCount = await _context.Customers.CountAsync(); // Toplam müşteri sayısı
            DateTime thirtyDaysAgo = currentDateTime.AddDays(-30);
            viewModel.NewCustomersLast30Days = await _context.Customers
                .Where(c => c.CAPIBLOCK_CREADEDDATE >= thirtyDaysAgo)
                .CountAsync();

            viewModel.LatestCustomers = await _context.Customers
                .OrderByDescending(c => c.LOGICALREF) // Veya CreateAt'e göre sıralayın
                .Take(5)
                .Select(c => new DashboardViewModel.CustomerSummary { Title = c.DEFINITION_, Eposta = c.EMAILADDR })
                .ToListAsync();

            // 4. Ceza İstatistikleri
            viewModel.TotalPenaltyCount = await _context.Cezalar.CountAsync();

            viewModel.UnpaidPenaltyCount = await _context.Cezalar
                .Where(c => !c.Odendi)
                .CountAsync();
            viewModel.TotalUnpaidPenaltyAmount = await _context.Cezalar
                .Where(c => !c.Odendi)
                .SumAsync(c => c.Tutar);

            viewModel.PaidPenaltyCount = await _context.Cezalar
                .Where(c => c.Odendi)
                .CountAsync();
            viewModel.TotalPaidPenaltyAmount = await _context.Cezalar
                .Where(c => c.Odendi)
                .SumAsync(c => c.Tutar);


            var rentalTrendData = await _context.Kiralamalar
                .Where(r => r.KayitTarihi.Year >= (currentDateTime.Year - 1)) // Son 2 yılı kapsayabiliriz
                .GroupBy(r => new { r.KayitTarihi.Year, r.KayitTarihi.Month })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            // Aktif Kiralamalar İçin Ay Bazında Veri (Başlangıç tarihi baz alınarak)
            var activeRentalMonthlyData = rentalTrendData
                .Where(x => x.Year == currentDateTime.Year) // Sadece mevcut yıl için
                .ToDictionary(
                    x => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(x.Month),
                    x => x.Count
                );

            foreach (var month in Enumerable.Range(1, 12)) // Tüm ayları döngüye al
            {
                string monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
                viewModel.ActiveRentalMonths.Add(monthName);
                viewModel.ActiveRentalCounts.Add(activeRentalMonthlyData.GetValueOrDefault(monthName, 0));
            }


            // Geçmiş Kiralamalar İçin Ay Bazında Veri (Bitiş tarihi baz alınarak) - Örn: Geçen yılın tamamı
            var pastRentalMonthlyData = rentalTrendData
                .Where(x => x.Year == (currentDateTime.Year - 1)) // Sadece geçen yıl için
                .ToDictionary(
                    x => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(x.Month),
                    x => x.Count
                );

            foreach (var month in Enumerable.Range(1, 12)) // Tüm ayları döngüye al
            {
                string monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
                viewModel.PastRentalMonths.Add(monthName);
                viewModel.PastRentalCounts.Add(pastRentalMonthlyData.GetValueOrDefault(monthName, 0));
            }

            // Eğer isterseniz, geçmiş 12 ayın trendini de gösterebilirsiniz:
            // for (int i = 0; i < 12; i++)
            // {
            //     DateTime targetMonth = currentDateTime.AddMonths(-i);
            //     string monthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(targetMonth.Month) + " " + targetMonth.Year;
            //     viewModel.PastRentalMonths.Insert(0, monthName); // Başa ekleyerek eski tarihten yeni tarihe sırala
            //     int count = rentalTrendData.FirstOrDefault(x => x.Year == targetMonth.Year && x.Month == targetMonth.Month)?.Count ?? 0;
            //     viewModel.PastRentalCounts.Insert(0, count);
            // }


            if (viewModel.ActiveRentalCount == 0)
            {
                _logger.LogInformation("Aktif kiralama bulunamadı. Veritabanında aktif kiralama kontrol edin.");
            }

            return View(viewModel); // ViewModel'i View'e gönder
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult Widgets()
        {
            return View();
        }

        public IActionResult Sales()
        {
            return View();
        }
    }
}