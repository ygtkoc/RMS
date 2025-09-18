using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc.Rendering;
using DataType = System.ComponentModel.DataAnnotations.DataType;


namespace Dastone.Models
{
    [Table("Roles")]
    public class Role
    {
        [Key]
        public int RoleID { get; set; }

        [Required(ErrorMessage = "Rol adı zorunludur.")]
        [StringLength(50, ErrorMessage = "Rol adı 50 karakterden uzun olamaz.")]
        public string RoleName { get; set; }

        public virtual ICollection<User> Users { get; set; }

        public Role()
        {
            Users = new List<User>();
        }
    }

    [Table("Users")]
    public class User
    {
        [Key]
        public int UserID { get; set; }

        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [StringLength(50, ErrorMessage = "Kullanıcı adı 50 karakterden uzun olamaz.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(256, ErrorMessage = "Şifre 256 karakterden uzun olamaz.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "E-posta zorunludur.")]
        [StringLength(100, ErrorMessage = "E-posta 100 karakterden uzun olamaz.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; }

        [StringLength(50, ErrorMessage = "Ad 50 karakterden uzun olamaz.")]
        public string FirstName { get; set; }

        [StringLength(50, ErrorMessage = "Soyad 50 karakterden uzun olamaz.")]
        public string? LastName { get; set; }

        [Column("RoleID")]
        [Required(ErrorMessage = "Rol ID'si zorunludur.")]
        public int? RoleId { get; set; }

        [ForeignKey(nameof(RoleId))]
        public virtual Role? Role { get; set; }

        [Required(ErrorMessage = "Oluşturulma tarihi zorunludur.")]
        public DateTime CreatedAt { get; set; }

        public string? phoneNumber { get; set; }
        public string? ProfilePicturePath { get; set; } // Yeni alan: Profil fotoğrafı yolu

        public bool IsTwoFactorEnabled { get; set; } = false; // Yeni alan: 2FA durumu

        public bool? ReceiveEmailNotifications { get; set; } = true; // Varsayılan: açık
        public bool? ReceiveSMSNotifications { get; set; } = false; // Varsayılan: kapalı
        public string? PreferredLanguage { get; set; } = "tr"; // Varsayılan: Türkçe
        public string? ThemePreference { get; set; } = "light"; // Varsayılan: Açık tema
        public int SessionTimeoutMinutes { get; set; } = 30; // Varsayılan: 30 dakika
    }
    
        public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        [StringLength(100)]
        public string Token { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        [ForeignKey("UserID")]
        public User User { get; set; }
    }

    [Table("Customers")]
    public class Customer
    {
        [Key]
        public int LOGICALREF { get; set; }

        public string? NAME { get; set; }
        public string? SURNAME { get; set; }

        // Identity Numbers
        public string? TCKNO { get; set; }

        // Veritabanında string olarak tutulan alan
        public string? ISPERSCOMP { get; set; }

        // Kodda bool? olarak kullanmak için:
        [NotMapped]
        public bool? IsPersonalCompany
        {
            get
            {
                if (string.IsNullOrEmpty(ISPERSCOMP)) return null;
                var v = ISPERSCOMP.Trim().ToLowerInvariant();
                if (v == "1" || v == "true") return true;
                if (v == "0" || v == "false") return false;
                return null;
            }
            set
            {
                if (value.HasValue)
                    ISPERSCOMP = value.Value ? "1" : "0";
                else
                    ISPERSCOMP = null;
            }
        }

        // Tax Information
        public string? TAXNR { get; set; }
        public string? TAXOFFICE { get; set; }
        public string? TAXOFFCODE { get; set; }
        public string? VATNR { get; set; }

        // Contact Information
        [Required(ErrorMessage = "E-posta zorunludur.")]
        [StringLength(100, ErrorMessage = "E-posta 100 karakterden uzun olamaz.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string? EMAILADDR { get; set; }
        public string? EMAILADDR2 { get; set; }
        public string? EMAILADDR3 { get; set; }

        public string? TELNRS1 { get; set; }
        public string? TELNRS2 { get; set; }
        public string? TELCODES1 { get; set; }
        public string? TELCODES2 { get; set; }
        public string? TELEXTNUMS1 { get; set; }
        public string? TELEXTNUMS2 { get; set; }
        public string? FAXNR { get; set; }
        public string? FAXCODE { get; set; }
        public string? FAXEXTNUM { get; set; }
        public string? CELLPHONE { get; set; }

        public string? ADDR1 { get; set; }
        public string? ADDR2 { get; set; }
        public string? CITY { get; set; }
        public string? CITYCODE { get; set; }
        public string? CITYID { get; set; }
        public string? TOWN { get; set; }
        public string? TOWNCODE { get; set; }
        public string? TOWNID { get; set; }
        public string? DISTRICT { get; set; }
        public string? DISTRICTCODE { get; set; }
        public string? COUNTRY { get; set; }
        public string? COUNTRYCODE { get; set; }
        public string? POSTCODE { get; set; }
        public string? ADRESSNO { get; set; }
        public string? STATECODE { get; set; }
        public string? STATENAME { get; set; }

        // E-Invoice Information
        //public string? E_Fatura_Mukellef { get; set; }
        public bool? ACCEPTEINV { get; set; }
        public string? EINVOICEID { get; set; }
        public string? PROFILEID { get; set; }
        public string? EINVOICETYPE { get; set; }
        public string? EINVOICETYP { get; set; }

        // Person In Charge
        public string? INCHARGE { get; set; }
        public string? INCHARGE2 { get; set; }
        public string? INCHARGE3 { get; set; }
        public string? INCHTELNRS1 { get; set; }
        public string? INCHTELNRS2 { get; set; }
        public string? INCHTELNRS3 { get; set; }
        public string? INCHTELCODES1 { get; set; }
        public string? INCHTELCODES2 { get; set; }
        public string? INCHTELCODES3 { get; set; }
        public string? INCHTELEXTNUMS1 { get; set; }
        public string? INCHTELEXTNUMS2 { get; set; }
        public string? INCHTELEXTNUMS3 { get; set; }

        // Bank Information
        public string? BANKNAMES1 { get; set; }
        public string? BANKNAMES2 { get; set; }
        public string? BANKNAMES3 { get; set; }
        public string? BANKNAMES4 { get; set; }
        public string? BANKNAMES5 { get; set; }
        public string? BANKNAMES6 { get; set; }
        public string? BANKNAMES7 { get; set; }
        public string? BANKBRANCHS1 { get; set; }
        public string? BANKBRANCHS2 { get; set; }
        public string? BANKBRANCHS3 { get; set; }
        public string? BANKBRANCHS4 { get; set; }
        public string? BANKBRANCHS5 { get; set; }
        public string? BANKBRANCHS6 { get; set; }
        public string? BANKBRANCHS7 { get; set; }
        public string? BANKACCOUNTS1 { get; set; }
        public string? BANKACCOUNTS2 { get; set; }
        public string? BANKACCOUNTS3 { get; set; }
        public string? BANKACCOUNTS4 { get; set; }
        public string? BANKACCOUNTS5 { get; set; }
        public string? BANKACCOUNTS6 { get; set; }
        public string? BANKACCOUNTS7 { get; set; }
        public string? BANKIBANS1 { get; set; }
        public string? BANKIBANS2 { get; set; }
        public string? BANKIBANS3 { get; set; }
        public string? BANKIBANS4 { get; set; }
        public string? BANKIBANS5 { get; set; }
        public string? BANKIBANS6 { get; set; }
        public string? BANKIBANS7 { get; set; }
        public string? BANKBICS1 { get; set; }
        public string? BANKBICS2 { get; set; }
        public string? BANKBICS3 { get; set; }
        public string? BANKBICS4 { get; set; }
        public string? BANKBICS5 { get; set; }
        public string? BANKBICS6 { get; set; }
        public string? BANKBICS7 { get; set; }
        public string? BANKBCURRENCY1 { get; set; }
        public string? BANKBCURRENCY2 { get; set; }
        public string? BANKBCURRENCY3 { get; set; }
        public string? BANKBCURRENCY4 { get; set; }
        public string? BANKBCURRENCY5 { get; set; }
        public string? BANKBCURRENCY6 { get; set; }
        public string? BANKBCURRENCY7 { get; set; }
        public string? BANKCORRPACC1 { get; set; }
        public string? BANKCORRPACC2 { get; set; }
        public string? BANKCORRPACC3 { get; set; }
        public string? BANKCORRPACC4 { get; set; }
        public string? BANKCORRPACC5 { get; set; }
        public string? BANKCORRPACC6 { get; set; }
        public string? BANKCORRPACC7 { get; set; }
        public string? BANKVOEN1 { get; set; }
        public string? BANKVOEN2 { get; set; }
        public string? BANKVOEN3 { get; set; }
        public string? BANKVOEN4 { get; set; }
        public string? BANKVOEN5 { get; set; }
        public string? BANKVOEN6 { get; set; }
        public string? BANKVOEN7 { get; set; }

        // Web and Social Media
        public string? WEBADDR { get; set; }
        public string? FACEBOOKURL { get; set; }
        public string? TWITTERURL { get; set; }
        public string? LINKEDINURL { get; set; }
        public string? INSTAGRAMURL { get; set; }
        public string? WHATSAPPID { get; set; }
        public string? APPLEID { get; set; }
        public string? SKYPEID { get; set; }

        // Business Information
        public string? CODE { get; set; }
        public string? DEFINITION_ { get; set; }
        public string? DEFINITION2 { get; set; }
        public string? SPECODE { get; set; }
        public string? SPECODE2 { get; set; }
        public string? SPECODE3 { get; set; }
        public string? SPECODE4 { get; set; }
        public string? SPECODE5 { get; set; }
        public string? CYPHCODE { get; set; }
        public string? CARDTYPE { get; set; }

        public bool? ACTIVE { get; set; }
        public bool? BLOCKED { get; set; }
        public string? CLANGUAGE { get; set; }
        public string? MERSISNO { get; set; }
        public string? COMMRECORDNO { get; set; }

        // Document Information
        public int? DISPPRINTCNT { get; set; }
        public int? INVPRINTCNT { get; set; }
        public int? ORDPRINTCNT { get; set; }

        // Financial Information
        public decimal? DISCRATE { get; set; }
        public string? DISCTYPE { get; set; }
        public string? CCURRENCY { get; set; }
        public string? ISPERCURR { get; set; }
        public string? CURRATETYPE { get; set; }
        public decimal? DBSLIMIT1 { get; set; }
        public decimal? DBSLIMIT2 { get; set; }
        public decimal? DBSLIMIT3 { get; set; }
        public decimal? DBSLIMIT4 { get; set; }
        public decimal? DBSLIMIT5 { get; set; }
        public decimal? DBSLIMIT6 { get; set; }
        public decimal? DBSLIMIT7 { get; set; }
        public decimal? DBSTOTAL1 { get; set; }
        public decimal? DBSTOTAL2 { get; set; }
        public decimal? DBSTOTAL3 { get; set; }
        public decimal? DBSTOTAL4 { get; set; }
        public decimal? DBSTOTAL5 { get; set; }
        public decimal? DBSTOTAL6 { get; set; }
        public decimal? DBSTOTAL7 { get; set; }

        // Geographic Information
        public string? MAPID { get; set; }
        public decimal? LONGITUDE { get; set; }
        public decimal? LATITUTE { get; set; }

        // System Fields
        public string? GLOBALID { get; set; }
        public string? GUID { get; set; }
        public int? EXTENREF { get; set; }
        public int? PAYMENTREF { get; set; }
        public int? CASHREF { get; set; }
        public int? DEFBNACCREF { get; set; }
        public int? PROJECTREF { get; set; }
        //public DateTime? CreatedAt { get; set; }
        public int? CAPIBLOCK_CREATEDBY { get; set; }
        public DateTime? CAPIBLOCK_CREADEDDATE { get; set; }
        public int? CAPIBLOCK_CREATEDHOUR { get; set; }
        public int? CAPIBLOCK_CREATEDMIN { get; set; }
        public int? CAPIBLOCK_CREATEDSEC { get; set; }
        public int? CAPIBLOCK_MODIFIEDBY { get; set; }
        public DateTime? CAPIBLOCK_MODIFIEDDATE { get; set; }
        public int? CAPIBLOCK_MODIFIEDHOUR { get; set; }
        public int? CAPIBLOCK_MODIFIEDMIN { get; set; }
        public int? CAPIBLOCK_MODIFIEDSEC { get; set; }

        // Eksik Alanlar Eklenerek Tamamlandı
        public int? WARNMETHOD { get; set; }
        public string? WARNEMAILADDR { get; set; }
        public string? WARNFAXNR { get; set; }
        public int? DELIVERYMETHOD { get; set; }
        public int? DELIVERYFIRM { get; set; }
        public int? TEXTINC { get; set; }
        public int? SITEID { get; set; }
        public int? RECSTATUS { get; set; }
        public int? ORGLOGICREF { get; set; }
        public string? EDINO { get; set; }
        public string? TRADINGGRP { get; set; }
        public int? PAYMENTPROC { get; set; }
        public int? CRATEDIFFPROC { get; set; }
        public int? WFSTATUS { get; set; }
        public string? PPGROUPCODE { get; set; }
        public int? PPGROUPREF { get; set; }
        public int? ORDSENDMETHOD { get; set; }
        public string? ORDSENDEMAILADDR { get; set; }
        public string? ORDSENDFAXNR { get; set; }
        public int? DSPSENDMETHOD { get; set; }
        public string? DSPSENDEMAILADDR { get; set; }
        public string? DSPSENDFAXNR { get; set; }
        public int? INVSENDMETHOD { get; set; }
        public string? INVSENDEMAILADDR { get; set; }
        public string? INVSENDFAXNR { get; set; }
        public int? SUBSCRIBERSTAT { get; set; }
        public int? SUBSCRIBEREXT { get; set; }
        public int? AUTOPAIDBANK { get; set; }
        public int? PAYMENTTYPE { get; set; }
        public int? LASTSENDREMLEV { get; set; }
        public int? EXTACCESSFLAGS { get; set; }
        public int? ORDSENDFORMAT { get; set; }
        public int? DSPSENDFORMAT { get; set; }
        public int? INVSENDFORMAT { get; set; }
        public int? REMSENDFORMAT { get; set; }
        public string? STORECREDITCARDNO { get; set; }
        public int? CLORDFREQ { get; set; }
        public int? ORDDAY { get; set; }
        public string? LOGOID { get; set; }
        public int? LIDCONFIRMED { get; set; }
        public string? EXPREGNO { get; set; }
        public string? EXPDOCNO { get; set; }
        public int? EXPBUSTYPREF { get; set; }
        public int? PIECEORDINFLICT { get; set; }
        public int? COLLECTINVOICING { get; set; }
        public int? EBUSDATASENDTYPE { get; set; }
        public int? INISTATUSFLAGS { get; set; }
        public int? SLSORDERSTATUS { get; set; }
        public int? SLSORDERPRICE { get; set; }
        public int? LTRSENDMETHOD { get; set; }
        public string? LTRSENDEMAILADDR { get; set; }
        public string? LTRSENDFAXNR { get; set; }
        public int? LTRSENDFORMAT { get; set; }
        public int? IMAGEINC { get; set; }
        public int? SAMEITEMCODEUSE { get; set; }
        public int? WFLOWCRDREF { get; set; }
        public int? PARENTCLREF { get; set; }
        public string? LOWLEVELCODES1 { get; set; }
        public string? LOWLEVELCODES2 { get; set; }
        public string? LOWLEVELCODES3 { get; set; }
        public string? LOWLEVELCODES4 { get; set; }
        public string? LOWLEVELCODES5 { get; set; }
        public string? LOWLEVELCODES6 { get; set; }
        public string? LOWLEVELCODES7 { get; set; }
        public string? LOWLEVELCODES8 { get; set; }
        public string? LOWLEVELCODES9 { get; set; }
        public string? LOWLEVELCODES10 { get; set; }
        public int? PURCHBRWS { get; set; }
        public int? SALESBRWS { get; set; }
        public int? IMPBRWS { get; set; }
        public int? EXPBRWS { get; set; }
        public int? FINBRWS { get; set; }
        public int? ORGLOGOID { get; set; }
        public int? ADDTOREFLIST { get; set; }
        public int? TEXTREFTR { get; set; }
        public int? TEXTREFEN { get; set; }
        public int? ARPQUOTEINC { get; set; }
        public int? CLCRM { get; set; }
        public int? GRPFIRMNR { get; set; }
        public int? CONSCODEREF { get; set; }
        public int? OFFSENDMETHOD { get; set; }
        public string? OFFSENDEMAILADDR { get; set; }
        public string? OFFSENDFAXNR { get; set; }
        public int? OFFSENDFORMAT { get; set; }
        public string? EBANKNO { get; set; }
        public int? LOANGRPCTRL { get; set; }
        public int? LDXFIRMNR { get; set; }
        public int? EXTSENDMETHOD { get; set; }
        public string? EXTSENDEMAILADDR { get; set; }
        public string? EXTSENDFAXNR { get; set; }
        public int? EXTSENDFORMAT { get; set; }
        public int? USEDINPERIODS { get; set; }
        public int? RSKLIMCR { get; set; }
        public int? RSKDUEDATECR { get; set; }
        public int? RSKAGINGCR { get; set; }
        public int? RSKAGINGDAY { get; set; }
        public int? PURCORDERSTATUS { get; set; }
        public int? PURCORDERPRICE { get; set; }
        public int? ISFOREIGN { get; set; }
        public int? SHIPBEGTIME1 { get; set; }
        public string? SHIPBEGTIME2 { get; set; }
        public string? SHIPBEGTIME3 { get; set; }
        public int? SHIPENDTIME1 { get; set; }
        public string? SHIPENDTIME2 { get; set; }
        public string? SHIPENDTIME3 { get; set; }
        public string? DBSBANKNO1 { get; set; }
        public string? DBSBANKNO2 { get; set; }
        public string? DBSBANKNO3 { get; set; }
        public string? DBSBANKNO4 { get; set; }
        public string? DBSBANKNO5 { get; set; }
        public string? DBSBANKNO6 { get; set; }
        public string? DBSBANKNO7 { get; set; }
        public int? DBSRISKCNTRL1 { get; set; }
        public int? DBSRISKCNTRL2 { get; set; }
        public int? DBSRISKCNTRL3 { get; set; }
        public int? DBSRISKCNTRL4 { get; set; }
        public int? DBSRISKCNTRL5 { get; set; }
        public int? DBSRISKCNTRL6 { get; set; }
        public int? DBSRISKCNTRL7 { get; set; }
        public int? DBSBANKCURRENCY1 { get; set; }
        public int? DBSBANKCURRENCY2 { get; set; }
        public int? DBSBANKCURRENCY3 { get; set; }
        public int? DBSBANKCURRENCY4 { get; set; }
        public int? DBSBANKCURRENCY5 { get; set; }
        public int? DBSBANKCURRENCY6 { get; set; }
        public int? DBSBANKCURRENCY7 { get; set; }
        public int? DUEDATECOUNT { get; set; }
        public int? DUEDATELIMIT { get; set; }
        public int? DUEDATETRACK { get; set; }
        public int? DUEDATECONTROL1 { get; set; }
        public int? DUEDATECONTROL2 { get; set; }
        public int? DUEDATECONTROL3 { get; set; }
        public int? DUEDATECONTROL4 { get; set; }
        public int? DUEDATECONTROL5 { get; set; }
        public int? DUEDATECONTROL6 { get; set; }
        public int? DUEDATECONTROL7 { get; set; }
        public int? DUEDATECONTROL8 { get; set; }
        public int? DUEDATECONTROL9 { get; set; }
        public int? DUEDATECONTROL10 { get; set; }
        public int? DUEDATECONTROL11 { get; set; }
        public int? DUEDATECONTROL12 { get; set; }
        public int? DUEDATECONTROL13 { get; set; }
        public int? DUEDATECONTROL14 { get; set; }
        public int? DUEDATECONTROL15 { get; set; }
        public string? POSTLABELCODE { get; set; }
        public string? SENDERLABELCODE { get; set; }
        public int? CLOSEDATECOUNT { get; set; }
        public int? CLOSEDATETRACK { get; set; }
        public int? DEGACTIVE { get; set; }
        public int? DEGCURR { get; set; }
        public string? LABELINFO { get; set; }
        public int? SENDMOD { get; set; }
        public int? INSTEADOFDESP { get; set; }
        public int? FBSSENDMETHOD { get; set; }
        public string? FBSSENDEMAILADDR { get; set; }
        public int? FBSSENDFORMAT { get; set; }
        public string? FBSSENDFAXNR { get; set; }
        public int? FBASENDMETHOD { get; set; }
        public string? FBASENDEMAILADDR { get; set; }
        public int? FBASENDFORMAT { get; set; }
        public string? FBASENDFAXNR { get; set; }
        public int? SECTORMAINREF { get; set; }
        public int? SECTORSUBREF { get; set; }
        public int? PERSONELCOSTS { get; set; }
        public string? EARCEMAILADDR1 { get; set; }
        public string? EARCEMAILADDR2 { get; set; }
        public string? EARCEMAILADDR3 { get; set; }
        public int? FACTORYDIVNR { get; set; }
        public int? FACTORYNR { get; set; }
        public int? ININVENNR { get; set; }
        public int? OUTINVENNR { get; set; }
        public int? QTYDEPDURATION { get; set; }
        public int? QTYINDEPDURATION { get; set; }
        public int? OVERLAPTYPE { get; set; }
        public decimal? OVERLAPAMNT { get; set; }
        public decimal? OVERLAPPERC { get; set; }
        public int? BROKERCOMP { get; set; }
        public int? CREATEWHFICHE { get; set; }
        public int? EINVCUSTOM { get; set; }
        public int? SUBCONT { get; set; }
        public int? ORDPRIORITY { get; set; }
        public int? ACCEPTEDESP { get; set; }
        public string? PROFILEIDDESP { get; set; }
        public string? LABELINFODESP { get; set; }
        public string? POSTLABELCODEDESP { get; set; }
        public string? SENDERLABELCODEDESP { get; set; }
        public string? ACCEPTEINVPUBLIC { get; set; }
        public int? PUBLICBNACCREF { get; set; }
        public int? PAYMENTPROCBRANCH { get; set; }
        public int? KVKKPERMSTATUS { get; set; }
        public DateTime? KVKKBEGDATE { get; set; }
        public DateTime? KVKKENDDATE { get; set; }
        public DateTime? KVKKCANCELDATE { get; set; }
        public int? KVKKANONYSTATUS { get; set; }
        public DateTime? KVKKANONYDATE { get; set; }
        public int? CLCCANDEDUCT { get; set; }
        public int? DRIVERREF { get; set; }
        public string? EXIMSENDFAXNR { get; set; }
        public int? EXIMSENDMETHOD { get; set; }
        public string? EXIMSENDEMAILADDR { get; set; }
        public int? EXIMSENDFORMAT { get; set; }
        public int? EXIMREGTYPREF { get; set; }
        public int? EXIMNTFYCLREF { get; set; }
        public int? EXIMCNSLTCLREF { get; set; }
        public int? EXIMPAYTYPREF { get; set; }
        public int? EXIMBRBANKREF { get; set; }
        public int? EXIMCUSTOMREF { get; set; }
        public int? EXIMFRGHTCLREF { get; set; }
        public int? CLSTYPEFORPPAYDT { get; set; }
        public int? CLPTYPEFORPPAYDT { get; set; }
        public int? IMCNTRYREF { get; set; }
        public int? EXCNTRYTYP { get; set; }
        public int? EXCNTRYREF { get; set; }
        public int? IMCNTRYTYP { get; set; }
        public int? NOTIFYCRDREF { get; set; }

        // Navigation Properties
        public virtual ICollection<Rental>? Kiralamalar { get; set; }
        public virtual ICollection<Vehicle>? Araclar { get; set; }
        public virtual ICollection<Ceza>? Cezalar { get; set; }
        public virtual ICollection<OtoyolGecisi>? OtoyolGecisleri { get; set; }
        public virtual ICollection<Invoice>? Invoices { get; set; }

        public Customer()
        {
            Kiralamalar = new List<Rental>();
            Araclar = new List<Vehicle>();
            Cezalar = new List<Ceza>();
            OtoyolGecisleri = new List<OtoyolGecisi>();
            Invoices = new List<Invoice>();
        }
    }


    [Table("FirmaBilgileri")]
    public class CompanyInfo
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Firma Adı zorunludur.")]
        [StringLength(200)]
        public string CompanyName { get; set; }

        [StringLength(200)]
        public string Address { get; set; }

        [StringLength(50)]
        public string City { get; set; }

        [StringLength(50)]
        public string District { get; set; } // İlçe

        [StringLength(20)]
        public string TaxNumber { get; set; } // Vergi Numarası

        [StringLength(100)]
        public string TaxOffice { get; set; } // Vergi Dairesi

        [StringLength(20)]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; }

        [StringLength(20)]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
        public string Phone { get; set; }

        [StringLength(200)]
        public string Website { get; set; }

        [StringLength(20)]
        public string TradeRegistryNumber { get; set; } // Ticaret Sicil No (Opsiyonel)

        [StringLength(50)]
        public string MersisNo { get; set; } // Mersis No (Opsiyonel)

        // Banka Bilgileri (Örnek: IBAN'lar için ayrı bir tablo da düşünülebilir)
        [StringLength(100)]
        public string BankName1 { get; set; }
        [StringLength(50)]
        public string BankBranch1 { get; set; }
        [StringLength(50)]
        public string BankAccountName1 { get; set; }
        [StringLength(34)] // IBAN max 34 karakter
        public string Iban1 { get; set; }

        [StringLength(100)]
        public string? BankName2 { get; set; }
        [StringLength(50)]
        public string? BankBranch2 { get; set; }
        [StringLength(50)]
        public string? BankAccountName2 { get; set; }
        [StringLength(34)]
        public string? Iban2 { get; set; }

        // Diğer ek bilgiler (örneğin e-fatura entegrasyonu için gerekli kodlar)
        [StringLength(50)]
        public string? EInvoiceProfileId { get; set; } // Örn: TEMELFATURA
        [StringLength(50)]
        public string? EInvoiceType { get; set; } // Örn: SATIŞ
        // ... XML'deki diğer sabit veya firma ile ilişkili alanlar eklenebilir.
    }

    [Table("Faturalar")]
    public class Invoice
    {
        [Key]
        public int InvoiceID { get; set; }

        [Required(ErrorMessage = "Fatura Numarası zorunludur.")]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } // Örn: GRB2024000000140

        [Required(ErrorMessage = "Fatura Türü zorunludur.")]
        [StringLength(20)]
        public string InvoiceType { get; set; } // Örn: "Satis", "Iade", vb. (XML'deki TYPE alanına karşılık gelebilir)

        [Required(ErrorMessage = "Fatura Tarihi zorunludur.")]
        public DateTime InvoiceDate { get; set; }

        public DateTime? IssueDate { get; set; } // Düzenlenme Tarihi (Görseldeki "Oluşturulma Tarihi")
        public TimeSpan? IssueTime { get; set; } // Düzenlenme Saati (Görseldeki "Oluşturulma Saati")

        // Müşteri Bilgileri
        [Required(ErrorMessage = "Müşteri ID'si zorunludur.")]
        public int LOGICALREF { get; set; }
        [ForeignKey("LOGICALREF")]
        public virtual Customer Customer { get; set; }

        // Firma Bilgileri (Hangi firmanın kestiğini belirtmek için, birden fazla firma olabilir)
        public int? CompanyInfoID { get; set; }
        [ForeignKey("CompanyInfoID")]
        public virtual CompanyInfo CompanyInfo { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalDiscounted { get; set; } // İskontolu Toplam (XML'deki TOTAL_DISCOUNTED)

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalGross { get; set; } // Brüt Toplam (XML'deki TOTAL_GROSS)

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalNet { get; set; } // Net Toplam (XML'deki TOTAL_NET)

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalVat { get; set; } // Toplam KDV

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmountDue { get; set; } // Ödenecek Tutar (KDV Dahil)

        [StringLength(500)]
        public string Notes { get; set; } // Genel Açıklamalar (XML'deki NOTES1)

        [StringLength(50)]
        public string EInvoiceGuid { get; set; } // XML'deki GUID

        [StringLength(50)]
        public string EInvoiceStatus { get; set; } // E-fatura durumu (taslak, gönderildi, kabul edildi, vb.)

        // Navigasyon Özellikleri
        public virtual ICollection<InvoiceItem> Items { get; set; }
        public virtual ICollection<InvoiceFile> Files { get; set; }

        public Invoice()
        {
            Items = new List<InvoiceItem>();
            Files = new List<InvoiceFile>();
        }
    }

    [Table("FaturaKalemleri")]
    public class InvoiceItem
    {
        [Key]
        public int InvoiceItemID { get; set; }

        [Required]
        public int InvoiceID { get; set; }
        [ForeignKey("InvoiceID")]
        public virtual Invoice Invoice { get; set; }

        [StringLength(50)]
        public string ItemCode { get; set; } // Kalem Kodu (XML'deki MASTER_CODE)

        [StringLength(500)]
        public string Description { get; set; } // Kalem Açıklaması (XML'deki DESCRIPTION)

        [Column(TypeName = "decimal(18, 4)")] // Miktar için daha fazla ondalık basamak
        public decimal Quantity { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } // Birim (ADET)

        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; } // Birim Fiyat

        [Column(TypeName = "decimal(18, 2)")]
        public decimal LineTotal { get; set; } // Satır Toplamı (İskontosuz, KDV'siz)


        [Column(TypeName = "decimal(18, 2)")]
        public decimal? DiscountAmount { get; set; } // İskonto Tutarı (Varsa)

        public decimal VatRate { get; set; } // KDV Oranı (Örn: 18)

        [Column(TypeName = "decimal(18, 2)")]
        public decimal VatAmount { get; set; } // KDV Tutarı

        [StringLength(100)]
        public string VatExemptionReason { get; set; } // KDV Muafiyet Nedeni (XML'deki VATEXCEPT_REASON)

        [StringLength(20)]
        public string VatExemptionCode { get; set; } // KDV Muafiyet Kodu (XML'deki VATEXCEPT_CODE)

        // Hangi ceza veya geçiş ile ilişkili olduğunu belirtmek için (opsiyonel)
        public int? CezaID { get; set; }
        [ForeignKey("CezaID")]
        public virtual Ceza? Ceza { get; set; }

        public int? OtoyolGecisiID { get; set; }
        [ForeignKey("OtoyolGecisiID")]
        public virtual OtoyolGecisi? OtoyolGecisi { get; set; }
    }

