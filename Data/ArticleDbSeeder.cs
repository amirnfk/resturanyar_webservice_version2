using resturanyar.Models;
using Resturanyar.Data;
using Serilog;

namespace resturanyar.Data
{
    public static class ArticleDbSeeder
    {
        public static bool Seed(AppDbContext context, string? contentRootPath = null)
        {
            try
            {
                if (!context.Database.CanConnect())
                {
                    Log.Warning("Article seed skipped: database is not reachable.");
                    return false;
                }

                if (context.Articles.Any())
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(contentRootPath))
                {
                    var synced = ArticleContentUpdater.SyncFromContentFolder(context, contentRootPath);
                    if (synced > 0)
                    {
                        Log.Information("Seeded {Count} articles from content files.", synced);
                        return true;
                    }
                }

                var articles = GetSeedArticles();
                foreach (var article in articles)
                {
                    article.IsPublished = true;
                }

                context.Articles.AddRange(articles);
                context.SaveChanges();
                Log.Information("Seeded {Count} articles.", articles.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Article seed failed.");
                return false;
            }
        }

        public static IReadOnlyList<Article> GetSeedArticles()
        {
            var baseDate = new DateTime(2025, 6, 1);

            return new List<Article>
            {
                new()
                {
                    Slug = "free-restaurant-management-software",
                    Title = "نرم‌افزار مدیریت رستوران رایگان؛ مقایسه رستورانیار با روش‌های سنتی",
                    MetaDescription = "مقایسه نرم‌افزار رایگان مدیریت رستوران رستورانیار با روش‌های سنتی کاغذی و اکسل. مزایا، معایب و زمان بازگشت سرمایه.",
                    Keywords = "نرم افزار مدیریت رستوران رایگان, نرم افزار رستوران, رستورانیار, مدیریت رستوران",
                    PublishedAt = baseDate,
                    FeaturedImageUrl = "/images/mobilesample.png",
                    Content = """
                        <p>بسیاری از مدیران رستوران هنوز سفارش‌ها را با دفتر و قلم ثبت می‌کنند. این روش در کسب‌وکارهای کوچک جواب می‌دهد، اما با رشد مشتری و پرسنل، خطا و اتلاف زمان چند برابر می‌شود. <strong>نرم‌افزار مدیریت رستوران رایگان</strong> رستورانیار جایگزینی عملی برای این روش‌هاست.</p>
                        <h2>محدودیت‌های روش سنتی</h2>
                        <ul>
                            <li>احتمال خطا در ثبت سفارش و محاسبه صورتحساب</li>
                            <li>عدم دسترسی لحظه‌ای به وضعیت میزها و آشپزخانه</li>
                            <li>گزارش‌گیری سخت و زمان‌بر در پایان روز</li>
                        </ul>
                        <h2>چرا رستورانیار؟</h2>
                        <p>رستورانیار با <a href="/restaurant-management">نرم‌افزار مدیریت رستوران</a> یکپارچه، ثبت سفارش گارسون، ارسال به آشپزخانه و <a href="/digital-menu">منوی دیجیتال</a> را در یک پلتفرم ارائه می‌دهد. نسخه موبایل برای پرسنل رایگان است و می‌توانید با اشتراک تستی بدون ریسک شروع کنید.</p>
                        <div class="highlight-box"><strong>نتیجه:</strong> بیشتر رستوران‌هایی که از نرم‌افزار استفاده می‌کنند، در هفته‌های اول سرعت خدمات و دقت ثبت سفارش را به‌طور محسوسی بهبود می‌بینند.</div>
                        <p><a href="/resturanyar-pricelist" class="btn btn-cta">دریافت لیست قیمت</a></p>
                        """
                },
                new()
                {
                    Slug = "digital-menu-qr-guide",
                    Title = "راهنمای راه‌اندازی منوی دیجیتال QR برای رستوران",
                    MetaDescription = "گام‌به‌گام راه‌اندازی منوی دیجیتال QR برای رستوران و کافه: انتخاب سیستم، چاپ کد، به‌روزرسانی منو و افزایش فروش.",
                    Keywords = "منوی دیجیتال QR, منو دیجیتال رستوران, QR کد منو, رستورانیار",
                    PublishedAt = baseDate.AddDays(5),
                    FeaturedImageUrl = "/images/sampleweb.jpg",
                    Content = """
                        <p>منوی دیجیتال با QR کد یکی از سریع‌ترین راه‌ها برای کاهش هزینه چاپ منو و افزایش سرعت سفارش‌گیری است. مشتری با اسکن کد، منوی به‌روز را روی موبایل خود می‌بیند.</p>
                        <h2>مراحل راه‌اندازی</h2>
                        <ol>
                            <li>ثبت منو و دسته‌بندی‌ها در <a href="/digital-menu">سیستم منوی دیجیتال</a> رستورانیار</li>
                            <li>تولید QR اختصاصی برای هر میز یا بخش سالن</li>
                            <li>چاپ و نصب کد روی میزها یا استند منو</li>
                            <li>به‌روزرسانی قیمت و موجودی از پنل مدیریت — بدون چاپ مجدد</li>
                        </ol>
                        <h2>نکات مهم</h2>
                        <p>منوی دیجیتال باید سریع بارگذاری شود و تصاویر با کیفیت داشته باشد. ترکیب منوی QR با <a href="/restaurant-management">نرم‌افزار مدیریت رستوران</a> باعث می‌شود سفارش مستقیم از منو به آشپزخانه برسد.</p>
                        <p><a href="/Home/ManagerLogin" class="btn btn-cta">شروع رایگان</a></p>
                        """
                },
                new()
                {
                    Slug = "restaurant-pos-features",
                    Title = "۱۰ ویژگی ضروری نرم‌افزار صندوق (POS) رستوران",
                    MetaDescription = "۱۰ قابلیت کلیدی که هر نرم‌افزار صندوق رستوران (POS) باید داشته باشد: ثبت سفارش، آشپزخانه، گزارش و باشگاه مشتریان.",
                    Keywords = "نرم افزار صندوق رستوران, POS رستوران, صندوق رستوران, رستورانیار",
                    PublishedAt = baseDate.AddDays(10),
                    FeaturedImageUrl = "/images/sampleweb2.jpg",
                    Content = """
                        <p>انتخاب POS مناسب پایه عملیات روزانه رستوران است. این ۱۰ ویژگی را حتماً بررسی کنید:</p>
                        <ol>
                            <li>ثبت سفارش سریع توسط گارسون (موبایل یا تبلت)</li>
                            <li>مدیریت میزها و وضعیت اشغال</li>
                            <li>ارسال خودکار سفارش به آشپزخانه</li>
                            <li>تسویه و چاپ فاکتور</li>
                            <li>گزارش فروش روزانه و ماهانه</li>
                            <li>مدیریت منو و قیمت‌گذاری</li>
                            <li>دسترسی سطح‌بندی‌شده پرسنل</li>
                            <li>پشتیبانی از چند شعبه</li>
                            <li>اتصال به <a href="/customer-club">باشگاه مشتریان</a></li>
                            <li>پشتیبانی فنی سریع</li>
                        </ol>
                        <p>رستورانیار این قابلیت‌ها را در <a href="/restaurant-management">نرم‌افزار مدیریت رستوران</a> یکپارچه ارائه می‌دهد.</p>
                        """
                },
                new()
                {
                    Slug = "kitchen-order-management",
                    Title = "چگونه سفارش‌ها را سریع‌تر به آشپزخانه برسانیم؟",
                    MetaDescription = "راهکارهای عملی برای مدیریت آشپزخانه رستوران: کاهش تأخیر، حذف سفارش‌های گم‌شده و هماهنگی سالن با آشپزخانه.",
                    Keywords = "مدیریت آشپزخانه, سفارش آشپزخانه, نرم افزار رستوران, رستورانیار",
                    PublishedAt = baseDate.AddDays(15),
                    FeaturedImageUrl = "/images/orderlist.jpg",
                    Content = """
                        <p>تأخیر در رساندن سفارش به آشپزخانه یکی از اصلی‌ترین دلایل نارضایتی مشتری و شلوغی سالن است.</p>
                        <h2>علت‌های رایج تأخیر</h2>
                        <ul>
                            <li>ثبت دستی سفارش روی کاغذ</li>
                            <li>عدم اولویت‌بندی سفارش‌های همزمان</li>
                            <li>نبود نمایشگر یا تبلت در آشپزخانه</li>
                        </ul>
                        <h2>راه‌حل دیجیتال</h2>
                        <p>با رستورانیار، گارسون سفارش را ثبت می‌کند و بلافاصله روی صفحه آشپزخانه ظاهر می‌شود. وضعیت «در حال آماده‌سازی» و «آماده تحویل» به سالن اطلاع داده می‌شود. این جریان در <a href="/restaurant-management">مدیریت رستوران</a> و <a href="/digital-menu">منوی دیجیتال</a> یکپارچه است.</p>
                        """
                },
                new()
                {
                    Slug = "customer-loyalty-tips",
                    Title = "۵ راه عملی برای افزایش مشتری وفادار در رستوران",
                    MetaDescription = "۵ استراتژی باشگاه مشتریان رستوران: ثبت خودکار، تحلیل رفتار خرید، پیشنهاد شخصی و افزایش نرخ بازگشت مشتری.",
                    Keywords = "باشگاه مشتریان رستوران, مشتری وفادار, مدیریت مشتریان, رستورانیار",
                    PublishedAt = baseDate.AddDays(20),
                    FeaturedImageUrl = "/images/customer_club.png",
                    Content = """
                        <p>جذب مشتری جدید هزینه‌بر است؛ نگه‌داشتن مشتری فعلی سودآورتر. این ۵ راه را امتحان کنید:</p>
                        <ol>
                            <li>ثبت خودکار مشتری هنگام هر سفارش (بدون فرم اضافی)</li>
                            <li>شناسایی مشتریان پرتکرار و VIP</li>
                            <li>ارائه پیشنهاد بر اساس سابقه خرید</li>
                            <li>پیگیری مشتریان غایب با پیام یا تماس</li>
                            <li>گزارش نرخ بازگشت ماهانه</li>
                        </ol>
                        <p>ماژول <a href="/customer-club">باشگاه مشتریان رستورانیار</a> این کارها را خودکار انجام می‌دهد و با <a href="/restaurant-management">سیستم مدیریت رستوران</a> یکپارچه است.</p>
                        """
                },
                new()
                {
                    Slug = "fastfood-order-management",
                    Title = "مدیریت سفارش‌های پرحجم در فست‌فود با نرم‌افزار",
                    MetaDescription = "چگونه در فست‌فود ساعات شلوغی، سرعت ثبت سفارش و دقت آشپزخانه را حفظ کنیم؟ راهکارهای نرم‌افزاری رستورانیار.",
                    Keywords = "نرم افزار فست فود, مدیریت فست فود, سفارشگیر, رستورانیار",
                    PublishedAt = baseDate.AddDays(25),
                    FeaturedImageUrl = "/images/bergerback.jpg",
                    Content = """
                        <p>فست‌فود در ساعات اوج با صف طولانی و فشار بالا مواجه است. نرم‌افزار مناسب تفاوت بین ۵ و ۱۵ دقیقه انتظار را می‌سازد.</p>
                        <h2>نیازهای خاص فست‌فود</h2>
                        <ul>
                            <li>ثبت سریع چند سفارش همزمان</li>
                            <li>نمایش وضعیت آماده‌سازی برای مشتری</li>
                            <li>گزارش سرعت خدمت و محبوب‌ترین آیتم‌ها</li>
                        </ul>
                        <p>رستورانیار برای فست‌فود، رستوران و <a href="/cafeshop-management">کافه</a> طراحی شده و با <a href="/digital-menu">منوی دیجیتال</a> ترکیب می‌شود تا صف صندوق کوتاه‌تر شود.</p>
                        <p><a href="/resturanyar-pricelist" class="btn btn-cta">دریافت لیست قیمت</a></p>
                        """
                },
                new()
                {
                    Slug = "restaurant-kpi-reporting",
                    Title = "KPIهای مهم گزارش‌گیری برای مدیر رستوران",
                    MetaDescription = "مهم‌ترین شاخص‌های عملکرد (KPI) رستوران: فروش روزانه، میانگین فاکتور، نرخ بازگشت مشتری و تحلیل منو.",
                    Keywords = "گزارش‌گیری رستوران, KPI رستوران, گزارش فروش, رستورانیار",
                    PublishedAt = baseDate.AddDays(30),
                    FeaturedImageUrl = "/images/sampleweb4.jpg",
                    Content = """
                        <p>بدون گزارش دقیق، مدیریت رستوران حدسی می‌شود. این KPIها را هفتگی بررسی کنید:</p>
                        <ul>
                            <li><strong>فروش روزانه و هفتگی</strong> — روند رشد یا افت</li>
                            <li><strong>میانگین مبلغ فاکتور</strong> — اثر upselling</li>
                            <li><strong>محبوب‌ترین آیتم‌های منو</strong> — بهینه‌سازی موجودی</li>
                            <li><strong>ساعات پیک</strong> — برنامه‌ریزی پرسنل</li>
                            <li><strong>نرخ بازگشت مشتری</strong> — سلامت باشگاه مشتریان</li>
                        </ul>
                        <p>پنل گزارش رستورانیار این داده‌ها را از <a href="/restaurant-management">نرم‌افزار مدیریت رستوران</a> استخراج می‌کند.</p>
                        """
                },
                new()
                {
                    Slug = "cafe-vs-restaurant-software",
                    Title = "تفاوت نیازهای نرم‌افزار مدیریت کافه و رستوران",
                    MetaDescription = "کافه و رستوران چه تفاوتی در نرم‌افزار مدیریت دارند؟ سرعت سفارش، منو، باشگاه مشتریان و گزارش‌گیری.",
                    Keywords = "نرم افزار مدیریت کافه, نرم افزار مدیریت رستوران, رستورانیار",
                    PublishedAt = baseDate.AddDays(35),
                    FeaturedImageUrl = "/images/tablethome.jpg",
                    Content = """
                        <p>کافه و رستوران هر دو به نرم‌افزار نیاز دارند، اما اولویت‌ها متفاوت است.</p>
                        <h2>کافه</h2>
                        <ul>
                            <li>ثبت سریع سفارش‌های تک‌نفره</li>
                            <li>مدیریت موجودی قهوه و دسر</li>
                            <li>باشگاه مشتریان برای مشتریان ثابت</li>
                        </ul>
                        <h2>رستوران</h2>
                        <ul>
                            <li>مدیریت میز و چند مرحله سفارش</li>
                            <li>هماهنگی آشپزخانه و سالن</li>
                            <li>گزارش‌گیری پیچیده‌تر</li>
                        </ul>
                        <p>رستورانیار هر دو سناریو را پوشش می‌دهد: <a href="/cafeshop-management">نرم‌افزار مدیریت کافه</a> و <a href="/restaurant-management">نرم‌افزار مدیریت رستوران</a>.</p>
                        """
                }
            };
        }
    }
}
