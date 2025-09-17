using Dastone.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

namespace RentalManagementSystem.Data
{
    public class RentalDbContext : DbContext
    {
        public RentalDbContext(DbContextOptions<RentalDbContext> options) : base(options) { }

        // DbSets
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Lokasyon> Lokasyonlar { get; set; }
        public DbSet<Vehicle> Araclar { get; set; }
        public DbSet<Rental> Kiralamalar { get; set; }
        public DbSet<RentalDocument> KiralamaSozlesmeleri { get; set; }
        public DbSet<Ceza> Cezalar { get; set; }
        public DbSet<OtoyolGecisi> OtoyolGecisleri { get; set; }
        public DbSet<CezaTanimi> CezaTanimlari { get; set; }
        public DbSet<AracTipiTanimi> AracTipiTanimi { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

        // Yeni
        public DbSet<CompanyInfo> FirmaBilgileri { get; set; }
        public DbSet<Invoice> Faturalar { get; set; }
        public DbSet<InvoiceItem> FaturaKalemleri { get; set; }
        public DbSet<InvoiceFile> FaturaDosyalari { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // DateOnly dönüştürücüleri
            var dateOnlyConverter = new ValueConverter<DateOnly, DateTime>(
                v => v.ToDateTime(TimeOnly.MinValue),
                v => DateOnly.FromDateTime(v));

            var nullableDateOnlyConverter = new ValueConverter<DateOnly?, DateTime?>(
                v => v.HasValue ? v.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                v => v.HasValue ? DateOnly.FromDateTime(v.Value) : (DateOnly?)null);

            // Vehicle -> AracTipi
            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.AracTipiTanimi)
                .WithMany(at => at.Araclar)
                .HasForeignKey(v => v.AracTipiID)
                .OnDelete(DeleteBehavior.SetNull);

            // User -> Role
            modelBuilder.Entity<User>()
                .Property(u => u.RoleId)
                .HasColumnName("RoleID");

            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .HasConstraintName("FK_Users_Roles")
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // PasswordResetToken -> User
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserID)
                .HasConstraintName("FK_PasswordResetTokens_Users")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(true);

            modelBuilder.Entity<User>()
                .Property(u => u.ProfilePicturePath)
                .HasMaxLength(255)
                .IsRequired(false);

            modelBuilder.Entity<User>()
                .Property(u => u.IsTwoFactorEnabled)
                .HasDefaultValue(false);

            // InvoiceItem
            modelBuilder.Entity<InvoiceItem>(e =>
            {
                e.Property(x => x.VatRate).HasPrecision(18, 4);
            });

            // Vehicle numeric alanlar
            modelBuilder.Entity<Vehicle>(e =>
            {
                e.Property(x => x.AracAlisFiyati).HasPrecision(18, 4);
                e.Property(x => x.AracBedeli).HasPrecision(18, 4);
                e.Property(x => x.KiralamaBedeli).HasPrecision(18, 4);
            });

            // Customer temel eşleme (tablo adı + PK)
            modelBuilder.Entity<Customer>(e =>
            {
                e.ToTable("Customers");
                e.HasKey(c => c.LOGICALREF);
                // Burada property-type override yapmıyoruz; CAST hatasından kaçınmak için
                // sorgularda "projeksiyon" kullanacağız.
            });

            // Rental
            modelBuilder.Entity<Rental>(e =>
            {
                e.ToTable("Kiralamalar");
                e.HasKey(k => k.KiralamaID);

                e.HasOne(k => k.Musteri)
                 .WithMany(c => c.Kiralamalar)
                 .HasForeignKey(k => k.MusteriID)
                 .HasPrincipalKey(c => c.LOGICALREF)
                 .OnDelete(DeleteBehavior.Restrict)
                 .HasConstraintName("FK_Kiralamalar_Customers");
            });

            // Vehicle -> AktifMusteri
            modelBuilder.Entity<Vehicle>(e =>
            {
                e.ToTable("Araclar");
                e.HasKey(v => v.AracID);

                e.HasOne(v => v.AktifMusteri)
                 .WithMany(c => c.Araclar)
                 .HasForeignKey(v => v.AktifMusteriID)
                 .HasPrincipalKey(c => c.LOGICALREF)
                 .OnDelete(DeleteBehavior.Restrict)
                 .HasConstraintName("FK_Araclar_Customers");
            });

            // Ceza -> Customer
            modelBuilder.Entity<Ceza>(e =>
            {
                e.ToTable("Cezalar");
                e.HasOne(cz => cz.Musteri)
                 .WithMany(c => c.Cezalar)
                 .HasForeignKey(cz => cz.MusteriID)
                 .HasPrincipalKey(c => c.LOGICALREF)
                 .OnDelete(DeleteBehavior.Restrict)
                 .HasConstraintName("FK_Cezalar_Customers");
            });

            // OtoyolGecisi -> Customer
            modelBuilder.Entity<OtoyolGecisi>(e =>
            {
                e.ToTable("OtoyolGecisleri");
                e.HasOne(og => og.Musteri)
                 .WithMany(c => c.OtoyolGecisleri)
                 .HasForeignKey(og => og.MusteriID)
                 .HasPrincipalKey(c => c.LOGICALREF)
                 .OnDelete(DeleteBehavior.Restrict)
                 .HasConstraintName("FK_OtoyolGecisleri_Customers");
            });

            // Rental DateOnly kolonları
            modelBuilder.Entity<Rental>()
                .Property(r => r.BaslangicTarihi)
                .HasConversion(dateOnlyConverter)
                .HasColumnType("date");

            modelBuilder.Entity<Rental>()
                .Property(r => r.BitisTarihi)
                .HasConversion(nullableDateOnlyConverter)
                .HasColumnType("date");

            modelBuilder.Entity<Lokasyon>()
                .HasIndex(x => x.LokasyonAdi)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}