    [Table("FaturaDosyalari")]
    public class InvoiceFile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InvoiceID { get; set; }
        [ForeignKey("InvoiceID")]
        public virtual Invoice Invoice { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } // Fatura dosyasının sunucudaki yolu (PDF veya XML)

        [Required]
        [StringLength(50)]
        public string FileType { get; set; } // "PDF", "XML", "JSON" gibi

        [StringLength(255)]
        public string? OriginalFileName { get; set; }

        public DateTime UploadDate { get; set; } = DateTime.Now;
    }
    // Koşullu validasyon için özel attribute
    public class RequiredIfAttribute : ValidationAttribute
    {
        private readonly string _conditionMethod;
        private readonly bool _expectedValue;

        public RequiredIfAttribute(string conditionMethod, bool expectedValue)
        {
            _conditionMethod = conditionMethod;
            _expectedValue = expectedValue;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var instance = validationContext.ObjectInstance as Customer;
            var method = instance?.GetType().GetMethod(_conditionMethod);
            if (method != null)
            {
                var conditionResult = (bool)method.Invoke(instance, null);
                if (conditionResult == _expectedValue)
                {
                    if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                    {
                        return new ValidationResult(ErrorMessage);
                    }
                }
            }
            return ValidationResult.Success;
        }
    }


