# v1.0.0 - ディザリング for YMM4

YukkuriMovieMaker4 向けのディザリングエフェクトプラグインの初回リリースです。
Direct2D カスタムピクセルシェーダーが入力映像を指定した階調数へ量子化し、8×8 の Bayer 行列による順序ディザリングで少ない色数のまま濃淡を残します。
RGB・モノクロ・デュオトーンの 3 つの色モードに対応し、量子化した結果を合成モードで元映像へ重ね、適用量で割合を調整します。
8 言語リソース構成の UI を備えます。

---

## 新機能

### 1. ピクセルシェーダー

`Dithering.hlsl` の `main` は、入力映像の各画素を Bayer 行列のしきい値付きで量子化します。追加テクスチャは使用しません。`source.a <= 0` のときはソースをそのまま返します。

#### Bayer 行列のしきい値

`BayerThreshold` は、シーン座標を `scale` で割ったマス目の座標から、8×8 の順序ディザ行列の値を求めます。`x ^ y` と `y` のビットを交互に並べて 0〜63 の値を作り、`(値 + 0.5) / 64` をしきい値とします。しきい値は `lerp(0.5, BayerThreshold(grid), strength)` で 0.5 と混ぜるため、`strength` が 0 のときは一律 0.5 のポスタリゼーション、1 のときは完全なディザリングになります。

| 値 | 説明 |
|---|---|
| `grid` | `floor(scenePosition.xy / max(scale, 1))` のマス目座標 |
| `strength` | Bayer しきい値と 0.5 を混ぜる割合 |
| `steps` | `max(levels − 1, 1)` の量子化段数 |

#### 量子化

`Quantize` は、色に `steps` を掛けた値の小数部としきい値を `step` で比較し、切り上げるか切り捨てるかを決めます。入力色はプリマルチプライドの `rgb` を `rgb / max(alpha, 1e-5)` でストレート化してから量子化します。

#### 色モード

| `mode` | モード | 処理 |
|---|---|---|
| 0 | RGB | 各チャンネルを個別に量子化します |
| 1 | モノクロ | Rec.601 係数 `(0.299, 0.587, 0.114)` の輝度を量子化します |
| 2 | デュオトーン | 量子化した輝度を `lerp(darkColor, lightColor, 量子化値)` で 2 色の間へ割り当てます |

最終出力は入力アルファを保持し、`float4(result × alpha, alpha)` をプリマルチプライドで返します。

---

### 2. カスタムシェーダーエフェクト

`DitheringCustomEffect` は `[CustomEffect(1)]` の 1 入力エフェクトです。公開プロパティは `SetValue` を介して定数バッファーへ転送します。各プロパティは代入時にシェーダーが前提とする範囲へ制限します。

| プロパティ | 型 | 範囲 |
|---|---|---|
| `Levels` | `float` | 2〜256（四捨五入） |
| `Scale` | `float` | 1〜4096 |
| `Strength` | `float` | 0〜1 |
| `Mode` | `int` | 0〜2 |
| `DarkR` / `DarkG` / `DarkB` | `float` | 0〜1 |
| `LightR` / `LightG` / `LightB` | `float` | 0〜1 |

`ConstantBuffer` のレイアウトは以下のとおりです。合計 48 バイトで 16 バイトの倍数に揃います。

| フィールド | 型 | 説明 |
|---|---|---|
| `Levels` | `float` | 階調数 |
| `Scale` | `float` | ドットサイズ |
| `Strength` | `float` | ディザ強度 |
| `Mode` | `int` | 色モード |
| `DarkColor` | `float4` | 暗色（RGB を使用） |
| `LightColor` | `float4` | 明色（RGB を使用） |

自ピクセルだけを参照するため入力矩形の拡張は不要で、矩形の対応付けは既定のまま 1 対 1 です。

シェーダーリソース: `pack://application:,,,/Dithering;component/Shaders/Dithering.cso`（ps_5_0、`ShaderResourceUri.Get` が生成）

---

### 3. エフェクト定義

`DitheringEffect` は YMM4 の映像エフェクトとして宣言されます。

`[VideoEffect]` 属性は以下のパラメーターで宣言されます。

- 表示名：`Texts.Dithering`（ローカライズキー、日本語では「ディザリング」）
- カテゴリー：`VideoEffectCategories.Filtering`
- 検索タグ：`TagDithering`・`TagRetro`・`TagPixelArt`
- `IsAviUtlSupported = false` により AviUtl 向け EXO 出力は非対応
- `ResourceType = typeof(Texts)` でローカライズリソースを指定

