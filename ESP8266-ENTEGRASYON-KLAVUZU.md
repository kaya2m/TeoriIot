# ESP8266 Entegrasyon Klavuzu

## Genel Bakış

Bu klavuz, ESP8266 mikro denetleyicilerini TeoriIot API'si ile entegre etmek için gerekli tüm bilgileri içermektedir. ESP8266 cihazları, sensör verilerini HTTP POST istekleri ile API'ye gönderir.

## Donanım Gereksinimleri

### Desteklenen ESP8266 Modelleri
- ESP8266 NodeMCU
- ESP8266 Wemos D1 Mini
- ESP8266-01, ESP8266-12E
- Diğer ESP8266 tabanlı geliştirme kartları

### Pin Konfigürasyonu
ESP8266'da kullanılabilir dijital pinler:
- D0 (GPIO16) - Pin Index: 0
- D1 (GPIO5) - Pin Index: 1
- D2 (GPIO4) - Pin Index: 2
- D3 (GPIO0) - Pin Index: 3
- D4 (GPIO2) - Pin Index: 4
- D5 (GPIO14) - Pin Index: 5
- D6 (GPIO12) - Pin Index: 6
- D7 (GPIO13) - Pin Index: 7
- D8 (GPIO15) - Pin Index: 8

## Arduino IDE Kurulumu

### 1. ESP8266 Board Manager
Arduino IDE > File > Preferences > Additional Boards Manager URLs:
```
https://arduino.esp8266.com/stable/package_esp8266com_index.json
```

### 2. Gerekli Kütüphaneler
```cpp
#include <ESP8266WiFi.h>
#include <ESP8266HTTPClient.h>
#include <WiFiClient.h>
```

Tools > Manage Libraries > Install:
- ESP8266WiFi
- ESP8266HTTPClient

## KapilarID Algoritması

### Encoding (ESP8266'da)
```cpp
// Pin durumlarını KapilarID'ye dönüştürme
uint32_t generateKapilarId(uint8_t pinStates) {
    uint32_t kapilarId = 0;
    
    for (int i = 0; i < 8; i++) {
        if (pinStates & (1 << i)) {
            kapilarId |= (1UL << (i * 4));
        }
    }
    
    return kapilarId;
}
```

### Örnek Kullanım
```cpp
uint8_t pinStates = 0;

// Pin durumlarını oku (örnek: D1, D2, D5 HIGH)
if (digitalRead(D1) == HIGH) pinStates |= (1 << 1);
if (digitalRead(D2) == HIGH) pinStates |= (1 << 2);
if (digitalRead(D5) == HIGH) pinStates |= (1 << 5);

uint32_t kapilarId = generateKapilarId(pinStates);
```

## Temel ESP8266 Kodu

### 1. Tam Örnek Kod
```cpp
#include <ESP8266WiFi.h>
#include <ESP8266HTTPClient.h>
#include <WiFiClient.h>

// WiFi ayarları
const char* ssid = "WiFi_Adi";
const char* password = "WiFi_Sifresi";

// API ayarları
const char* apiUrl = "http://192.168.1.100:5000/ingest/";
const char* apiKey = "your-device-api-key-here";
const int nodeNum = 1; // Cihaz node numarası (1-255)

// Pin tanımları
const int inputPins[] = {D1, D2, D3, D4, D5, D6, D7, D8};
const int pinCount = 8;

// Son pin durumları (değişiklik tespiti için)
uint8_t lastPinStates = 0;

WiFiClient wifiClient;
HTTPClient http;

void setup() {
  Serial.begin(115200);
  
  // Pin modlarını ayarla
  for (int i = 0; i < pinCount; i++) {
    pinMode(inputPins[i], INPUT_PULLUP);
  }
  
  // WiFi bağlantısı
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.println("WiFi'ye bağlanıyor...");
  }
  Serial.println("WiFi bağlı!");
  Serial.print("IP: ");
  Serial.println(WiFi.localIP());
}

void loop() {
  uint8_t currentPinStates = readPinStates();
  
  // Pin durumu değiştiyse API'ye gönder
  if (currentPinStates != lastPinStates) {
    uint32_t kapilarId = generateKapilarId(currentPinStates);
    sendToApi(kapilarId);
    lastPinStates = currentPinStates;
  }
  
  delay(100); // 100ms bekleme (debounce için)
}

uint8_t readPinStates() {
  uint8_t states = 0;
  for (int i = 0; i < pinCount; i++) {
    if (digitalRead(inputPins[i]) == LOW) { // Pull-up direnci kullanıldığı için LOW = aktif
      states |= (1 << i);
    }
  }
  return states;
}

uint32_t generateKapilarId(uint8_t pinStates) {
  uint32_t kapilarId = 0;
  for (int i = 0; i < 8; i++) {
    if (pinStates & (1 << i)) {
      kapilarId |= (1UL << (i * 4));
    }
  }
  return kapilarId;
}

void sendToApi(uint32_t kapilarId) {
  if (WiFi.status() == WL_CONNECTED) {
    String url = String(apiUrl) + String(nodeNum);
    String payload = "kapilar_id=" + String(kapilarId);
    
    http.begin(wifiClient, url);
    http.addHeader("Content-Type", "application/x-www-form-urlencoded");
    http.addHeader("X-API-Key", apiKey);
    
    int httpResponseCode = http.POST(payload);
    
    if (httpResponseCode == 204) {
      Serial.println("Veri başarıyla gönderildi");
    } else {
      Serial.print("HTTP Hata: ");
      Serial.println(httpResponseCode);
    }
    
    http.end();
  }
}
```