    [Table("Lokasyonlar")]
    public class Lokasyon
    {
        [Key]
        public int LokasyonID { get; set; }

        [Required(ErrorMessage = "Lokasyon adı zorunludur.")]
        [StringLength(100, ErrorMessage = "Lokasyon adı 100 karakterden uzun olamaz.")]
        public string LokasyonAdi { get; set; }

        [StringLength(200, ErrorMessage = "Açıklama 200 karakterden uzun olamaz.")]
        public string? Aciklama { get; set; }

        // Navigation property: Bu lokasyondaki araçlar
        public List<Vehicle> Araclar { get; set; } = new List<Vehicle>();
    }

    [Table("Araclar")]
    public class Vehicle
    {
        [Key]
        public int AracID { get; set; }

        [Required(ErrorMessage = "Plaka zorunludur.")]
        [StringLength(20, ErrorMessage = "Plaka 20 karakterden uzun olamaz.")]
        public string Plaka { get; set; }

        [Required(ErrorMessage = "Şasi numarası zorunludur.")]
        [StringLength(50, ErrorMessage = "Şasi numarası 50 karakterden uzun olamaz.")]
        public string SaseNo { get; set; }

        [StringLength(255, ErrorMessage = "Açıklama 255 karakterden uzun olamaz.")]
        public string? Aciklama { get; set; }

