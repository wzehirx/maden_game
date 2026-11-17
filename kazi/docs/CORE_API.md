# CORE_API — Kazı Kazan çekirdeği (namespace `Kazi.Core`) için Godot katmanı rehberi

Kaynak: `kazi/Scripts/Core/*.cs` (Godot/Unity bağımsız, LangVersion 9, .NET 8 test). Tüm sayılar `kazi/data/*.json` içindedir; Godot katmanı yalnızca bu API'yi çağırır, oyun kuralı yazmaz. Tüm sayılar `long` (bakiye ASLA negatif olmaz, taşmada doygunlaşır).

## 1. Başlatma
```csharp
var cfg = GameConfig.LoadDirectory(dataDir);            // cards/upgrades/prestige/settings.json (veya GameConfig.FromJson(4 string))
GameState st = SaveSystem.LoadFromFile(savePath, cfg);  // yoksa/bozuksa yeni oyun, ASLA istisna atmaz
var game = new GameSession(cfg, st);
game.OnEvent += e => { /* konuş/ses/titreşim */ };
var off = game.ApplyOfflineSince(unixNow);              // açılışta bir kez; off.Speech tek cümle (OfflineReport olayı da yayılır)
```
Ana döngü: her karede `game.Tick(deltaSeconds)` (otomasyon, yardım parası, "alabilirsin" bildirimi, otomasyon özeti, kayıt sayacı için). Kare hızı boşta düşük olabilir; büyük dt güvenlidir.

## 2. Kayıt (`SaveSystem`)
- `SaveSystem.SaveToFile(state, path)` (tmp dosya + taşıma), `SaveSystem.Serialize(state)` / `TryDeserialize(json, cfg, out state)` / `LoadOrNew(json,cfg)` / `NewGame(cfg)`.
- Sürümlü (`GameState.Version`, `CurrentVersion = 1`); bozuk/kesik/boş JSON yeni oyuna düşer, bozuk kartlar atılır, değerler `Sanitize` ile sınırlanır.
- Kaydedilmeden önce `state.LastSaveUnix = unixNow` yazın (çevrimdışı kazanç buradan hesaplanır). Öneri: her `cfg.Settings.AutosaveSeconds` (30 sn) ve uygulama arka plana geçince/kapanırken kaydet.
- Ayarlar da `GameState` içindedir ve kayda girer: `Detail`, `SpeechRate`, `Mode`, `NotifyAffordable`, `AutoBuyOn`, `AutoDiscardTrap`.

## 3. Ayarlar (GameSession üzerinden)
| Alan | Tür | Açıklama |
|---|---|---|
| `game.Detail` | `SpeechDetail { Full, Short, Silent }` | Varsayılan **Short**. Full: her hücre okunur. Short: yalnızca sonuç ve bilgi. Silent: hiç konuşma metni yok (olaylar yine yayılır, `Speech==""`). Menü/dükkân okumaları (`SpeechLevel.Menu`) sessizde de gelir. |
| `game.SpeechRate` | `double` | KONUŞMA hızı (oyun hızı değil). 0.5–3.0, 0.25 adım, varsayılan **1.5**; atama otomatik yuvarlanır/kısıtlanır (`GameState.NormalizeRate`; sabitler `MinRate/MaxRate/RateStep/DefaultRate`). TTS motoruna bu değeri uygulayın. |
| `game.Mode` | `ScratchMode { Sequential, Drag }` | Kazıma modu; `GestureRecognizer.DragMode = (Mode==Drag)` ile eşleyin. |
| `game.State.NotifyAffordable` | bool | "Alabilirsin: X" bildirimini aç/kapat. |
| `game.State.AutoBuyOn`, `AutoDiscardTrap` | bool | Dükkândan da değişir (Toggle öğeleri). |
Oyun hızı (x1/x2/x3) ayarı YOKTUR; ekonomi gerçek zamanlıdır.

## 4. Olaylar (`GameEvent`, `session.OnEvent`)
Alanlar: `Kind` (EventKind), `Speech` (okunacak Türkçe cümle; ayrıntı ayarına göre boş olabilir), `Amount` (para/ AB / adet), `Symbol` (sembol/kart/yükseltme/düğüm kimliği; ses seçimi), `CellIndex` (-1 olabilir), `Rarity` (`None, Small, Medium, Big, Jackpot` earcon seçimi), **`EstimatedSeconds`** (= karakter / (13 × SpeechRate); konuşma kuyruğu/susturma kararı için; Speech boşsa 0).

