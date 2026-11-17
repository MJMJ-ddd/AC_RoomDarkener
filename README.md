# AC_RoomDarkener

アイコミ (Aicomi) の H シーン / 探索タッチ UI の左メニューに「明かり ON/OFF」ボタンを追加する **BepInEx 6 (IL2CPP)** プラグインです。

シーンを読み込み直さずに、その場で部屋を青みのある夜の照明へ切り替えます。

## 特徴

- 左メニュー最下部に電球ボタンを追加（既存ボタンを複製してスタイルを統一）
- ボタンを押すたびに「明かり ON ⇔ OFF」をシームレスに切り替え
- 消灯時は **BuildingLight / PointLight を全マップでオフ**
- **キャラクターの影の濃さを 0〜100%** で調整（0% = 影なし）
- **画面の露出 / ガンマを 0〜100%** で調整（焼き込みライトマップのマップにも有効）
- 夜のマップでのみボタンを表示（設定で変更可能）
- H 終了 / マップ変更 / シーン変更 / ゲーム終了時に必ず元の照明へ復元
- ConfigManager から全パラメータを即時変更

## 動作環境

| 項目 | 値 |
|---|---|
| ゲーム | アイコミ (Aicomi) |
| ゲーム側 Unity | 2022.3.55f1 (IL2CPP x64) |
| BepInEx | 6.0.0-be.752 で動作確認 |
| ビルド | .NET 6 (`net6.0`) |

## インストール

