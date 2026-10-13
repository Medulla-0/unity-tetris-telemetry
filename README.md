# Unity Tetris & Real-Time Telemetry System

Unity (C#) ile geliştirilmiş 2D Tetris oyunu ve oyun içi verileri REST API üzerinden gerçek zamanlı olarak SQLite veritabanına kaydeden Node.js tabanlı telemetri paneli.

## Proje Yapısı

- **`unity/`** : Unity 2D Tetris istemcisi (`Assets`, `Packages`, `ProjectSettings`).
  - `Assets/Scripts/BoardManager.cs` : 7-Bag blok üretimi, Ghost Piece (hayalet blok), Wall-Kick döndürme, Hold (blok saklama) ve satır temizleme mekanikleri.
  - `Assets/Scripts/GameManager.cs` : Dinamik skor, combo çarpanı ve seviye ilerleme yönetimi.
  - `Assets/Scripts/TelemetryManager.cs` : Oyun başladığında benzersiz **Oyun ID** (`OYUN-XXXXXX`) üretimi ve oyun metriklerinin sunucuya canlı aktarımı.
- **`web/`** : Node.js ve yerleşik SQLite (`node:sqlite`) tabanlı telemetri sunucusu ve web arayüzü.

## Kurulum ve Çalıştırma

### 1. Web Telemetri Sunucusu
Node.js (v22+) içindeki yerleşik modülleri kullanır, harici paket kurulumu gerektirmez:

```bash
cd web
node server.js
```

veya Windows üzerinde `web/baslat.bat` dosyasını çalıştırabilirsiniz. Panel `http://localhost:8080` adresinde açılır.

### 2. Unity İstemcisi
`unity/` klasörünü Unity Hub üzerinden açıp oyunu başlattığınızda:
- Otomatik olarak yeni bir **Oyun ID** (`OYUN-XXXXXX`) atanır ve anında web paneline eklenir.
- Oyun süresince üretilen bloklar, yapılan hamleler (sol/sağ, döndürme, soft drop, hard drop, hold), temizlenen satırlar, skor, seviye ve APM değerleri canlı olarak güncellenir.

## API Uç Noktaları

- `POST /api/telemetry` : Unity istemcisinden gelen oyun verilerini SQLite veritabanına kaydeder veya günceller.
- `GET /api/stats` : Genel özet metrikleri, en yüksek skorları ve canlı oyun akışını döndürür.
- `DELETE /api/reset` : Kayıtlı oyun verilerini temizler.