        [Required(ErrorMessage = "Marka zorunludur.")]
        [StringLength(50, ErrorMessage = "Marka 50 karakterden uzun olamaz.")]
        public string Marka { get; set; }

        [Required(ErrorMessage = "Model zorunludur.")]
        [StringLength(50, ErrorMessage = "Model 50 karakterden uzun olamaz.")]
        public string Model { get; set; }

        [Required(ErrorMessage = "Model yılı zorunludur.")]
        [Range(1900, 2100, ErrorMessage = "Model yılı 1900 ile 2100 arasında olmalıdır.")]
        public int ModelYili { get; set; }

        public DateTime? TrafikBaslangicTarihi { get; set; }
        public DateTime? TrafikBitisTarihi { get; set; }

        [StringLength(255, ErrorMessage = "Belge yolu 255 karakterden uzun olamaz.")]
        public string? TrafikBelgesi { get; set; }

        public DateTime? KaskoBaslangicTarihi { get; set; }
        public DateTime? KaskoBitisTarihi { get; set; }

        [StringLength(255, ErrorMessage = "Belge yolu 255 karakterden uzun olamaz.")]
        public string? KaskoBelgesi { get; set; }

        [StringLength(255, ErrorMessage = "Belge yolu 255 karakterden uzun olamaz.")]
        public string? MTV1Belgesi { get; set; }

