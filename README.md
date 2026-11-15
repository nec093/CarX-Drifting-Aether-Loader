# Aether Loader

CarX Drift Racing Online（moddable ブランチ、Unity 2023.2.22f1）向けの、Aether シリーズの Mod（Aether Mod）を管理するローダーです。BepInEx 5 のプラグインとして動きます。

- `AetherLoader\mods\` のフォルダーごとに 1 つの Mod を見つけ、依存関係の順に読み込みます。
- ゲーム内のウィンドウ（**F9**）に一覧を出します。Mod ごとに、状態と読み込まれなかった理由が分かります。Mod の有効と無効も、ここで切り替えます（次の起動から反映）。
- Mod は SDK（`Aether.API.dll`）を参照して作ります。SDK には、Mod 自身のフォルダーから AssetBundle を読む機能が入っています（独自マップ、独自パーツなどのため）。
- KSL にも kino にも依存しません。従来の BepInEx の Mod とも、そのまま一緒に動きます。

## 導入（開発環境）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy.ps1 -Build   # ゲームを閉じて
```

| 置き場所（ゲームのフォルダーから） | 中身 |
|---|---|
| `BepInEx\plugins\AetherLoader\` | `AetherLoader.dll`（プラグイン）、`Aether.API.dll`（SDK） |
| `AetherLoader\mods\<フォルダー>\` | Mod（`mod.json` と DLL、アセット） |
| `AetherLoader\config\loader.json` | ウィンドウの設定（`hotkey`、`uiScale`、`window` の位置と大きさ、無効にした Mod の `disabled`） |
| `AetherLoader\config\<Mod の ID>\` | 各 Mod の設定 |
| `AetherLoader\logs\status.json` | 今回の起動で何が読み込まれ、何が読み込まれなかったか（理由つき） |

ログは BepInEx の `BepInEx\LogOutput.log` に出ます。ソース名は、ローダーが `Aether Loader`、各 Mod はその ID です。

BepInEx 5（Windows x64、5.4.23.6 で確認）が必要です。KSL 1.0.14 を有効にしたままだと、KSL が BepInEx を消します。

## 管理画面（F9）

VS Code の Dark+ の配色に合わせたウィンドウです。

| 場所 | 内容 |
|---|---|
| 左 | Mod の一覧。チェックボックス（有効／無効）、状態、名前、版。上に絞り込み欄と状態のタブ（ALL / OK / OFF / PROBLEMS） |
| 右 | 選んだ Mod。状態、版、作者、フォルダー、設定フォルダー、読み込まれなかった理由、説明、依存先（REQUIRES）、それを必要とする Mod（REQUIRED BY）。ボタンは Disable/Enable、Open Folder、Open Config |
| 下 | ステータスバー。状態ごとの件数、保留中の変更と Undo、Mods Folder、Close |

- **キー操作:**
  - ↑↓：選択を移す
  - Space：選んだ Mod の有効と無効を切り替える
  - Ctrl+F：絞り込み欄へ移る
  - Esc：絞り込み欄から抜ける。欄の外では閉じる
- **反映のタイミング:** 有効と無効の切り替えは、次の起動から反映します。保留中の Mod には黄色の ● が付きます。
- **ウィンドウ:** タイトルバーで動かし、右下の角で大きさを変えます。位置と大きさは `loader.json` に 1440p 換算で残ります。
- **大きさ:** 画面の高さに合わせます（1440p で 1 倍）。`loader.json` の `uiScale` で固定もできます。文字は最終的な大きさで描くので、拡大してもぼやけません。
- **アクセシビリティ:**
  - 文字と背景のコントラスト比は 4.5:1 以上です（WCAG AA）。
  - 状態は色だけでなく、`ok` / `off` / `skip` / `fail` の文字でも示します。
  - キーボードで操作できます。

## Mod の作り方

Mod は `AetherLoader\mods\` の下のフォルダー 1 つです。必ず `mod.json` を置きます。

```json
{
  "id": "nec093.aether.map.tsukuba",
  "name": "Tsukuba",
  "version": "1.0.0",
  "api": "1.0",
  "author": "nec093",
  "description": "...",
  "assembly": "TsukubaMap.dll",
  "entry": "Tsukuba.TsukubaMod",
  "dependencies": [ { "id": "nec093.aether.maps", "version": ">=1.0.0", "optional": false } ]
}
```

| 項目 | 必須 | 意味 |
|---|---|---|
| `id` | ○ | 一意の ID。小文字の英数字を `.` `_` `-` でつなぐ（例 `作者.aether.種類.名前`） |
| `name`、`version` | ○ | 表示名と版（`major.minor.patch`） |
| `api` | ○ | 作ったときの Aether.API の版。メジャーが同じで、マイナーがローダー以下なら読み込む |
| `assembly` | | Mod のコード（フォルダー内の `.dll`）。無ければアセットだけの Mod（ほかの Mod が `ModInfo.Directory` から読む） |
| `entry` | | `IAetherMod` を実装したクラスの完全名。省くと、アセンブリ内に 1 つだけあるものを使う |
| `dependencies` | | 必要な Mod。`version` は条件を空白で区切って並べたもの（`>=1.2.0 <2.0.0` など）。素の `1.2` は「1.2 以上で同じメジャー」。`optional: true` は読み込みの順番だけを決める |

ローダーが知らない項目は無視します。ほかの Mod が読むための項目を、自由に足せます（`ModInfo.ManifestPath`）。

コードは `Aether.API.dll` を参照します（`Private=false`。Mod のフォルダーには入れない）。

```csharp
public sealed class TsukubaMod : Aether.IAetherMod {
	public void OnLoad(Aether.ModContext context) {
		context.Log.Info("loaded from " + context.Directory);
		var prefab = context.Assets.LoadAsset<UnityEngine.GameObject>("bundles/tsukuba.bundle", "Track");
		context.Root.AddComponent<TrackSpawner>(); // Update やコルーチンはここに付けたコンポーネントで
	}
	public void OnUnload() { }
}
```

- **`OnLoad` と `OnUnload` の呼ばれ方:** `OnLoad` は Unity のメインスレッドで、依存先の後に呼ばれます。`OnUnload` はゲームの終了時に、読み込みと逆の順で呼ばれます。
- **`OnLoad` が例外を投げたとき:** その Mod は `Failed` になり、読み込んだアセットは解放されます。それに依存する Mod は読み込まれません。ほかの Mod はそのまま読み込まれます。
- **`context.Assets` の使い方:** Mod のフォルダーにある AssetBundle を、相対パスで読みます。フォルダーの外を指すパスは受け付けません。バンドルは、ゲームと同じ Unity（2023.2.22f1）で StandaloneWindows64 向けにビルドします。
- **見本:** `samples\HelloMod`（最小の Mod）と `samples\DependentMod`（ほかの Mod の型を使う Mod）。

## Mod が読み込まれない理由（状態）

| 状態 | 意味 |
|---|---|
| `Loaded` | 読み込まれた |
| `Disabled` | ウィンドウで無効にしている |
| `Skipped` | 読み込まなかった（`mod.json` の誤り、ID の重複、API の版、依存先が無い・版が合わない・無効・読み込まれていない、依存の循環） |
| `Failed` | アセンブリの読み込みか `OnLoad` が失敗した |

ID が重複したときは、フォルダー名の順で先のものを使います。

## ビルドとテスト

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1       # SDK、ローダー、見本
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\test-game.ps1   # ゲーム内テスト（ゲームを閉じて）
```

- **`test-game.ps1` の流れ:**
  1. 見本と `tests\fixtures\`（失敗するケースの `mod.json`）を、`mods\aether-test-*` として入れる。
  2. ゲームを起動する。
  3. `status.json` とログを `tests\expected.json` と比べる。
  4. ゲームを閉じ、テスト用の Mod を外す。
- **`-Keep` を付けたとき:** ゲームとテスト用の Mod をそのまま残します（F9 でウィンドウを見るため）。後で `deploy.ps1 -RemoveTests` で外します。
- **`-OpenWindow` を付けたとき:** 起動時に管理画面を開きます（`config\dev-open-window` を置く）。`scripts\screenshot.ps1` で、ゲームのウィンドウの範囲を PNG に保存できます（`build\screenshots\`）。キーもクリックも送りません。

ゲームの DLL（`Drift Racing Online_Data\Managed`）と `BepInEx\core\BepInEx.dll` は、その場所のまま参照します。リポジトリには含めません。ゲームが別の場所にあるときは `-GameDir` を付けます。

## 非公式であること

Aether Loader は非公式の Mod です。CarX Technologies、Unity Technologies が作成・支援・推奨しているものではありません。
