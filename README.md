# Stok, Cari ve Fiş Hizmeti Web API

Çoklu şirket (multi-tenant) yapısında stok, cari ve fiş yönetimi sunan **.NET 10 Web API**.
Fiş onaylandığında stok ve cari hareketleri tek transaction içinde üretilir, bakiyeler güncellenir.

> Kapsam: yalnızca backend. Kullanıcı arayüzü, Nuxt.js ve Docker kapsam dışıdır.

## Teknoloji

.NET 10 · ASP.NET Core Web API (Controller) · Entity Framework Core (Code-First) · PostgreSQL (Npgsql) · Redis (StackExchange.Redis) · AutoMapper · Serilog · JWT · Swagger

## Mimari (klasik N-Tier)

| Proje | Sorumluluk |
|---|---|
| `StokCari.Api` | Controller'lar, middleware, JWT, Swagger, hata yönetimi (RFC 7807), Serilog yapılandırması |
| `StokCari.Services` | İş kuralları (servisler), Redis cache servisi |
| `StokCari.Data` | `AppDbContext`, entity yapılandırmaları, SaveChanges interceptor |
| `StokCari.Core` | Entity'ler, DTO'lar, arayüzler, sabitler, exception'lar |

Şema: [`docs/stok_cari_fis_nihai.dbml`](docs/stok_cari_fis_nihai.dbml) (dbdiagram.io'ya yapıştırılabilir).

## Gereksinimler

- .NET 10 SDK
- PostgreSQL (kurulu ve çalışır durumda; `postgres` kullanıcısının parolası gerekir)
- Redis (opsiyonel: kurulu değilse veya kapalıysa API cache'i atlar ve veritabanından çalışmaya devam eder; bağlantı gelince cache kendiliğinden devreye girer)

Veritabanını (`stokcari`) elle oluşturmanız gerekmez; Development ortamında uygulama ilk çalıştırmada kendisi oluşturur.

## Çalıştırma

```bash
# 1) Bağlantı dizesi (parolayı kendi parolanızla değiştirin). Windows PowerShell:
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=stokcari;Username=postgres;Password=PAROLANIZ"
#    Linux/macOS: export ConnectionStrings__Default="Host=localhost;Port=5432;Database=stokcari;Username=postgres;Password=PAROLANIZ"
#    (Kalıcı yapmak için: dotnet user-secrets set "ConnectionStrings:Default" "<dize>" --project src/StokCari.Api)
# 2) JWT anahtarı (Development için appsettings.Development.json içinde örnek bir anahtar vardır; kendi anahtarınızı user-secrets ile verin)
dotnet user-secrets set "Jwt:Key" "en-az-32-karakterlik-gizli-anahtar-buraya" --project src/StokCari.Api

# 3) AutoMapper lisans anahtarı (ücretsiz Community lisansı: https://luckypennysoftware.com).
#    Anahtar yoksa da çalışır; yalnızca açılışta "geliştirme/test için izinli" uyarısı loglanır.
dotnet user-secrets set "AutoMapper:LicenseKey" "<lisans-anahtariniz>" --project src/StokCari.Api

# 4) Çalıştırın
dotnet run --project src/StokCari.Api
```

Swagger: <http://localhost:5080/swagger> (Development ortamında açık; diğer ortamlarda `Swagger:Enabled=true` ile).

Development ortamında veritabanı otomatik oluşturulur ve seed verisi eklenir:

| Kullanıcı | Parola | Rol |
|---|---|---|
| `admin` | `Admin12345!` | Admin |
| `demo` | `Demo12345!` | User (DEMO şirketi; ELMA stoğu ve AHMET carisi hazır) |

> Bu parolalar yalnızca geliştirme içindir (`appsettings.Development.json`). Gerçek ortamda `Seed:AdminPassword` değerini user-secrets veya ortam değişkeniyle verin; `Seed:DemoData` kapalı kalmalıdır.

### Migration

Şema EF Core migration'larıyla yönetilir (`src/StokCari.Data/Migrations`). Development ortamında (`Database:AutoMigrate=true`) uygulama açılışta bekleyen migration'ları kendisi uygular; ayrıca bir şey yapmanız gerekmez.

Elle uygulamak veya yeni migration eklemek için:

```bash
dotnet tool install --global dotnet-ef
export STOKCARI_CONNECTION="Host=localhost;Port=5432;Database=stokcari;Username=postgres;Password=PAROLANIZ"   # PowerShell: $env:STOKCARI_CONNECTION = "..."

# Veritabanını güncel şemaya getir
dotnet ef database update --project src/StokCari.Data --startup-project src/StokCari.Data

# Entity değiştikten sonra yeni migration ekle (örnek)
dotnet ef migrations add AddStockBarcode --project src/StokCari.Data --startup-project src/StokCari.Data
```

> Daha önce `EnsureCreated` ile oluşturulmuş bir `stokcari` veritabanınız varsa (içinde `__EFMigrationsHistory` tablosu yoksa) migration'lar uygulanamaz; bu veritabanını bir kez silip uygulamanın yeniden oluşturmasına izin verin.

## Test

- **Swagger:** Giriş yapın, dönen token'ı **Authorize** düğmesine yapıştırın.
- **Postman:** [`postman/StokCari.postman_collection.json`](postman/StokCari.postman_collection.json) dosyasını içe aktarın. Klasörleri sırayla çalıştırın (giriş → stok → cari → fiş akışı). Fiş akışı onay, iptal, yetersiz stok ve bakiye tutarlılığı senaryolarını içerir.

## Yetkilendirme

| Rol | Yetki |
|---|---|
| **Admin** | Şirket ve kullanıcı yönetimi. Stok/Cari/Fiş işlemlerinde hedef şirketi `X-Company-Id` başlığıyla belirtir; başlık yoksa listeleri tüm şirketler için görür. |
| **User** | Yalnızca bağlı olduğu şirketin Stok/Cari/Fiş işlemleri. Şirket bilgisi **token'dan** alınır, istemciden alınmaz. |

Arama `ILIKE` ile büyük/küçük harfe duyarsızdır. Şirket izolasyonu EF Core global query filter ile uygulanır; silinmiş (soft delete) kayıtlar da aynı filtreyle gizlenir.

## API özeti

| Endpoint | Açıklama |
|---|---|
| `POST /api/auth/login` | Giriş, JWT döner |
| `/api/companies`, `/api/users` | CRUD (yalnızca Admin) |
| `/api/stocks`, `/api/accounts` | CRUD + sayfalama/filtreleme/sıralama (Redis cache'li) |
| `GET /api/stocks/{id}/balance-check`, `/api/accounts/{id}/balance-check` | Saklanan bakiye ile hareket toplamını karşılaştırır |
| `/api/receipt-series` | Şirketin tanımlı fiş serilerini listeler (`?type=Sale` ile türe göre); Admin seçili şirket için seri tanımlar |
| `/api/receipts` | Taslak fiş CRUD + filtreleme (durum, tür, cari, stok, tarih). Fişte şirket için tanımlı ve fişin türüne ait bir seri seçilmelidir. |
| `POST /api/receipts/{id}/approve` | Fişi onaylar |
| `POST /api/receipts/{id}/cancel` | Onaylı fişi iptal eder |

Liste uç noktalarında `pageNumber`, `pageSize` (en fazla 100), `search`, `sortBy`, `desc` kullanılır. Yanıt `items`, `totalCount`, `pageNumber`, `pageSize`, `totalPages` döner.

### Fiş serileri

Admin, seri tanımlarken hedef şirketi `X-Company-Id` başlığıyla seçer:

```http
POST /api/receipt-series
Authorization: Bearer <admin-token>
X-Company-Id: <company-id>
Content-Type: application/json

{"series":"SAT","type":"Sale"}
```

Her seri tek bir fiş türüne aittir (`Sale` veya `Purchase`); böylece satış ve alış fişleri ayrı ve kesintisiz numaralanır (SAT-1, SAT-2… ve ALS-1, ALS-2…). Şirket kullanıcısı `GET /api/receipt-series?type=Sale` ile fişin türüne uygun serileri listeler ve fiş oluşturma isteğinde birini `series` olarak gönderir. Tanımlı olmayan veya fişin türüne ait olmayan seri reddedilir (400). Taslak fişin türü sonradan değiştirilemez; seri ve numara türe bağlıdır. Seri tanımları silinmez, böylece eski fiş numaraları korunur. Development seed verisinde DEMO şirketi için `SAT` (Sale) ve `ALS` (Purchase) serileri hazırdır.

## İş kuralları

- **Fiş durumları:** `Draft → Approved → Cancelled`. Yalnızca Draft düzenlenir ve silinir (soft delete). Onaylı fiş silinmez, iptal edilir. İptal geri alınamaz.
- **Onay (tek transaction):** `StockTrans` (satışta Out, alışta In) ve `ActTrans` (satışta Debit, alışta Credit) üretilir; miktar ve tutar her zaman pozitiftir, yön `direction` alanındadır; `stocks.balance` ve `accounts.balance` güncellenir. Hata olursa hepsi geri alınır.
- **Negatif stok:** Satış onayında stok yetersizse onay reddedilir (409). Alış iptali stoğu negatife düşürecekse iptal reddedilir.
- **İptal:** Hareket satırları silinmez ve yeni satır eklenmez; fiş `Cancelled` olduğu için hareketler geçersiz sayılır. Bakiyeler aynı transaction'da geri alınır. Hareketlerden toplam alan sorgular yalnızca fişsiz hareketleri ve `Approved` fişlerin hareketlerini saymalıdır (`AppDbContext.EffectiveStockTrans()` / `EffectiveActTrans()`).
- **Fişsiz hareket:** `receipt_id` zorunlu değildir; açılış veya düzeltme gibi fişe bağlı olmayan hareketler için boş bırakılabilir.
- **Sabit değerler enum'dır:** `UserRole`, `AccountType`, `ReceiptType`, `ReceiptStatus`, `TransDirection`, `StockDirection` (`StokCari.Core/Enums`). Veritabanında metin olarak saklanır, API'de metin olarak gelir/gider (ör. `"Sale"`).
- **Hareket tabloları salt eklenir:** `AuditSaveChangesInterceptor` bu tablolarda güncelleme/silmeyi engeller.
- **Seri / numara:** Seriler Admin tarafından şirket bazında `POST /api/receipt-series` ile tanımlanır (Admin `X-Company-Id` başlığını gönderir). Şirket kullanıcıları `GET /api/receipt-series` ile seçimleri listeler. Her seri tek bir fiş türüne aittir; fiş oluşturma isteğindeki seri bu listede tanımlı ve fişin türüne ait olmalıdır. Sistem numarayı şirket ve seri bazında `receipt_series` tablosunda EF Core üzerinden artırır. Sayaç `xmin` ile korunur, eşzamanlı çakışmada işlem birkaç kez yeniden denenir. `(company_id, series, number)` tekildir. Seri tanımı kaldırılmaz; geçmiş numaralar korunur.
- **Eşzamanlılık:** `stocks` ve `accounts` üzerinde optimistic concurrency (PostgreSQL `xmin` sistem kolonu, EF Core `RowVersion`); çakışmada 409 döner.
- **Silme kuralları:** Draft fişte kullanılan veya bakiyesi sıfır olmayan stok/cari silinemez. Aktif kullanıcısı veya Draft fişi olan şirket silinemez. Silinen şirketin kullanıcıları giriş yapamaz.
- **Fiş tek kalemlidir:** Stok, miktar ve birim fiyat doğrudan `receipts` üzerindedir (`receipt_lines` yoktur). KDV/iskonto yoktur.

## Cache (Redis, cache-aside)

- Yalnızca **Stok ve Cari** cache'lenir (detay + liste). Fiş ve hareketler cache'lenmez.
- Anahtarlar: `stock:item:{id}` ve `stock:list:{şirket|all}:v{versiyon}:{sorgu-hash}`.
- Kayıt değişince (CRUD, fiş onayı/iptali) şirket bazlı **versiyon sayacı** artırılır; eski liste anahtarları bir daha okunmaz ve TTL (10 dk) ile silinir.
- Invalidation **transaction commit edildikten sonra** yapılır. Onay/iptal kararları cache'ten değil veritabanından okunan bakiyeyle verilir.

## Loglama ve hata yönetimi

- **Serilog**, konsola ve günlük dönen dosyaya (`logs/`) yapılandırılmış JSON yazar. Ayarlar `appsettings.json > Serilog` bölümündedir.
- Her isteğe bir **Correlation ID** atanır (`X-Correlation-ID` başlığı gelirse o kullanılır) ve yanıt başlığında döner. Log satırlarına `CorrelationId`, `UserId`, `CompanyId` eklenir.
- Hatalar **RFC 7807 Problem Details** olarak döner ve `correlationId` içerir. Beklenmeyen hatalarda iç detay sızdırılmaz, stack trace yalnızca loga yazılır.

| Durum | Kod |
|---|---|
| Doğrulama hatası (Data Annotations) | 400 |
| Kimlik doğrulama / yetki yok | 401 / 403 |
| Kayıt bulunamadı | 404 |
| İş kuralı ihlali, stok yetersiz, eşzamanlılık, benzersizlik çakışması | 409 |
| Beklenmeyen hata | 500 |

## Yapılandırma

Gizli değerleri (`Jwt:Key`, `AutoMapper:LicenseKey`, bağlantı dizesi parolası, `Seed:AdminPassword`) repoya koymayın; user-secrets veya ortam değişkeni kullanın (ör. `Jwt__Key`, `AutoMapper__LicenseKey`, `ConnectionStrings__Default`).
