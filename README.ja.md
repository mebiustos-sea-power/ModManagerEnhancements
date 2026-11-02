# Mod Manager Enhancements

[English](README.md)

Sea Powerの既存MODマネージャーに、ファイルの上書き関係を確認できるマークと詳細表示を追加します。

## 機能と操作

- 有効なローカル・Workshop MODとUser Dataの間で、内容が異なる重複ファイルを表示します。
- `↑` は、そのMODが最優先の提供元になる重複ファイルの件数です。`↓` は、最優先ファイルと内容が異なる件数です。同一内容の重複だけではマークを付けません。
- 左のMOD一覧のマークをクリックすると、そのMODの上書き詳細に絞り込みます。絞り込み中だけ対象MOD名と「全件表示に戻す」が表示されます。
- ファイルの提供元を優先順に表示します。カーソルを合わせると実際のパスを確認できます。詳細リストは選択操作を持たない表示専用の一覧です。
- チェック状態、ドラッグ、既存の依存順ソート、プロファイル適用に追従します。ファイルの編集・追加・削除後は「再スキャン」で読み直せます。
- ゲーム言語が日本語なら日本語、それ以外は英語で表示します。

## 導入

Sea Power 0.8.7と、動作する[Anchor Chain](https://steamcommunity.com/sharedfiles/filedetails/?id=3380210757)環境が必要です。

1. [Releases](https://github.com/mebiustos-sea-power/ModManagerEnhancements/releases)から `ModManagerEnhancements-v0.1.3.zip` を取得します。
2. 展開した `ModManagerEnhancements` フォルダを `Sea Power_Data/StreamingAssets` へ配置します。
3. ゲーム内で本MODとAnchor Chainを有効にし、Sea Powerを完全に終了して再起動します。

MODの選択・並び順の保存には、ゲーム既存の「確定」を使用します。

## 判定範囲

有効なMOD間で同じ相対パスに存在するファイルを比較します。通常の単一ファイル検索では、MOD一覧の上が優先です。同じ内容の重複、ゲーム本体 `original` への通常の上書き、MOD説明やサムネイル等の配布用メタデータは除外します。

INIは読み込み方法によりキー単位で統合されます。INI項目はファイル候補の優先順位を示すもので、個々のキーの採用結果を判定するものではありません。該当項目には注意書きを表示します。

DLLの依存、同一プラグインID、Harmonyパッチの干渉、異なるファイル間の意味的な非互換性は判定しません。ファイル変更やMODの自動整列も行いません。上書きは意図的な差し替えにも使われるため、マークは不具合の判定ではありません。

ファイルの読み取りに失敗した場合は、パネルとBepInExログへ原因を表示します。リンクされたサブフォルダも、黙って読み飛ばさず走査エラーとして報告します。

## ビルドと検証

Windowsの.NET 10 SDK、インストール済みのゲームとAnchor Chainが必要です。ゲームや外部依存のDLLは同梱せず、ローカルのインストール先を参照します。NuGetからのダウンロードは不要です。

`Directory.Build.local.props.example` を `Directory.Build.local.props` としてコピーし、依存先のパスを設定して実行します。

```powershell
.\build.ps1 -Test
```

成果物は `dist/ModManagerEnhancements` に作成します。ビルドはゲームへの配置、起動、ゲーム設定の変更を行いません。

18項目の機能検証が.NET 10とゲーム付属Monoの両方で成功しています。対象はファイル優先順位、有効状態、内容比較、複数提供元、再スキャン、絞り込み、INI注意書き、キャンセル、古い解析結果の排除、失敗からの回復です。自動テストではゲーム内UIの描画やマウス入力の経路は検証しません。

- `src/Core`: Unity・Noesisから独立した解析・結果・表示データ・非同期処理。
- `src/UI`、`src/ManagerController.cs`: 既存画面への組み込みとライフサイクル管理。
- `src/Plugin.cs`: 有効状態の確認とAnchor Chain起動処理。
- `tests`、`tools/test-mono.ps1`: 機能検証と独立したMono検証プロセス。

識別子: `mebiustos.modmanagerenhancements`。バージョン: `0.1.3`。
