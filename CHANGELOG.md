# Changelog

## 0.1.0 — 2026-11-17

### Added

- 左メニュー最下部に「明かり ON/OFF」ボタンを追加
  - H シーンの `H.HSprite` / 探索の `TouchController` の両方に対応
  - 既存ボタンを複製してスタイルを統一
  - 電球スプライトを既存メニューから流用
- シーンを再読み込みせずに照明を消灯 / 復元
  - Light の intensity / color / shadowStrength
  - RenderSettings の ambient / reflection
  - Emission マテリアル
  - 追加の月明かり Directional Light
- BuildingLight / PointLight の一括オフ
  - 名前を正規化して全マップから検索
  - GameObject 自体の非アクティブ化にも対応
- キャラクターの影の濃さを 0〜100% で調整
- 画面の露出 / ガンマを 0〜100% で調整
  - ゲーム標準の Beautify (URP VolumeComponent) を変更
  - 保険として 3D 画面用の暗幕も選択可能
- 夜判定（環境 / FreeH / セーブデータの時間帯）
- H 開始 / 終了 / マップ変更 / シーン変更 / 終了時に強制復元
- ConfigManager から全パラメータを即時変更

### Known issues

- 焼き込みライトマップのマップでは、Light を消しても部屋は暗くならない。
  画面の露出（ScreenExposure）で調整する。
- 影の調整はリアルタイムシャドウのみ。焼き込み影には効かない。
- 左メニューのボタン名はプレハブ依存。自動探索に失敗する場合は
  `SourceButtonPath` を設定する。
