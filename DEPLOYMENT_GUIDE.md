# Cipher Quiz Projesi Sunucu Kurulum Rehberi

Bu proje **ASP.NET Core Hosted Blazor WebAssembly** yapısındadır. Yani hem bir **Server** (API ve Statik Dosya Sunucusu) hem de bir **Client** (Tarayıcıda çalışan uygulama) içerir.

Projeyi bir sunucuya (Windows veya Linux) aktarmak için aşağıdaki adımları izleyebilirsiniz.

---

> [!IMPORTANT]
> **HTTPS Gerekliliği**: Uygulama, Pano (Clipboard) ve Tam Ekran (Fullscreen) API'lerini yoğun olarak kullanır. Modern tarayıcılar bu özelliklerin tam fonksiyonlu çalışması için **HTTPS (Güvenli Bağlantı)** zorunluluğu koşabilir. Yerel ağda (HTTP) çalışırken bazı özellikler için yedek mekanizmalar (fallback) devreye girse de, prodüksiyon ortamında mutlaka SSL sertifikası kullanılması önerilir.

## 1. Projeyi Yayınlama (Publish)

Öncelikle projenin "Release" sürümünü oluşturmanız gerekir. Bu işlem, gereksiz dosyaları temizler ve projeyi çalışmaya hazır hale getirir.

1.  Terminali açın ve proje ana dizinine (Solution dosyasının olduğu yer) gelin.
2.  Aşağıdaki komutu çalıştırın:

```bash
dotnet publish Server/CipherQuiz.Server.csproj -c Release -o ./publish
```

Bu komut tamamlandığında, ana dizinde `publish` adında bir klasör oluşacaktır. Sunucuya kopyalamanız gereken klasör **sadece bu klasördür**.

---

## 2. Windows Sunucu (IIS) Kurulumu

Eğer Windows Server kullanacaksanız:

### Gereksinimler
- **.NET 9.0 Hosting Bundle**: Sunucuda yüklü olmalıdır. [Buradan indirebilirsiniz](https://dotnet.microsoft.com/download/dotnet/9.0).
- **IIS (Internet Information Services)**: Sunucuda aktif olmalıdır.

### Adımlar
1.  **Dosyaları Kopyala:** `publish` klasörünün içindekileri sunucuda `C:\inetpub\wwwroot\CipherQuiz` gibi bir klasöre kopyalayın.
2.  **IIS'i Açın:** IIS Yöneticisi'ni başlatın.
3.  **Site Ekle:** "Sites"a sağ tıklayıp "Add Website" deyin.
    - **Site Name:** CipherQuiz
    - **Physical Path:** Dosyaları attığınız klasör (`C:\inetpub\wwwroot\CipherQuiz`)
    - **Port:** 80 veya istediğiniz bir port.
4.  **Application Pool Ayarı:** Oluşturulan Application Pool'un ".NET CLR Version" ayarını **"No Managed Code"** olarak seçin (Çünkü .NET Core/5+ kullanıyoruz).
5.  **Siteyi Başlat:** Siteyi başlatın ve tarayıcıdan erişmeyi deneyin.

---

## 3. Linux Sunucu (Ubuntu/Nginx) Kurulumu

Eğer Linux (örneğin Ubuntu) kullanacaksanız:

### Gereksinimler
- **.NET 9.0 Runtime**:
  ```bash
  sudo apt-get update && \
  sudo apt-get install -y dotnet-runtime-9.0 aspnetcore-runtime-9.0
  ```
- **Nginx**:
  ```bash
  sudo apt install nginx
  ```

### Adımlar
1.  **Dosyaları Kopyala:** `publish` klasörünü sunucuya (örn: `/var/www/cipherquiz`) aktarın (FTP veya SCP ile).
2.  **Servis Oluştur:** Uygulamanın arka planda sürekli çalışması için bir servis dosyası oluşturun:
    ```bash
    sudo nano /etc/systemd/system/cipherquiz.service
    ```
    İçeriği:
    ```ini
    [Unit]
    Description=Cipher Quiz Web App

    [Service]
    WorkingDirectory=/var/www/cipherquiz
    ExecStart=/usr/bin/dotnet /var/www/cipherquiz/CipherQuiz.Server.dll
    Restart=always
    # Restart service after 10 seconds if the dotnet service crashes:
    RestartSec=10
    KillSignal=SIGINT
    SyslogIdentifier=cipherquiz
    User=www-data
    Environment=ASPNETCORE_ENVIRONMENT=Production
    Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

    [Install]
    WantedBy=multi-user.target
    ```
3.  **Servisi Başlat:**
    ```bash
    sudo systemctl enable cipherquiz.service
    sudo systemctl start cipherquiz.service
    ```
4.  **Nginx Ayarı (Reverse Proxy):** Dış dünyadan gelen istekleri uygulamanıza (varsayılan port 5000) yönlendirin.
    ```bash
    sudo nano /etc/nginx/sites-available/default
    ```
    `server` bloğu içine şunu ekleyin/düzenleyin:
    ```nginx
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    ```
5.  **Nginx'i Yeniden Başlat:**
    ```bash
    sudo service nginx restart
    ```

---

## 4. Docker ile Kurulum (Alternatif)

Eğer sunucuda Docker varsa, projeyi bir konteyner olarak da çalıştırabilirsiniz.

1.  Ana dizinde bir `Dockerfile` oluşturun (projede yoksa).
2.  Image oluşturun: `docker build -t cipherquiz .`
3.  Çalıştırın: `docker run -d -p 80:8080 cipherquiz`
