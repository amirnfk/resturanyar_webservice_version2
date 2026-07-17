using resturanyar.Models;

namespace resturanyar.Utility
{
    public static class SeoDefaults
    {
        public const string SiteUrl = "https://resturanyar.ir";
        public const string DefaultOgImage = SiteUrl + "/images/og-resturanyar.jpg";

        public static SeoMetadata HomePage() => new()
        {
            Title = "رستورانیار | نرم‌افزار مدیریت رستوران و منوی دیجیتال",
            Description = "رستورانیار: نرم‌افزار یکپارچه مدیریت کافه و رستوران، منوی دیجیتال، سفارش‌گیر، آشپزخانه و صندوق. سریع، هوشمند، قابل اتکا.",
            Keywords = "نرم افزار رستوران, POS رستوران, منوی دیجیتال QR, سفارشگیر گارسون, سیستم مدیریت کافه, نرم افزار فست فود, صندوق رستوران, مدیریت آشپزخانه",
            CanonicalUrl = SiteUrl + "/",
            OgType = "website"
        };

        public static SeoMetadata RestaurantManagement() => new()
        {
            Title = "نرم‌افزار مدیریت رستوران، کافه و فست‌فود | رستورانیار",
            Description = "رستورانیار نرم‌افزار جامع مدیریت رستوران با نسخه موبایل رایگان و اشتراک تستی. مدیریت سفارش، پرسنل، میزها، آشپزخانه و گزارش‌گیری هوشمند.",
            Keywords = "نرم افزار مدیریت رستوران, مدیریت رستوران, نرم افزار مدیریت رستوران رایگان, نرم افزار مدیریت رستوران موبایل, رستورانیار",
            CanonicalUrl = SiteUrl + "/restaurant-management",
            OgType = "article"
        };

        public static SeoMetadata CafeShopManagement() => new()
        {
            Title = "نرم‌افزار مدیریت کافه، کافی‌شاپ و قهوه‌خانه | رستورانیار",
            Description = "رستورانیار نرم‌افزار جامع مدیریت کافه و کافی‌شاپ با نسخه موبایل رایگان و اشتراک تستی. مدیریت سفارشات، پرسنل، میزها، موجودی و گزارش‌گیری هوشمند.",
            Keywords = "نرم افزار مدیریت کافه, مدیریت کافی شاپ, نرم افزار کافه, نرم افزار مدیریت قهوه خانه, رستورانیار, نرم افزار مدیریت رستوران",
            CanonicalUrl = SiteUrl + "/cafeshop-management",
            OgType = "article"
        };

        public static SeoMetadata DigitalMenu() => new()
        {
            Title = "منوی دیجیتال رستوران و کافه با QR کد | رستورانیار دلاویتا",
            Description = "با منوی دیجیتال رستورانیار دلاویتا، منوی رستوران، کافه یا فست‌فود خود را به صورت آنلاین و با QR کد در اختیار مشتریان قرار دهید. افزایش سرعت سفارش، کاهش هزینه‌ها و بهبود تجربه مشتری.",
            Keywords = "منوی دیجیتال, منو دیجیتال رستوران, QR کد منو, منوی آنلاین کافه, رستورانیار, دلاویتا, نرم افزار منوی دیجیتال",
            CanonicalUrl = SiteUrl + "/digital-menu",
            OgType = "article"
        };

        public static SeoMetadata CustomerClub() => new()
        {
            Title = "باشگاه مشتریان رستوران و کافه | رستورانیار دلاویتا",
            Description = "باشگاه مشتریان رستورانیار دلاویتا با ثبت خودکار مشتریان هنگام سفارش، تحلیل رفتار خرید، نرخ بازگشت و رتبه‌بندی مشتریان وفادار. مدیریت هوشمند مشتریان رستوران، کافه و فست‌فود.",
            Keywords = "باشگاه مشتریان, مدیریت مشتریان رستوران, تحلیل رفتار مشتری, نرخ بازگشت مشتری, مشتری وفادار, رستورانیار, دلاویتا, نرم افزار مشتریان",
            CanonicalUrl = SiteUrl + "/customer-club",
            OgType = "article"
        };

        public static SeoMetadata PublicSupport() => new()
        {
            Title = "پشتیبانی نرم‌افزار رستورانیار | تماس با ما",
            Description = "پشتیبانی  رستورانیار دلاویتا. ارتباط از طریق چت آنلاین، تلفن، ایمیل و پاسخ به سوالات متداول. تیم پشتیبانی ما همیشه در کنار شماست.",
            Keywords = "پشتیبانی رستورانیار, تماس با پشتیبانی, پشتیبانی نرم افزار رستوران, چت آنلاین, رستورانیار دلاویتا",
            CanonicalUrl = SiteUrl + "/public-support",
            OgType = "article"
        };

        public static SeoMetadata AboutUs() => new()
        {
            Title = "درباره ما | رستورانیار دلاویتا، آرمان مدیریت هوشمند",
            Description = "رستورانیار دلاویتا با هدف مدیریت صحیح و کامل رستوران‌ها و کافه‌ها طراحی شده است. ما به دنبال تحول در صنعت مهمان‌نوازی از طریق فناوری و هوشمندی هستیم.",
            Keywords = "درباره ما, رستورانیار, دلاویتا, مدیریت رستوران, مدیریت کافه, نرم افزار مدیریت رستوران",
            CanonicalUrl = SiteUrl + "/about-us",
            OgType = "article"
        };

        public static SeoMetadata PriceList() => new()
        {
            Title = "دریافت لیست قیمت نرم‌افزار رستورانیار",
            Description = "دریافت آنی لیست قیمت محصولات و نرم‌افزار مدیریت رستوران، فست‌فود و کافی‌شاپ رستورانیار دلاویتا.",
            Keywords = "لیست قیمت رستورانیار, قیمت نرم افزار رستوران, نرم افزار مدیریت رستوران, رستورانیار",
            CanonicalUrl = SiteUrl + "/resturanyar-pricelist",
            OgType = "website"
        };
    }
}