        [StringLength(255, ErrorMessage = "Belge yolu 255 karakterden uzun olamaz.")]
        public string? MTV2Belgesi { get; set; }

        public bool MTV1Odendi { get; set; }
        public bool MTV2Odendi { get; set; }

        public int? LokasyonID { get; set; }
        [ForeignKey("LokasyonID")]
        public Lokasyon? Lokasyon { get; set; }

        public int? AktifMusteriID { get; set; }

        [ForeignKey("AktifMusteriID")]
        public virtual Customer? AktifMusteri { get; set; }

        public string? Durum { get; set; }

        public virtual ICollection<Rental>? Kiralamalar { get; set; }
        public virtual ICollection<Ceza>? Cezalar { get; set; }

        public virtual ICollection<OtoyolGecisi>? OtoyolGecisleri { get; set; }
        public DateTime? TescilTarihi { get; set; }

        public decimal? KiralamaBedeli { get; set; }
        public decimal? AracBedeli { get; set; }
        public decimal? AracAlisFiyati { get; set; }
        public string? HizmetKodu { get; set; }

        public string? YakitTipi { get; set; }
        public string? VitesTipi { get; set; }
        public int? KMBilgi { get; set; }
        public string? CekisTipi { get; set; }
        public string? Renk { get; set; }
        public string? MotorGucu { get; set; }
        public string? KabisID { get; set; }
        public DateTime? KabisGirisTarihi { get; set; }
        public DateTime? KabisBitisTarihi { get; set; }
        public string? KabisBelgesi { get; set; }
        public string? RuhsatNo {  get; set; }