## Gelişmiş Özellikler

### 1. Deep Sleep Modu (Pil Tasarrufu)
```cpp
#include <ESP8266WiFi.h>

void setup() {
  // ... diğer kodlar
  
  // Verileri gönder
  sendDataToApi();
  
  // 10 saniye deep sleep
  ESP.deepSleep(10 * 1000000); // mikrosaniye
}

void loop() {
  // Deep sleep modunda loop() çalışmaz
}
```

### 2. OTA (Over-The-Air) Update
```cpp
#include <ESP8266WiFi.h>
#include <ESP8266HTTPClient.h>
#include <ESP8266httpUpdate.h>

void checkForUpdates() {
  WiFiClient client;
  ESPhttpUpdate.update(client, "http://192.168.1.100/firmware.bin");
}
```

### 3. Watchdog Timer
```cpp
#include <Ticker.h>

Ticker watchdog;

void setup() {
  watchdog.attach(30, watchdogReset); // 30 saniyede bir reset
}

void watchdogReset() {
  ESP.restart();
}

void loop() {
  watchdog.detach();
  // Ana kod
  watchdog.attach(30, watchdogReset);
}
```

## Ağ Konfigürasyonu

### 1. Statik IP Ayarlama
```cpp
IPAddress local_IP(192, 168, 1, 100);
IPAddress gateway(192, 168, 1, 1);
IPAddress subnet(255, 255, 255, 0);
IPAddress primaryDNS(8, 8, 8, 8);

void setup() {
  if (!WiFi.config(local_IP, gateway, subnet, primaryDNS)) {
    Serial.println("STA Failed to configure");
  }
  
  WiFi.begin(ssid, password);
}
```

### 2. WiFi Manager (Otomatik Konfigürasyon)
```cpp
#include <WiFiManager.h>

void setup() {
  WiFiManager wifiManager;
  
  // Reset ayarları (test için)
  // wifiManager.resetSettings();
  
  // Otomatik bağlan veya hotspot oluştur
  if (!wifiManager.autoConnect("ESP8266_Setup")) {
    Serial.println("Bağlantı kurulamadı");
    delay(3000);
    ESP.restart();
  }
}
```

## Hata Ayıklama

### 1. Serial Monitor Çıktıları
```cpp
void debugPrint(String message) {
  Serial.print("[DEBUG] ");
  Serial.print(millis());
  Serial.print(": ");
  Serial.println(message);
}

void sendToApi(uint32_t kapilarId) {
  debugPrint("API'ye gönderiliyor: " + String(kapilarId));
  
  // HTTP istek kodu...
  
  if (httpResponseCode == 204) {
    debugPrint("Başarılı!");
  } else {
    debugPrint("Hata: " + String(httpResponseCode));
    debugPrint("Yanıt: " + http.getString());
  }
}
```

### 2. LED Durum Göstergesi
```cpp
const int statusLED = D4; // Yerleşik LED

void setup() {
  pinMode(statusLED, OUTPUT);
  digitalWrite(statusLED, HIGH); // Başlangıçta kapalı
}

void indicateSuccess() {
  for (int i = 0; i < 3; i++) {
    digitalWrite(statusLED, LOW);  // LED aç
    delay(100);
    digitalWrite(statusLED, HIGH); // LED kapat
    delay(100);
  }
}

void indicateError() {
  for (int i = 0; i < 5; i++) {
    digitalWrite(statusLED, LOW);
    delay(50);
    digitalWrite(statusLED, HIGH);
    delay(50);
  }
}
```

