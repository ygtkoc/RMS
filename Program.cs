using Dastone.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using RentalManagementSystem.Data;
using System.IO; // Path.Combine ve Directory.GetCurrentDirectory i�in EKLEND�
using System.Reflection; // EKLEND�
using System.Runtime.Loader; // EKLEND�

var builder = WebApplication.CreateBuilder(args);

// Session servislerini ekle
builder.Services.AddDistributedMemoryCache(); // Bellek i�i session saklama
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Oturum zaman a��m�
    options.Cookie.HttpOnly = true; // G�venlik i�in
    options.Cookie.IsEssential = true; // GDPR uyumlulu�u
});

// Add services to the container.
builder.Services.AddHttpClient<AccountController>();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();


builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

builder.Services.AddDbContext<RentalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthorization();



var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Bir hata oluştu. Lütfen daha sonra tekrar deneyin.");
    });
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();


// Kimlik do�rulama ve yetkilendirme middleware'lerini ekle
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();