1. [BepInEx 6 (IL2CPP)](https://github.com/BepInEx/BepInEx) をゲームに導入する
2. `AC_RoomDarkener.dll` を以下へ配置する

```
Aicomi/BepInEx/plugins/AC_RoomDarkener/AC_RoomDarkener.dll
```

3. ゲームを起動する
4. `BepInEx/config/samur.aicomi.roomdarkener.cfg` が自動生成される

DLL を削除すればバニラの状態に戻ります。ゲームファイル・セーブデータは変更しません。

## 使い方

1. 夜のマップで H シーンに入る（または探索のタッチ UI を開く）
2. 左メニュー最下部（歯車の下）の電球ボタンを押す
3. もう一度押すと元の照明に戻る

ホットキー：`Ctrl + L`（`General/Hotkey` で変更、`HotkeyRequireCtrl=false` で単キー）

## 設定

`BepInEx/config/samur.aicomi.roomdarkener.cfg`、または ConfigManager から変更できます。

### General

| キー | 既定値 | 内容 |
|---|---|---|
| `Enabled` | true | プラグイン全体の有効/無効 |
| `OnlyAtNight` | true | 夜と判定したマップでのみボタンを表示 |
| `NightTimeZoneIds` | `4` | 夜とみなす時間帯 ID（カンマ区切り） |
| `ShowButton` | true | ボタンを追加する |
| `ShowWhenUnknown` | false | 時間帯不明でもボタンを表示（デバッグ用） |
| `Hotkey` | `L` | 切替キー |
| `HotkeyRequireCtrl` | true | Ctrl 同時押しを要求 |

`AC.User.TimeZones`: 0=早朝, 1=朝, 2=昼, 3=夕方, 4=夜, 5=帰宅

### Darkness

| キー | 既定値 | 内容 |
|---|---|---|
| `Brightness` | 0.12 | 消灯時のライト強度倍率（0=消灯, 1=変更なし） |
| `ColorTemperatureK` | 8000 | 月明かりの色温度 |
| `AmbientColor` | `#22385C` | 環境光の色 |
| `AmbientIntensity` | 0.6 | 環境光の強さ |
| `MinAmbientIntensity` | 0.15 | 環境光の下限 |
| `MinLuminance` | 0.22 | 人物が視認できる最低輝度 |
| `ReflectionIntensity` | 0.3 | ReflectionProbe 強度 |
| `KeepWarmAccentLights` | true | スタンドライト等の暖色光を弱く残す |
| `WarmLightKeep` | 0.35 | 暖色ライトを残す割合 |
| `WarmColorThreshold` | 0.08 | 暖色判定の R-B しきい値 |
| `LightColorMix` | 0.25 | ライト色を月明かり色へ寄せる割合 |
| `HandleEmission` | true | Emission マテリアルも暗くする |
| `EmissionScale` | 0.25 | Emission 倍率 |
| `MaxEmissionMaterials` | 4000 | Emission を処理する上限 |
| `MoonLightIntensity` | 0.45 | 追加する月明かりの強さ |

### Lights

| キー | 既定値 | 内容 |
|---|---|---|
| `DisableBuildingLights` | true | 名前に `BuildingLight` を含むライトをオフ |
| `DisablePointLights` | true | 名前に `PointLight` / `Point Light` を含むライトをオフ |
| `DisableNamedLightObjects` | true | 該当 GameObject 自体も非アクティブ化 |

### Screen

| キー | 既定値 | 内容 |
|---|---|---|
| `ScreenExposure` | 55 | 画面の露出を下げる強さ（0-100） |
| `ScreenGamma` | 0 | 画面のガンマ / コントラスト（0-100） |
| `CharacterShadow` | 100 | 影の濃さ（0=影なし, 100=元のまま） |
| `UsePostProcessing` | true | Beautify の brightness / contrast / tint を変更 |
| `UseScreenOverlay` | false | 3D 画面に暗幕を重ねる（保険） |
| `OverlayColor` | `#05080F` | 暗幕・夜色 |

### Advanced

| キー | 既定値 | 内容 |
|---|---|---|
| `ScanWholeScene` | false | シーン全体の Light / Renderer を対象にする |
| `ExtraLightRootPath` | (空) | 追加で走査するライトのルート（Hierarchy パス） |
| `VerboseLog` | false | ライト 1 本ごとの詳細ログ |
| `DumpButtonHierarchy` | true | ボタン探索失敗時に一覧をログ出力 |

### UI

| キー | 既定値 | 内容 |
|---|---|---|
| `TintIconByState` | true | 消灯中はアイコンを青系にする |
| `OffIconTint` | `#5A7BB0` | 消灯中のアイコン色 |
| `SourceButtonPath` | (空) | 複製元ボタンのフルパス |

## ソースからビルド

```powershell
git clone https://github.com/<your-account>/AC_RoomDarkener.git
cd AC_RoomDarkener
$env:DOTNET_CLI_UI_LANGUAGE="en"
dotnet build AC_RoomDarkener.csproj -c Release -v q
```

出力：`bin/Release/net6.0/AC_RoomDarkener.dll`

ゲームへ直接コピーする場合：

```powershell
dotnet build AC_RoomDarkener.csproj -c Release -p:GameDir="C:\path\to\Aicomi"
```

## トラブルシューティング

| 症状 | 原因 | 対処 |
|---|---|---|
| プラグイン一覧に出ない | DLL の配置場所が違う | `BepInEx/plugins/AC_RoomDarkener/` に置く |
| ボタンが出ない | 夜判定が false / UI 探索失敗 | `OnlyAtNight=false` で切り分け。ログのボタン一覧から `SourceButtonPath` を設定 |
| ボタンは出るが暗くならない | 対象ライトが 0 本 | `ScanWholeScene=true`、ログの `[Snapshot] Light=...` を確認 |
| 部屋が思ったより暗くならない | 焼き込みライトマップ | `ScreenExposure` を 80〜100 に上げる |
| 露出が効かない | Beautify が取得できない | `UseScreenOverlay=true` を試す（UI も暗くなる場合あり） |
| 影が消えない | 焼き込み影 | `CharacterShadow` はリアルタイム影のみ。露出で調整 |
| 切替時に一瞬カクつく | Emission マテリアル走査 | `HandleEmission=false` または `MaxEmissionMaterials` を下げる |
| 他 MOD と競合 | 同じ Light を変更 | 片方を無効化（本 MOD は終了時に元へ戻す） |

デバッグ時は `BepInEx/LogOutput.log` の `AC_RoomDarkener` 行を確認してください。

```
[Snapshot] 保存しました: Light=32, Emission=94, NamedObjects=8, Root=2
[Apply] Building/Point GameObject を 8 件非アクティブにしました。
[Apply] Beautify: brightness=0.42, contrast=1.00, exposure=55%
[Restore] 元のライト状態へ戻しました (button).
```

## 実装メモ

技術的な調査結果・Harmony パッチ・既知の罠は [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) を参照してください。

## ライセンス

ライセンスは設定していません。再配布・改変・再アップロードは作者の許可が必要です。