## Performans Optimizasyonu

### 1. HTTP Keep-Alive
```cpp
HTTPClient http;

void setup() {
  http.setReuse(true); // Bağlantıyı yeniden kullan
}

void sendToApi(uint32_t kapilarId) {
  // Her seferinde yeni bağlantı kurmak yerine
  // mevcut bağlantıyı kullan
}
```

### 2. Batch Gönderim
```cpp
String batchData = "";
int batchCount = 0;
const int MAX_BATCH = 5;

void addToBatch(uint32_t kapilarId) {
  batchData += "kapilar_id=" + String(kapilarId) + "&";
  batchCount++;
  
  if (batchCount >= MAX_BATCH) {
    sendBatchToApi();
    batchData = "";
    batchCount = 0;
  }
}
```

## Güvenlik

### 1. HTTPS Kullanımı
```cpp
#include <WiFiClientSecure.h>

WiFiClientSecure httpsClient;

void setup() {
  httpsClient.setInsecure(); // Test için (üretimde certificate kullanın)
}
```

### 2. API Key Güvenliği
```cpp
// EEPROM'da API key saklama
#include <EEPROM.h>

void saveApiKey(String key) {
  EEPROM.begin(512);
  for (int i = 0; i < key.length(); i++) {
    EEPROM.write(i, key[i]);
  }
  EEPROM.write(key.length(), 0);
  EEPROM.commit();
}

String loadApiKey() {
  EEPROM.begin(512);
  String key = "";
  for (int i = 0; i < 64; i++) {
    char c = EEPROM.read(i);
    if (c == 0) break;
    key += c;
  }
  return key;
}
```

## Test ve Doğrulama

### 1. API Test Kodu
```cpp
void testApiConnection() {
  Serial.println("API bağlantısı test ediliyor...");
  
  // Test verisi gönder
  uint32_t testKapilarId = 0x11111111;
  sendToApi(testKapilarId);
  
  delay(1000);
  
  // Başka bir test
  testKapilarId = 0x00000000;
  sendToApi(testKapilarId);
}
```

### 2. Pin Test Modu
```cpp
bool testMode = false;

void loop() {
  if (testMode) {
    // Tüm pinleri sırayla test et
    for (int i = 0; i < pinCount; i++) {
      uint8_t testStates = (1 << i);
      uint32_t kapilarId = generateKapilarId(testStates);
      sendToApi(kapilarId);
      delay(1000);
    }
    testMode = false;
  }
  
  // Normal işlem...
}
```

## Sorun Giderme

### Yaygın Problemler

1. **WiFi Bağlantı Problemi**
   - SSID ve parola kontrolü
   - Sinyal gücü kontrolü
   - Router ayarları (MAC filtresi, vb.)

2. **HTTP 401 Hatası**
   - API key kontrolü
   - Header formatı kontrolü

3. **HTTP 404 Hatası**
   - URL kontrolü
   - Node numarası kontrolü

4. **Veri Gönderilmiyor**
   - Pin konfigürasyonu kontrolü
   - KapilarID hesaplama kontrolü

### Debug Komutları
```cpp
void printSystemInfo() {
  Serial.println("=== Sistem Bilgileri ===");
  Serial.print("Chip ID: ");
  Serial.println(ESP.getChipId());
  Serial.print("Flash Size: ");
  Serial.println(ESP.getFlashChipSize());
  Serial.print("Free Heap: ");
  Serial.println(ESP.getFreeHeap());
  Serial.print("WiFi Status: ");
  Serial.println(WiFi.status());
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());
  Serial.println("========================");
}
```

## Üretim Dağıtımı

### 1. Board Ayarları
- Board: Generic ESP8266 Module
- Flash Mode: DIO
- Flash Size: 4M (3M SPIFFS)
- CPU Frequency: 80MHz
- Upload Speed: 115200

### 2. Toplu Programlama
```bash
# esptool kullanarak
esptool.py --chip esp8266 --port COM3 write_flash 0x00000 firmware.bin
```

### 3. Kalite Kontrol Checklist
- [ ] WiFi bağlantısı test edildi
- [ ] API bağlantısı test edildi
- [ ] Tüm pinler test edildi
- [ ] Deep sleep modu test edildi (varsa)
- [ ] Güç tüketimi ölçüldü
- [ ] Uzun süreli çalışma testi yapıldı