        public int? DanismanID { get; set; }


        public int? AracTipiID { get; set; }

        [ForeignKey("AracTipiID")]
        public AracTipiTanimi? AracTipiTanimi { get; set; } // Navigation property

        public Vehicle()
        {
            Kiralamalar = new List<Rental>();
            Cezalar = new List<Ceza>();
        }
    }

    [Table("AracTipiTanimi")]
    public class AracTipiTanimi
    {
        [Key]
        public int AracTipiID { get; set; }

        [Display(Name = "Araç Tipi Adı")]
        [Required(ErrorMessage = "Araç Tipi Adı zorunludur.")]  // Ek: Server-side validation
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Ad en az 2, en fazla 100 karakter olmalı.")]  // Ek: Uzunluk kontrolü
        public string? AracTipiName { get; set; }

        [Display(Name = "Açıklama")]
        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olmalı.")]  // Ek: Opsiyonel ama limitli
        public string? AracTipiAciklama { get; set; }

        public ICollection<Vehicle>? Araclar { get; set; } = new List<Vehicle>();

        public AracTipiTanimi()
        {
            Araclar = new List<Vehicle>();
        }
    }


    public class VehiclesListPartialViewModel
    {
        // Bu partial view'de listelenecek araçlar
        public List<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

        // Arama kutusundaki mevcut arama terimini tutar
        public string SearchTerm { get; set; }

        // Seçili olan sıralama değerini tutar (örneğin "plakaAsc")
        public string SortBy { get; set; }

        // Sıralama seçeneklerini içeren SelectList nesnesi (dropdown listesi için)
        public SelectList SortOptions { get; set; }

        // Hangi sekmenin (tab'ın) aktif olduğunu partial view'e bildirmek için
        // Bu, özellikle JavaScript kodunda veya form ID'lerinde benzersizlik sağlamak için kullanışlıdır.
        public string ActiveTab { get; set; }
        public List<Vehicle> RentedVehicles { get; internal set; }
        public List<Vehicle> AvailableVehicles { get; internal set; }
        public List<Vehicle> SecondHandVehicles { get; internal set; }
        public VehiclesListPartialViewModel RentedVehiclesPartial { get; internal set; }
        public VehiclesListPartialViewModel AvailableVehiclesPartial { get; internal set; }
        public VehiclesListPartialViewModel SecondHandVehiclesPartial { get; internal set; }
    }
    public class VehiclesDashboardViewModel
    {
        public string ActiveTab { get; set; }
        public int TotalRentedVehiclesCount { get; set; }
        public int TotalAvailableVehiclesCount { get; set; }
        public int TotalSecondHandVehiclesCount { get; set; }

        // Partial View'ler için modelleri buraya ekleyin
        public VehiclesListPartialViewModel RentedVehiclesPartial { get; set; }
        public VehiclesListPartialViewModel AvailableVehiclesPartial { get; set; }
        public VehiclesListPartialViewModel SecondHandVehiclesPartial { get; set; }

