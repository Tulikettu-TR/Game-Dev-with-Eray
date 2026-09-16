# CLAWBLES

Arkadaşlarla oynanan, geliştirme aşamasında bir Unity oyunu. Bu depo oynanabilir EXE yerine düzenlenebilir Unity projesini içerir.

## Projeyi açma

1. Unity Hub üzerinden **Unity 6.6 / 6000.6.0f1** sürümünü kurun. Windows çıktısı almak için Windows Build Support (Mono) modülünü ekleyin.
2. Git ve Git LFS kurun. `git lfs install` komutunu bir kez çalıştırın.
3. Bu depoyu GitHub Desktop ile Clone edin veya GitHub'ın Code menüsündeki adresle `git clone` kullanın. Klon klasöründe `git lfs pull` çalıştırın.
4. Unity Hub > Add > Add project from disk ile klonun kök klasörünü seçin. Kök klasörde `Assets`, `Packages`, `ProjectSettings` bulunmalı.
5. İlk açılışta paketlerin ve varlıkların içe aktarılmasını bekleyin. Unity eksik `Library` klasörünü kendisi oluşturur.
6. `Assets/Clawbles/Scenes/Clawbles.unity` sahnesini açıp Play'e basın. Host ile oturum açın ve vardiyayı başlatın.

## İçerik ve kod

- `Assets/Clawbles/Runtime`: oyun simülasyonu, ağ, kamera, fizik ve arayüz.
- `Assets/Clawbles/Editor`: varlık oluşturma, testler ve demo derleme araçları.
- `Assets/Clawbles/Art` ve `Resources`: modeller, malzemeler ve çalışma zamanı kaynakları.
- `Packages/manifest.json` ve `packages-lock.json`: paket bağımlılıkları ve sürümleri.
- `ProjectSettings`: ortak Unity ayarları.

Dört vardiya: pençe, mıknatıs, fiziksel çengel, fiziksel kepçe. 9–25 oda ve rastgele seçilen CCTV pilotu bulunur. Ağ bağlantısı yerel ağda Host/Join üzerinden çalışır; Steam eşleştirmesi bulunmaz.

## Birlikte çalışma

- Her iş için ayrı branch açın; örneğin `feature/room-art` veya `fix/magnet`.
- Çalışmadan önce son değişiklikleri alın. Değişiklikleri küçük commit'lerle gönderin ve pull request açın.
- Bir varlığı taşıdığınızda veya eklediğinizde yanındaki `.meta` dosyasını da aynı commit'e ekleyin. Taşıma/silme işlemlerini mümkünse Unity içinden yapın.
- Aynı sahne veya prefab üzerinde eşzamanlı değişiklik yapmadan önce haberleşin. Ortak sahne yerine ayrı prefablar üzerinde çalışmak çakışmayı azaltır.
- Unity sürümünü ve paket sürümlerini birlikte karar vermeden yükseltmeyin.
- `Library`, `Temp`, `Logs`, `UserSettings`, `Builds` ve IDE dosyaları Git'e dahil edilmez. Büyük ikili görsel/ses/model dosyaları Git LFS kullanır.
- Projeyi test etmek için Unity içinden Play kullanın. Derleme testleri Editör'ü otomatik kapatır; bunları günlük açık Editör oturumunda çalıştırmayın.

## Otomatik doğrulama ve Windows demo

Editör kapalıyken PowerShell'de, yerel yollarınızı kullanarak:

```powershell
$env:CLAWBLES_BUILD_PATH = 'Builds/Windows/CLAWBLES.exe'
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'PROJENIN_TAM_YOLU' -executeMethod Clawbles.Editor.ClawBuild.ValidateAndBuild -logFile 'DERLEME_LOGUNUN_TAM_YOLU'
```

`-quit` eklemeyin: test aracı Play Mode'a girer, testleri çalıştırır, çıkar, derlemeyi tamamlayıp Editör'ü kendisi kapatır. Başarılı log `CLAWBLES_BUILD_OK` içerir. EXE ve yanındaki veri klasörlerini birlikte paylaşın; bunları kaynak deposuna eklemeyin.

Bu depo için açık kaynak lisansı tanımlanmamıştır. Erişim vermek, projeyi yeniden yayımlama izni vermek anlamına gelmez.