| EventKind | Ne zaman | Notlar |
|---|---|---|
| CellOpened | hücre açıldı | Speech yalnızca Full'de dolu; Symbol = sembol kimliği ("carpan" çarpan hücresi), CellIndex |
| CellPeek | sürüklemede göz atma eşiği | Speech "Dikkat, tuzak gibi." ya da boş; Symbol "tuzak" |
| CardBought | kart alındı/sıradaki karta geçildi | Amount fiyat, Symbol tür |
| CardResult | kart bitti (elle) | Speech tek cümle ("Kazandın, 250 lira" / "Bu sefer çıkmadı"), Amount net değişim, Rarity |
| TrapHit | tuzak cezası uygulandı | Amount ceza |
| CardDiscarded | çöpe atıldı | Amount iade |
| AutoResult | otomatik kartın sonucu | Speech HER ZAMAN boş; yalnız ses (earcon) için, Rarity/Amount |
| AutoReport | ~60 sn'de bir otomasyon özeti | "12 kart kazıldı, 3 kazandı." Amount net, CellIndex=kart sayısı |
| NotEnoughMoney / NoCard / BagFull / Error | uyarılar | tek cümle |
| UpgradeBought / GadgetBought / AutomationBought | satın alma | Symbol kimlik (her gadget için ayrı ses) |
| UpgradeAffordable | artık alınabilir (ayardan kapanır) | Symbol |
| HelpMoney | yardım parası verildi | Amount |
| CatalogUnlocked | yeni katalog | Amount = katalog no |
| PrestigeOffer / PrestigeDone / NodeBought | prestij akışı | |
| OfflineReport | açılışta tek cümle | |
| ShopOpened / ShopClosed / ShopItemChanged / ShopCategoryChanged | dükkân gezinme, Speech hep dolu | |
| Status / Info | durum okuma, bilgi | |
Konuşma önerisi: Speech boş değilse okut; `EstimatedSeconds` ile yeni olay gelince kuyruk uzunluğunu sınırla (ör. otomasyon özeti sonuç cümlesini kesmesin, süreyi aşan bayat Info'ları at).

## 5. Oyun metotları (`GameSession`)
Kazıma/kart:
- `bool BuyCard(string typeId)`; `void ScratchTap()` (SIRALI mod: dokunuş başına `Stats.ScratchPower` hücre); `void ScratchDrag(int cell, double amount)` (SÜRÜKLEME: amount 0..1 hücre kazınma artışı; eşikler settings.json `scratchThresholds`: peek 0.25, open 0.55; 3x3'e ekranı siz bölersiniz, hücre = satır*cols+sütun); `bool DiscardCard()` (yalnızca tuzaklı tür; ödeme/ceza yok, bilet bedelinin iade yüzdesi geri gelir); `bool NextCard()` (çantadaki ya da son türden yenisi).
- `Card State.Current` (`Type, Cells, Open[], Scratch[], Done, AllOpen, DescribeCell(i)`), `State.Bag`.
- `string LensHint()` (Mercek gadget'ı: tuzak ihtimali ipucu), `string ReadStatus()` (2 parmak dokunuş: bakiye, katalog, otomasyon özeti; ayrıca `Status` olayı).
Dükkân (kontroller: yukarı kaydır = `ToggleShop()`, sola/sağa = `ShopPrev()/ShopNext()`, 2 parmak sola/sağa = `ShopPrevCategory()/ShopNextCategory()`, çift dokunuş = `bool ShopBuy()`):
- `ShopOpen`, `ShopCategory`, `CategoryName` (`GameSession.Categories`: Kartlar, Kazıma, Otomasyon, Gadget, Prestij), `string ReadShopItem()`, `List<ShopItem> BuildItems(int cat)` (`ShopItem{Kind,Id,Name,Desc,Price,UsesAB}`; ekranda büyük yazı için).
- Prestij sıfırlama öğesi iki adım onaylıdır (ilk `ShopBuy` "Emin misin?", ikincisi uygular).
Doğrudan: `BuyUpgrade(id)`, `BuyNode(id)` (AB harcar), `SetAutoType(typeId)`, `CycleAutoType()`.
Prestij: `PrestigeOffered`, `long PrestigeGain()`, `bool AcceptPrestige()`, `void DeclinePrestige()`.
Otomasyon: `Tick` yürütür; `FlushAutoReport()` özeti hemen yayar (arka plana geçerken).
Çevrimdışı: `ApplyOffline(sec)` / `ApplyOfflineSince(unix)`; yalnızca otomasyon, tavan `Stats.OfflineCap` (≤ 7200 sn + yükseltme/düğümler), `OfflineResult{SecondsCounted, Money, Cards, Speech}`.
Salt okunur: `State` (Money, AB, UnlockedCatalog, PrestigeCount, TotalEarned, ...), `Stats` (ScratchPower, Luck, PayoutMult, Discount, BagCap, AutoOwned, AutoCpm, Batch, Lens, ...), `Config` (CardTypes, Catalogs, Upgrades, Nodes, Settings), `TicketPrice(type)`, `CanUseType`, `GateLevelsLeft(type)`.
Yardım parası otomatik: bakiye < en ucuz kart fiyatı ve kart/kuyruk yoksa `Tick` içinde `HelpMoney` olayı.

## 6. Hareket tanıma (`GestureRecognizer`)
```csharp
var g = new GestureRecognizer();
g.Configure(screenWidthPx, screenHeightPx, dpi);   // eşikler dpi'ya ölçeklenir
g.DragMode = game.Mode == ScratchMode.Drag;
g.OnGesture += (Gesture x) => { ... };             // Tap, DoubleTap, SwipeUp/Down/Left/Right, TwoFingerTap, ThreeFingerTap, TwoFingerSwipeLeft/Right
g.OnDragMove += (DragMove m) => { ... };           // yalnız DragMode: X,Y,PrevX,PrevY,Time,Distance (px)
g.Feed(new RawTouch(id, TouchPhase.Down|Move|Up|Cancel, xPx, yPx, timeSeconds));  // Godot InputEventScreenTouch/Drag'den
```
- Tek dokunuş (`Tap`) parmak kalkınca ANINDA yayılır (çift dokunuş beklemesi yok); ikinci dokunuş `Tap` sonra `DoubleTap` yayar (sıra: Tap, Tap, DoubleTap; aralık ≤ 0.35 sn). Kart ekranında `Tap`'i kazıma için kullanın; dükkânda `Tap`'i yok sayın (veya yalnız öğeyi yeniden okutun) ve satın almayı `DoubleTap`'e bağlayın.
- Uzun basış yok sayılır; `Reset()` sahne geçişlerinde. Önerilen eşleme: Tap=`ScratchTap` (dükkân kapalı), SwipeDown=`DiscardCard` (tuzaklı ve yarım açıksa) yoksa `NextCard`, SwipeUp=`ToggleShop`, Swipe sol/sağ (dükkân açık)=`ShopPrev/Next`, TwoFingerSwipe=kategori, DoubleTap (dükkân)=`ShopBuy`, TwoFingerTap=`ReadStatus`, ThreeFingerTap=ayarlar menüsü (Godot katmanı).
- SÜRÜKLEME modunda: `OnDragMove` her adımında parmağın bulunduğu hücreye `ScratchDrag(cell, amount)` çağırın (amount ≈ yol uzunluğu / hücre çapı gibi 0..1); hız→ton, sütun→pan, sınır geçişi ipucu Godot'ta. Yavaş sürükleme kaydırma sayılmaz, hızlı fırlatma (FlickSpeedPx) sayılır.

## 7. Yardımcılar
- `NumberFormat.Short(long)` ("1,2 bin"), `NumberFormat.Lira(long)` ("1,2 bin lira"), `Duration`; Türkçe okunur, katrilyon/kentilyona kadar.
- `Rng` (xorshift, durum kayıtta), `Odds.Rtp(type, luck, settings)`, `Economy.ExpectedNet/BestType` (Sim ve gösterge için).
- İçerik: 5 katalog, 12+ kart türü (ThreeOfAKind, NumberHunt, Multiplier, Trap, Joker, Sequence, Jackpot, Son Şans kapıları), ~24 prestij düğümü; hepsi JSON.
