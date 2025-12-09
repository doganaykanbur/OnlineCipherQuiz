# Cipher Quiz

Gerçek zamanlı, oda tabanlı kriptografi quiz platformu.

## Özellikler
- **Realtime**: SignalR ile anlık iletişim.
- **Admin Paneli**: Oda yönetimi, canlı skor tablosu, proctor logları.
- **Proctoring**: Sekme değiştirme, kopyalama/yapıştırma tespiti.
- **Export**: Sonuçları PDF ve Excel olarak indirme.
- **Pratik Modu**: Tek kişilik alıştırma.
- **Neon Tema**: Modern Cyberpunk arayüz.
- **Desteklenen Şifreler**: Caesar, Vigenere, Base64, XOR, Hill, Monoalphabetic, Playfair, Transposition.

## Teknoloji Yığını
- **Backend**: .NET 9, ASP.NET Core Web API + SignalR
- **Frontend**: Blazor WebAssembly (Hosted)
- **Veri**: In-Memory Store
- **Export**: QuestPDF, ClosedXML

## Kurulum ve Çalıştırma

1. Gereksinimler: .NET 9 SDK.
2. Projeyi klonlayın veya indirin.
3. Terminali açın ve proje dizinine gidin:
   ```bash
   cd CipherQuiz
   ```
4. Projeyi çalıştırın:
   ```bash
   dotnet run --project Server/CipherQuiz.Server.csproj
   ```
5. Tarayıcıda `https://localhost:7120` (veya terminalde belirtilen port) adresine gidin.

## Kullanım
1. **Admin**: "Oda Kur" butonuna tıklayın. Oda adını girin. Oluşan kodu katılımcılarla paylaşın.
2. **Oyuncu**: Ana sayfada kodu girip "Odaya Katıl" deyin. Admin onayını bekleyin.
3. **Başlangıç**: Admin quizi başlattığında, oyuncuların karşısına "YARIŞMA BAŞLIYOR" ekranı gelir. "BAŞLA" butonuna tıklayarak tam ekran moduna geçilir ve ilk soru görüntülenir.
4. **Akış**: Oyuncular soruları çözer -> Admin quizi bitirir -> Sonuçlar indirilir.

## Notlar
- PDF export için QuestPDF Community lisansı kullanılmıştır.
- PDF export için QuestPDF Community lisansı kullanılmıştır.
- Veriler sunucu kapandığında silinir (In-Memory).
- **Not**: Pano (Clipboard) işlemleri için güvenli olmayan bağlantılarda (HTTP) yedek mekanizma devreye girer. En iyi deneyim için HTTPS önerilir.
