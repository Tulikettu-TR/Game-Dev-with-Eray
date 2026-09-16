# CLAWBLES — Night Shift
## Oyun kararı
1–4 kişinin dev bir oyuncak makinesinde vardiya tamamladığı, birinci şahıs co-op oyun. Önerilen ana deneyim 2–4 kişi; solo modu öğrenmek için kullanılabilir. Bir kişi yerdeki konsolda vinci yönetir. Operatör gelen oyuncu seslerini duyamaz. Saha ekibi yükü sağlamlaştırır, yol gösterir ve yanlış bırakılan oyuncağın altında kalmamaya çalışır.

Tek cümlelik sunum: “Arkadaşların yükün altında, sen düğmenin başındasın. Ve onları duyamıyorsun.”

Bu tasarım tercihi, Steam başarısı tahmini değildir. [PEAK](https://store.steampowered.com/app/3527290/PEAK/) arkadaş yardımı ve değişen parkuru; [Big Walk](https://store.steampowered.com/app/1478500/Big_Walk/) birlikte keşfetme ve iletişimi; [LOCKDOWN Protocol](https://store.steampowered.com/app/2780980/LOCKDOWN_Protocol/) iletişime bağlı farklı rolleri öne çıkarıyor. CLAWBLES için çıkardığım tasarım sonucu: az sayıda anlaşılır eylem, arkadaşın kararına ihtiyaç ve oyuncuların kendi yarattığı komik hatalar.

## Şu an uygulanan oturum
- Hazırlık ekranı: host vardiyayı başlatır; başlamadan süre işlemez.
- First Shift: 300 saniyede 1 Honey Duck teslimatı.
- Glass Parade: 360 saniyede 2 Button Bear; vinç daha hızlıdır, erken frenlemek gerekir.
- Overtime: 420 saniyede 3 Long-Ear Bunny; hava akımı salınımı etkiler.
- Lazer teması yükü sıfırlar ve 15 saniye götürür.
- Her teslimat 100 ortak kredi kazandırır. Vardiya sonunda süreye bağlı ek kredi ve hata sayısına göre 1–3 yıldız gelir.
- Vardiyalar arasında 150 krediye grip liner, rigging kit veya 60 saniyelik overtime pass alınabilir. Hiçbirini almadan devam etmek de mümkündür.
- Üç vardiya tamamlanınca sonuç ekranı açılır. Süre biterse oturum kaybedilir. Yeni oturum yeni bir tohumla başlar.
- En iyi yıldız derecesi ve tamamlanan oturum sayısı yerel olarak saklanır.

Sipariş oyuncakları farklı çarpışma boyutlarına sahiptir. Yerdeki peluşlar oyuncuyu engeller ve kancayla taşınabilir. Yalnızca hareket eden peluşlarda düşme hesabı çalışır; değişen konumlar host tarafından paylaşılır. Görüntüleme aynı tür oyuncakları GPU instancing ile gruplar. Dünya düzeni host'un tohumu ile bütün oyunculara eşit şekilde üretilir. Vardiya, süre, para, yükseltmeler ve sonuç host tarafından belirlenir.

## Görsel ve işitsel yön
Orta poligonlu, yuvarlak hatlı, kumaş dokulu peluşlar; sıcak plastik ve metal; şeffaf cam ve okunabilir işaretler. Yakından taşınan sipariş modelleri ve dışa aktarılan peluş prefabları, kalabalık dekorlardan daha ayrıntılıdır. Vardiyalarda kabin rengi değişir. FPS kolları, karakterlerin yük tutma/yürüme hareketleri ve geçici ragdoll korunur.

Teslimat, pençe kilidi, hata ve başarı için özgün sentezlenmiş kısa arcade sesleri; konumsal motor sesi bulunur. Mikrofon sesi ayrı sistemdir. Fare hassasiyeti, ters bakış ve efekt ses düzeyi menüden ayarlanıp kaydedilebilir.

## Kullanım
Unity 6000.6.0f1 ile Assets/Clawbles/Scenes/Clawbles.unity sahnesini aç. Play → HOST / SOLO → START SHIFT.
Diğer oyuncular aynı v6 build ile host'un LAN IP adresine JOIN FRIEND kullanarak bağlanır.
Windows build: D:\Belge\Unity\CLAWBLES\Builds\Windows\CLAWBLES.exe.
WASD/fare hareket ve bakış; E etkileşim; F yükü sağlamlaştırma; operatörde Space/Ctrl yükseltme/alçaltma; Q bırakma; V basılı yakınlık sesi; T/Enter yazılı sohbet; 1–8 hızlı yön mesajları.

## Steam sürümünden önce kalan çalışma
Bu sürüm oynanabilir alfa; yayımlanmaya hazır tam ürün olarak sunulmamalı. Henüz Steam daveti, Steam üzerinden internet oturumu, başarımlar ve bulut kaydı uygulanmış değil. Şu an LAN bağlantısı kullanılıyor.
Gerçek mikrofonla farklı cihazlar arasında test, düşük donanımda ölçüm, uzun oturumlarda bağlantı kaybı denemeleri, kontrolcü/erişilebilirlik çalışması ve arkadaş gruplarıyla kör oynanış testleri gerekiyor. Yeni oyuncuların işaretler olmadan yönünü bulabilmesi ve yan salınım bölmesinin zorluğu insanlarla sınanmalı.
Steam mağaza sayfası, yayımlama hesabı ve App ID bu projeye bağlanmış değil. Mağaza sunumu, fiyat ve çıkış kararı bu testlerin sonucuyla belirlenmeli; satış garantisi verilemez.


## Sıkışmadan kurtulma
Kanca ve yük, küçük çarpışma adımlarıyla engel boyunca kayabilir; engellenen eksen diğer yönleri kilitlemez. Engelin içine giren oyuncu boş bir konuma çıkarılır. Yük veya kanca tarafından duvarın içine çekilecek oyuncu otomatik olarak tutuşunu bırakır.
R: operatör için kancayı sıfırlar ve yükü doğduğu bölmeye geri yollar; saha oyuncusu için konsola dönüş sağlar. Operatörün yerdeki konumu değişmez. Puan ve mevcut vardiya sıfırlanmaz. Kurtarma işlemleri arasında 5 saniye beklenir; aktif vardiyada her kurtarma 10 saniye götürür.

## v5 — Zorunlu parkur ve taşınabilir peluşlar
İlk sipariş, kuzeyde duvarlarla çevrili yük bölmesinde başlar. Yük için çıkış lazerli penceredir; arkadaki dar kapı saha oyuncuları içindir. Teslimat tarafına geçmek için kabinin tamamını bölen ikinci duvardaki pencereye ulaşmak gerekir. Yan salınım bölmesi sonraki teslimatlarda kullanılmaya devam eder.

Pençenin merkezi en fazla 6,8 metreye çıkar; salınımın yükselme etkisi de bu sınırla kısıtlanır. Bölme duvarları 8,5 metre olduğundan üstten aşılmaz. Space yükseltir, Ctrl alçaltır.

Dik oyuncak sütunları kaldırıldı. Tohuma göre farklı boy, açı ve yoğunlukta, zemine doğrudan oturan peluş kümeleri oluşur. Peluşlar görünmez kutular üzerine istiflenmez. Oyuncular bu oyuncakların içinden geçemez; üzerlerine çıkabilirler. Kancayı oyuncağın üstüne hizalayıp E ile tutabilir, tekrar E ile bırakabilirsin. Yanlış peluş deliğe düşerse puan kazandırmaz ve yerine döner. Düşen peluş oyuncuya çarparsa kısa süreli düşürür.

Performans için bütün peluşlara sürekli Rigidbody simülasyonu uygulanmaz. Çarpışma hacimleri düşük maliyetli kutulardır; yumuşak kumaş fiziği uygulanmaz. Eski build ile bağlantı kabul edilmez.

v6 düzeltmesi: Peluşların görünür modelinin en alt noktası zemine hizalanır; birbirinin üstünde havada kalan başlangıç yerleşimleri kaldırılmıştır.