`Label` プロパティは `Texts.Dithering` を返します。

公開プロパティは以下のとおりです。

| プロパティ | 型 | デフォルト | 内部範囲 | アニメーション |
|---|---|---|---|---|
| `Mode` | `DitheringMode` | `Rgb` | — | なし |
| `Levels` | `Animation` | 4 | 2〜256 | あり |
| `Scale` | `Animation` | 1 | 1〜512 | あり |
| `Strength` | `Animation` | 100 | 0〜100 | あり |
| `DarkColor` | `Color` | `#FF0F380F` | — | なし |
| `LightColor` | `Color` | `#FF9BBC0F` | — | なし |
| `BlendMode` | `Blend` | `Normal` | — | なし |
| `Amount` | `Animation` | 100 | 0〜100 | あり |

`DitheringMode` は `Rgb`・`Grayscale`・`Duotone` の 3 値です。`DarkColor` と `LightColor` は `DuotoneColorVisibleAttribute` により、モードが `Duotone` のときにだけ UI に表示されます。

`GetAnimatables` は `Levels`・`Scale`・`Strength`・`Amount` を返します。

`CreateExoVideoFilters` は空のシーケンスを返します（EXO 非対応）。`CreateVideoEffect` は映像処理用のインスタンスを生成します。

---

### 4. エフェクトチェーンとフレームごとの更新

`DitheringEffectProcessor` はカスタムシェーダーの出力を合成モードと適用量で元映像へ重ねます。`CreateEffect` で次の 4 つを構築します。

- `DitheringCustomEffect`：入力 0 を量子化する本体
- `Composite`（2 入力）：入力 0 に元映像、入力 1 に量子化結果
- `Blend`（2 入力）：入力 0 に元映像、入力 1 に量子化結果
- `CrossFade`：入力 1 に元映像、入力 0 に合成結果

各フレームで YMM4 の `EffectDescription` からフレーム位置、アイテム長、FPS を取得し、アニメーション値を評価します。前フレームと値が異なる項目だけを反映します。

| パラメータ | 反映 |
|---|---|
| `Levels` / `Scale` | 数値をそのままカスタムシェーダーへ |
| `Strength` | `value / 100` |
| `Mode` | 列挙値を `int` へ |
| `DarkColor` / `LightColor` | `R/G/B` を 0〜1 の float へ |
| `BlendMode` | 合成系のときは `Composite`、それ以外は `Blend` の対応するモードへ設定し、その出力を `CrossFade` の入力 0 へ接続 |
| `Amount` | `value / 100` を `CrossFade` の重みへ |

`BlendMode` は YMM4 の `Blend` 列挙で、`IsCompositionEffect` により `Composite` と `Blend` のどちらを使うかを切り替えます。`CrossFade` の重みが適用量で、量子化結果を重ねた映像と元映像の割合を決めます。入力は各エフェクトへ `SetInput` で接続し、エフェクトチェーンのクリア時はすべての入力を `null` に戻します。

---

### 5. ローカライズ

`Texts` クラスは `[AutoGenLocalizer]` 属性を持つ `partial` クラスとして宣言されます。
`YukkuriMovieMaker.Generator` のソースジェネレーターが `Texts.csv` を処理し、各ロケールのリソースファイルを自動生成します。

対応リソース：日本語（`ja-jp`）・英語（`en-us`）・中国語簡体字（`zh-cn`）・中国語繁体字（`zh-tw`）・韓国語（`ko-kr`）・スペイン語（`es-es`）・アラビア語（`ar-sa`）・インドネシア語（`id-id`）

ローカライズキーの一覧は以下のとおりです。

| キー | ja-jp |
|---|---|
| `Dithering` | ディザリング |
| `TagDithering` | ディザ |
| `TagRetro` | レトロ |
| `TagPixelArt` | ドット絵 |
| `Mode` | モード |
| `ModeRgb` | RGB |
| `ModeGrayscale` | モノクロ |
| `ModeDuotone` | デュオトーン |
| `Levels` | 階調数 |
| `Scale` | ドットサイズ |
| `Strength` | ディザ強度 |
| `DarkColor` | 暗色 |
| `LightColor` | 明色 |
| `BlendMode` | 合成モード |
| `Amount` | 適用量 |