        // Sorting için SortOptions'ı da buraya eklemelisiniz.
        public List<SortOption> SortOptions { get; set; }
        public string SearchTerm { get; internal set; }
        public string SortBy { get; internal set; }
        public List<Vehicle> RentedVehicles { get; internal set; }
        public List<Vehicle> AvailableVehicles { get; internal set; }
        public List<Vehicle> SecondHandVehicles { get; internal set; }
    }
    public class SortOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
        public bool Selected { get; set; }
    }



    [Table("Kiralamalar")]
    public class Rental
    {
        [Key]
        public int KiralamaID { get; set; }

        [Required(ErrorMessage = "Araç ID'si zorunludur.")]
        public int? AracID { get; set; }

        [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")]
        public DateOnly BaslangicTarihi { get; set; }

        public DateOnly? BitisTarihi { get; set; }

        [Required(ErrorMessage = "Kayıt tarihi zorunludur.")]
        public DateTime KayitTarihi { get; set; }

        [ForeignKey("AracID")]
        public virtual Vehicle? Arac { get; set; }

        public int? MusteriID { get; set; }
        public virtual Customer? Musteri { get; set; }

        public int? LokasyonID { get; set; }
        [ForeignKey("LokasyonID")]
        public Lokasyon? Lokasyon { get; set; }   

        public string? Durum { get; set; }

        [NotMapped]
        public List<IFormFile>? KiralamaSozlesmeleriDosyalari { get; set; } // Adını daha açık hale getirdim

        // Veritabanında saklanacak olan sözleşme dosya yolları için koleksiyon
        public virtual ICollection<RentalDocument>? RentalDocuments { get; set; }


        [StringLength(100, ErrorMessage = "Orijinal lokasyon adı 100 karakterden uzun olamaz.")]
        public string? OriginalLokasyonAdi { get; set; }
    }

    [Table("KiralamaSozlesmeleri")] // Veritabanı tablo adı
    public class RentalDocument
    {
        [Key]
        public int Id { get; set; } // Benzersiz ID

        [Required]
        public int RentalID { get; set; } // Hangi kiralamaya ait olduğunu belirtir

        [Required]
        [StringLength(500)] // Dosya yolunun uzunluğunu belirleyin
        public string FilePath { get; set; } // Dosyanın sunucudaki yolu (veya URL)

        [StringLength(255)]
        public string? OriginalFileName { get; set; } // Orjinal dosya adı (isteğe bağlı)

        public DateTime UploadDate { get; set; } = DateTime.Now; // Yükleme tarihi

        [ForeignKey("RentalID")]
        public virtual Rental Rental { get; set; } = null!; // İlişkili Rental nesnesi
    }



    [Table("Cezalar")]
    public class Ceza
    {
        [Key]
        public int CezaID { get; set; }

        [Required(ErrorMessage = "Araç ID'si zorunludur.")]
        public int AracID { get; set; }

        [Required(ErrorMessage = "Müşteri ID'si zorunludur.")]
        public int MusteriID { get; set; }

        [Required(ErrorMessage = "Ceza tarihi zorunludur.")]
        public DateTime CezaTarihi { get; set; }

        [Required(ErrorMessage = "Ceza tutarı zorunludur.")]
        [Column(TypeName = "decimal(10, 2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Ceza tutarı pozitif bir değer olmalıdır.")]
        public decimal Tutar { get; set; }

        [StringLength(255, ErrorMessage = "Açıklama 255 karakterden uzun olamaz.")]
        public string? Aciklama { get; set; }

        [StringLength(255, ErrorMessage = "Ceza yeri 255 karakterden uzun olamaz.")]
        public string? CezaYeri { get; set; }

        [StringLength(500)]
        public string? CezaBelgesi { get; set; }

        public bool Odendi { get; set; } = false;

        [ForeignKey("AracID")]
        public virtual Vehicle? Arac { get; set; }     
        public virtual Customer? Musteri { get; set; }

        public virtual ICollection<InvoiceItem>? InvoiceItems { get; set; }

        
        [Required(ErrorMessage = "Ceza Tanımı ID zorunludur.")]
        public int CezaTanimiID { get; set; }
        [ForeignKey("CezaTanimiID")]
        // BURADA [Required] OLMAMALI!
        public virtual CezaTanimi CezaTanimi { get; set; }

        public Ceza()
        {
            // ... mevcut init'ler
            InvoiceItems = new List<InvoiceItem>();
        }
    }

    [Table("CezaTanimi")] // Veritabanı tablosu adı
    public class CezaTanimi
    {
        [Key]
        public int CezaTanimiID { get; set; }

        [Required(ErrorMessage = "Ceza Kodu zorunludur.")]
        [StringLength(50, ErrorMessage = "Ceza Kodu 50 karakterden uzun olamaz.")]
        public string CezaKodu { get; set; }

        [Required(ErrorMessage = "Kısa Açıklama zorunludur.")]
        [StringLength(255, ErrorMessage = "Kısa Açıklama 255 karakterden uzun olamaz.")]
        public string KisaAciklama { get; set; }

        [StringLength(1000, ErrorMessage = "Uzun Açıklama 1000 karakterden uzun olamaz.")]
        public string? UzunAciklama { get; set; }
    }

    [Table("OtoyolGecisleri")]
    public class OtoyolGecisi
    {
        [Key]
        public int GecisID { get; set; }

        [Required(ErrorMessage = "Lütfen bir araç seçin.")] // Eklendi: AracID zorunlu yapıldı
        public int? AracID { get; set; }
        [ForeignKey("AracID")]
        public virtual Vehicle? Arac { get; set; } // Nullable olduğundan emin olun

        [Required(ErrorMessage = "Lütfen bir müşteri seçin.")] // Eklendi: MusteriID zorunlu yapıldı
        public int? MusteriID { get; set; }
        public virtual Customer? Musteri { get; set; } // Nullable olduğundan emin olun

        [Required(ErrorMessage = "Geçiş tarihi zorunludur.")]
        public DateTime GecisTarihi { get; set; }

        [Required(ErrorMessage = "Tutar zorunludur.")]
        [Column(TypeName = "decimal(10, 2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Tutar pozitif bir değer olmalıdır.")]
        public decimal Tutar { get; set; }

        [Required(ErrorMessage = "Geçiş yeri zorunludur.")] // Eklendi: Geçiş yeri zorunlu yapıldı
        [StringLength(255, ErrorMessage = "Geçiş yeri 255 karakterden uzun olamaz.")]
        public string GecisYeri { get; set; }

        [StringLength(255, ErrorMessage = "Açıklama 255 karakterden uzun olamaz.")]
        public string? Aciklama { get; set; }

        public string? GecisBelgesi { get; set; }
        public bool Odendi { get; set; } = false; // Bu alanın zaten varsayılan değeri var, bu yüzden [Required] eklenmedi.

        public int? LokasyonID { get; set; }
        [ForeignKey("LokasyonID")]
        public virtual Lokasyon? Lokasyon { get; set; }

        public virtual ICollection<InvoiceItem>? InvoiceItems { get; set; }

        public OtoyolGecisi()
        {
            InvoiceItems = new List<InvoiceItem>();
        }
    }


    public class DashboardViewModel
    {
        // Kiralama İstatistikleri
        public int TotalRentalCount { get; set; }
        public int ActiveRentalCount { get; set; }
        public int CompletedRentalCount { get; set; }
        public int TodayStartingRentalCount { get; set; }
        public int TodayEndingRentalCount { get; set; }

        // Araç Envanteri
        public int TotalVehicleCount { get; set; }
        public int RentedVehicleCount { get; set; } // Kiralıkta olan araç
        public int AvailableVehicleCount { get; set; } // Müsait araç
        public List<LocationVehicleCount> VehiclesByLocation { get; set; } = new List<LocationVehicleCount>();
        public List<MostRentedVehicle> MostRentedVehicles { get; set; } = new List<MostRentedVehicle>();

        // Müşteri İstatistikleri
        public int TotalCustomerCount { get; set; } // Yeni eklendi
        public int NewCustomersLast30Days { get; set; }
        public List<CustomerSummary> LatestCustomers { get; set; } = new List<CustomerSummary>();

        // Ceza İstatistikleri
        public int TotalPenaltyCount { get; set; }
        public int UnpaidPenaltyCount { get; set; }
        public decimal TotalUnpaidPenaltyAmount { get; set; }
        public int PaidPenaltyCount { get; set; }
        public decimal TotalPaidPenaltyAmount { get; set; }

        // Grafik Verileri (Ay bazında)
        public List<string> ActiveRentalMonths { get; set; } = new List<string>();
        public List<int> ActiveRentalCounts { get; set; } = new List<int>();

        public List<string> PastRentalMonths { get; set; } = new List<string>();
        public List<int> PastRentalCounts { get; set; } = new List<int>();

        // İç DTO'lar (Data Transfer Objects)
        public class LocationVehicleCount
        {
            public string LocationName { get; set; }
            public int Count { get; set; }
        }

        public class CustomerSummary
        {
            public string Title { get; set; }
            public string Eposta { get; set; }
        }

        public class MostRentedVehicle
        {
            public string VehicleName { get; set; } // Marka Model birleşimi
            public int RentalCount { get; set; }
        }
    }


    public class CustomersDashboardViewModel
    {
        public string ActiveTab { get; set; } = "all"; // Varsayılan sekme
        public int TotalCustomersCount { get; set; }
        public int IndividualCustomersCount { get; set; }
        public int CorporateCustomersCount { get; set; }

        public CustomerListPartialViewModel AllCustomersPartial { get; set; }
        public CustomerListPartialViewModel IndividualCustomersPartial { get; set; }
        public CustomerListPartialViewModel CorporateCustomersPartial { get; set; }
    }

    // Müşteri listesi partial'ı için ViewModel
    public class CustomerListPartialViewModel
    {
        public List<Customer> Customers { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string SearchQuery { get; set; }
        public bool CustomerType { get; set; } // "all", "individual", "corporate"
    }

    public class ParametersDashboardViewModel
    {
        // Mevcut Ceza Tanımları için
        public PenaltyDefinitionListPartialViewModel PenaltyDefinitionsPartial { get; set; }
        public int TotalPenaltyDefinitionsCount { get; set; }

        // Mevcut Kullanıcı Tanımları için
        public UserListPartialViewModel UsersPartial { get; set; }
        public int TotalUsersCount { get; set; }

        // YENİ EKLENEN: Araç Tipi Tanımları için
        public CarTypePartialViewModel CarTypePartial { get; set; } // Daha önce CarTypePartialViewModel'ı tanımlamıştık
        public int TotalCarTypeCount { get; set; }

        public string ActiveTab { get; set; } // Hangi tab'ın aktif olduğunu tutar

        public int TotalLocationsCount { get; set; }
        public LocationPartialViewModel LocationsPartial { get; set; }

        public ParametersDashboardViewModel()
        {
            // Null Referans hatalarını önlemek için varsayılan başlatmalar
            PenaltyDefinitionsPartial = new PenaltyDefinitionListPartialViewModel();
            UsersPartial = new UserListPartialViewModel();
            CarTypePartial = new CarTypePartialViewModel(); // YENİ EKLENDİ
            LocationsPartial = new LocationPartialViewModel();
            ActiveTab = "penalty-definitions"; // Varsayılan aktif tab
        }
    }

    public class LocationPartialViewModel
    {
        public List<Lokasyon> Locations { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public string SearchQuery { get; set; }
    }
   


    public class CarTypePartialViewModel
    {
        // Tip güvenliği için IEnumerable<AracTipiTanimi> olarak değiştirildi
        public IEnumerable<AracTipiTanimi> AracTipiListesi { get; set; } // Daha açıklayıcı bir isim de verdim
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string SearchQuery { get; set; } = ""; // Null referans hatası almamak için varsayılan değer
        public int TotalCount { get; internal set; }

        // AracTipiListesi null ise boş bir liste döndür.
        public CarTypePartialViewModel()
        {
            AracTipiListesi = new List<AracTipiTanimi>();
        }
    }

    // Ceza Tanımı listesi partial'ı için ViewModel
    public class PenaltyDefinitionListPartialViewModel
    {
        public IEnumerable<CezaTanimi> PenaltyDefinitions { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string SearchQuery { get; set; }
    }

    // Kullanıcı listesi partial'ı için ViewModel (Danışman Tanımları için)
    public class UserListPartialViewModel
    {
        public IEnumerable<User> Users { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string SearchQuery { get; set; }
        public IEnumerable<Role> AvailableRoles { get; set; } // Kullanıcı oluşturma/düzenleme için roller
    }

    public class CreateRentalViewModel
    {
        public int? AracID { get; set; } // [Required] niteliği burada olmalı

        public int? MusteriID { get; set; } // [Required] niteliği burada olmalı

        [Required(ErrorMessage = "Başlangıç tarihi zorunludur.")]
        public DateOnly BaslangicTarihi { get; set; }

        public DateOnly? BitisTarihi { get; set; }

        // Diğer Rental özelliklerini buraya taşıyın (eğer gerekiyorsa)
        // public DateTime KayitTarihi { get; set; } // Bu, controller'da atanmalı, ViewModel'de tutulmasına gerek yok
        // public int? LokasyonID { get; set; }
        // public string? Durum { get; set; }

        // Formdan yüklenecek dosyalar için:
        public List<IFormFile>? KiralamaSozlesmeleriDosyalari { get; set; }

        // Formun başlangıç verilerini taşımak için SearchItem listeleri:
        public List<SearchItem> AvailableVehicles { get; set; } = new List<SearchItem>();
        public List<SearchItem> AvailableCustomers { get; set; } = new List<SearchItem>();

        // Eğer formda seçilen aracın/müşterinin metinsel gösterimine ihtiyacınız varsa:
        public string? SelectedVehicleDisplay { get; set; }
        public string? SelectedCustomerDisplay { get; set; }
    }

    // SearchItem sınıfınızı olduğu gibi tutun
    public class SearchItem
    {
        public int Id { get; set; }
        public string Text { get; set; }
        // Diğer gerekli özellikler (örneğin Marka, Model, Plaka, Telefon)
        public string? Marka { get; set; }
        public string? Model { get; set; }
        public string? Plaka { get; set; }
        public decimal? KiralamaBedeli { get; set; }
        public string? Ad { get; set; }
        public string? Soyad { get; set; }
        public string? KimlikNo { get; set; }
        public string? Telefon { get; set; }
    }

    public class CezaViewModel
    {
        // Eğer düzenleme yaparken ID'ye ihtiyacınız olursa
        public int CezaID { get; set; } // Edit işlemi için

        // Hidden ID alanları ve ilişkili display alanları
        [Required(ErrorMessage = "Araç seçimi zorunludur.")]
        [Display(Name = "Araç ID")]
        public int AracID { get; set; }

        [Display(Name = "Seçilen Araç")] // Sadece görüntüleme amaçlı, [Required] YOK
        public string SelectedVehicleDisplayPenalty { get; set; }

        [Required(ErrorMessage = "Müşteri seçimi zorunludur.")]
        [Display(Name = "Müşteri ID")]
        public int MusteriID { get; set; }

        [Display(Name = "Seçilen Müşteri")] // Sadece görüntüleme amaçlı, [Required] YOK
        public string SelectedCustomerDisplayPenalty { get; set; }

        [Required(ErrorMessage = "Ceza Tanımı seçimi zorunludur.")]
        [Display(Name = "Ceza Tanımı ID")]
        public int CezaTanimiID { get; set; }

        [Display(Name = "Seçilen Ceza Tanımı")] // Sadece görüntüleme amaçlı, [Required] YOK
        public string SelectedPenaltyDefinitionDisplay { get; set; }

        // Form alanları
        [Required(ErrorMessage = "Ceza Tarihi zorunludur.")]
        [DataType(DataType.Date)]
        [Display(Name = "Ceza Tarihi")]
        public DateTime CezaTarihi { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Tutar zorunludur.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Tutar pozitif bir değer olmalıdır.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Tutar")]
        public decimal Tutar { get; set; }

        [StringLength(500, ErrorMessage = "Açıklama 500 karakterden uzun olamaz.")]
        [Display(Name = "Açıklama")]
        public string Aciklama { get; set; }

        [Required(ErrorMessage = "Ceza Yeri zorunludur.")]
        [StringLength(200, ErrorMessage = "Ceza Yeri 200 karakterden uzun olamaz.")]
        [Display(Name = "Ceza Yeri")]
        public string CezaYeri { get; set; }

        // bool tipi için [Required] YOK
        [Display(Name = "Ödendi mi?")]
        public bool Odendi { get; set; } = false;

        // Dosya yükleme için. [Required] ekleyebilirsiniz eğer belge yüklemek ZORUNLU ise.
        // Şu anki senaryoda, AddPenalty'de IFormFile? olduğu için zorunlu değil gibi duruyor.
        [Display(Name = "Ceza Belgesi")]
        public IFormFile? CezaBelgesiFile { get; set; }

        // Veritabanına kaydedilecek dosya yolu. Formdan gelmediği için [Required] YOK
        public string? CezaBelgesi { get; set; } // Sizdeki modelde CezaBelgesi olarak geçiyor.

        // Edit işlemi için mevcut belge yolunu tutmak gerekebilir
        public string? ExistingCezaBelgesi { get; set; }
    }

    public class RelatedVehiclesViewModel
    {
        public int AracTipiId { get; set; }
        public List<Vehicle> RelatedVehicles { get; set; } = new List<Vehicle>();
        public List<AracTipiTanimi> AllVehicleTypes { get; set; } = new List<AracTipiTanimi>();
    }

}


    