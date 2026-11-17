# 開発メモ

## 構成

| ファイル | 役割 |
|---|---|
| `Plugin.cs` | 入口。Config / Harmony / 常駐コンポーネント |
| `RoomDarkenerBehaviour.cs` | 毎フレームの軽いポーリング |
| `Config/DarkenerConfig.cs` | BepInEx Config 定義 |
| `Core/DarkenerSettings.cs` | 消灯 1 回分の値 / 色温度変換 |
| `Core/NightDetector.cs` | 夜判定 |
| `Core/LightStateSnapshot.cs` | Light / RenderSettings / Emission / Beautify / 影の保存・適用・復元 |
| `Core/RoomDarkness.cs` | 状態遷移（H 開始/終了・マップ変更・ON/OFF） |
| `UI/DarkenButton.cs` | 既存ボタンの複製・配置・アイコン |
| `Patches/HarmonyPatches.cs` | Harmony パッチ |

## Harmony パッチ（3 か所のみ）

1. `AC.Scene.Touch.TouchController.Setup` — Postfix
2. `H.HScene.Initialize(InputParameter, Transform)` — Prefix
3. `Manager.Environment.ChangeTimeZone(int)` — Postfix

マップ読み込み完了・H 終了・シーン変更は 0.2 秒間隔のポーリングで拾う。
`UniTask` を await しない、構造体返却メソッドをフックしない、という IL2CPP の罠を回避するため。

## 調査結果

| 項目 | 確認結果 |
|---|---|
| 夜判定 | `Manager.Environment.Entity.TimeZone` / `AC.User.SaveData.Cycle.TimeZone` / `FreeHScene._timeZones`。`AC.User.TimeZones.Night = 4` |
| ライト管理 | `AC.EnvironmentComponent` (`Root` / `TimeZone` / `LightComponent`)、`H.TimeZoneOfLightH` |
| H シーンの照明 | `H.HSprite.LightPanel` (`H.LightPanel._light` / `_subLights`) |
| 左メニュー | `H.HSprite`（Cloth / Accessory / Coorde / FreeMove / UseItem / LightPanel） |
| 探索タッチ UI | `AC.Scene.Touch.TouchController._buttonLight` / `_lightPanel` |
| H 開始 | `H.HScene.Initialize` / `H.HScene.IsActive()` |
| 露出・ガンマ | `Beautify.Universal.BeautifySettings.settings`（`Beautify : VolumeComponent`） |
| 影 | `Light.shadowStrength` |

### dnSpy / ILSpy の検索キーワード

読み込む DLL: `BepInEx/interop-Aicomi/interop/Assembly-CSharp.dll`

- 夜判定: `TimeZones`, `Cycle.TimeZone`, `EnvironmentComponent.TimeZone`, `ChangeTimeZone`
- ライト: `EnvironmentLight`, `EnvironmentComponent.Root`, `TimeZoneOfLight`, `LightPanel`
- 左メニュー: `HSprite`, `HButtons`, `ClothPanel`, `_buttonLight`
- H: `HScene.Initialize`, `HScene.IsActive`
- 露出: `BeautifySettings`, `Beautify` (`Unity.RenderPipelines.Universal.Runtime.dll`)

## 既知の罠

### 1. MOD 独自型を `GetComponent<T>()` に渡さない

`light.GetComponent<MyMarker>()` のように Il2CppInterop が注入した型をジェネリック
`GetComponent<T>` に渡すと、次の例外で落ちる。

```
System.TypeInitializationException:
  The type initializer for 'MethodInfoStoreGeneric_GetComponent_Public_T_0`1' threw an exception.
```

対策：MOD 独自型を目印にしない。必要な場合は非ジェネリック `GetComponent(Type)` + `TryCast<T>()`。
Unity 標準型（`Light` / `Button` / `Image`）は問題ない。

### 2. 焼き込みライトマップは Light を消しても暗くならない

- ログの `[Snapshot] Light` に出るのはイベント用の Point Light だけ、ということがある
- 部屋の明るさがライトマップに焼かれている場合、`Light.intensity = 0` では変化しない
- 対策：Beautify の `brightness` / `contrast` / `tintColor` を変更する

### 3. 左メニューのアイコンは Button とは限らない

H シーンの左メニューには `Toggle` のアイコンもある。
本 MOD は `SubCategory/btnConfig` を複製元のフォールバックにしている。

### 4. 既存ボタンの複製

- `Button.onClick.RemoveAllListeners()` で元の動作を外す
- `LayoutGroup` がある場合は `SetAsLastSibling()`、手動配置なら座標で下へずらす
- `CanvasGroup.alpha` / `Animator` を強制表示する

## 復元の設計

`LightStateSnapshot` が以下を保存し、`Restore()` で戻す。

- 各 Light の intensity / color / shadowStrength / enabled
- RenderSettings の ambient / reflection
- Emission マテリアルの色とキーワード
- Beautify の brightness / contrast / tintColor（overrideState 含む）
- Building/PointLight の GameObject の activeSelf

シーンが変わっている場合は RenderSettings の復元をスキップし、
新しいシーンの値を上書きしない。
