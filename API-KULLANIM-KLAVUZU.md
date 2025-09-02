# TeoriIot API Kullanım Klavuzu

## Genel Bakış

TeoriIot API, IoT cihazlarından gelen verileri almak ve yönetmek için geliştirilmiş bir .NET 8 Minimal API'sidir. Bu API, ESP8266 cihazlarından HTTP POST ile veri alımını destekler ve JWT tabanlı kimlik doğrulama sistemi kullanır.

## Hızlı Başlangıç

### 1. Sistemi Çalıştırma

```bash
# Docker ile çalıştırma
docker-compose up -d

# Yerel geliştirme
dotnet run --project src/IotIngest.Api
```

### 2. Veritabanı Kurulumu

SQL Server'da `sql/init.sql` dosyasını çalıştırarak veritabanını oluşturun.

### 3. API'ye Erişim

- **Base URL**: `http://localhost:5000` (yerel) veya `http://localhost:8080` (Docker)
- **Swagger UI**: `http://localhost:5000/swagger`

## Kimlik Doğrulama

### Giriş Yapma

```http
POST /auth/login
Content-Type: application/json

{
    "username": "admin",
    "password": "admin123"
}
```

**Yanıt:**
```json
{
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "a1b2c3d4e5f6...",
    "expiresIn": 3600,
    "user": {
        "id": 1,
        "username": "admin",
        "displayName": "System Administrator",
        "roles": ["Admin"]
    }
}
```

### Token Yenileme

```http
POST /auth/refresh
Content-Type: application/json

{
    "refreshToken": "a1b2c3d4e5f6..."
}
```

### Authorization Header

Tüm korumalı endpoint'ler için:
```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

## API Endpoint'leri

### IoT Veri Alımı

#### ESP8266'dan Veri Alma
```http
POST /ingest/{nodeNum}
Content-Type: application/x-www-form-urlencoded
X-API-Key: your-device-api-key

kapilar_id=12345
```

**Parametreler:**
- `nodeNum`: Cihaz node numarası (1-255)
- `kapilar_id`: Pin durumu bilgisini içeren kodlanmış ID

**Yanıt:** `204 No Content` (ultra hızlı işleme için)

### Cihaz Yönetimi

#### Tüm Cihazları Listele
```http
GET /devices
Authorization: Bearer {token}
```

#### Kullanıcının Cihazlarını Listele
```http
GET /devices/user
Authorization: Bearer {token}
```

#### Cihaz Oluştur
```http
POST /devices
Authorization: Bearer {token}
Content-Type: application/json

{
    "name": "Salon Sensörleri",
    "apiKey": "unique-api-key-123",
    "isActive": true
}
```

### Giriş (Input) Yönetimi

#### Cihaz Girişlerini Listele
```http
GET /devices/{deviceId}/inputs
Authorization: Bearer {token}
```

#### Giriş Ayarlarını Güncelle
```http
PUT /devices/{deviceId}/inputs/{pinIndex}
Authorization: Bearer {token}
Content-Type: application/json

{
    "name": "Kapı Sensörü",
    "enabled": true,
    "ncLogic": true,
    "debounceMs": 50,
    "description": "Ana giriş kapısı manyetik sensörü"
}
```

### Olay (Event) Yönetimi

#### Son Olayları Listele
```http
GET /events/{nodeNum}/recent?minutes=60
Authorization: Bearer {token}
```

### Kullanıcı Yönetimi (Admin)

#### Tüm Kullanıcıları Listele
```http
GET /users
Authorization: Bearer {token}
```

#### Kullanıcı Oluştur
```http
POST /users
Authorization: Bearer {token}
Content-Type: application/json

{
    "username": "operator1",
    "displayName": "Operatör 1",
    "password": "securePass123",
    "isActive": true,
    "roleNames": ["Operator"],
    "deviceIds": [1, 2, 3]
}
```

#### Kullanıcı Güncelle
```http
PUT /users/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
    "displayName": "Güncel İsim",
    "password": "newPassword123",
    "isActive": true,
    "roleNames": ["Viewer"],
    "deviceIds": [1, 2]
}
```

#### Kullanıcı Sil
```http
DELETE /users/{id}
Authorization: Bearer {token}
```

#### Rolleri Listele
```http
GET /users/roles
Authorization: Bearer {token}
```

## Roller ve Yetkiler

### Roller
- **Admin**: Tüm işlemler (kullanıcı yönetimi, cihaz yönetimi)
- **Operator**: Cihaz ve input yönetimi, olay görüntüleme
- **Viewer**: Sadece veri görüntüleme

### Yetki Matrisi

| İşlem | Admin | Operator | Viewer |
|-------|-------|----------|---------|
| Veri Görüntüleme | ✅ | ✅ | ✅ |
| Cihaz Yönetimi | ✅ | ✅ | ❌ |
| Input Ayarları | ✅ | ✅ | ❌ |
| Kullanıcı Yönetimi | ✅ | ❌ | ❌ |

## Hata Kodları

| HTTP Kodu | Açıklama |
|-----------|----------|
| 200 | Başarılı |
| 201 | Oluşturuldu |
| 204 | İçerik Yok (IoT veri alımı) |
| 400 | Hatalı İstek |
| 401 | Kimlik Doğrulama Gerekli |
| 403 | Yetkisiz |
| 404 | Bulunamadı |
| 409 | Çakışma (Kullanıcı adı zaten var) |
| 500 | Sunucu Hatası |

## Hata Yanıt Formatı

```json
{
    "error": "Invalid operation",
    "message": "Username 'test' already exists. Please choose a different username."
}
```

## Rate Limiting ve Performans

- IoT veri alımı ultra hızlı işleme için optimize edilmiştir
- KapilarID dekodlaması için özel algoritma kullanılır
- Dapper ORM ile yüksek performanslı veri erişimi
- Connection pooling desteklenir

## Güvenlik

- JWT token'lar 1 saat geçerli
- Refresh token'lar 7 gün geçerli
- BCrypt ile parola şifreleme
- API key'lerle cihaz kimlik doğrulaması
- Role-based access control (RBAC)

## Monitoring ve Logging

- Structured logging ile Serilog
- Tüm API istekleri loglanır
- Hata durumları detaylı loglanır
- Performance metrikleri izlenebilir

## Veritabanı Yapısı

### Ana Tablolar
- `iot.Devices`: Cihaz bilgileri
- `iot.Inputs`: Pin/giriş konfigürasyonları
- `iot.Events`: Olay kayıtları
- `iot.AuthUsers`: Kullanıcı bilgileri
- `iot.AuthRoles`: Rol tanımları
- `iot.AuthRefreshTokens`: Refresh token'lar

## Destek ve Geliştirme

### Yerel Geliştirme
```bash
dotnet watch run --project src/IotIngest.Api
```

### Test Çalıştırma
```bash
dotnet test
```

### Veritabanı Bağlantı Dizisi
`appsettings.json` dosyasında:
```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=localhost;Database=TeoriIot;Trusted_Connection=true;TrustServerCertificate=true;"
    }
}
```