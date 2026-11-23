# Mod Manager Enhancementsの翻訳

MMEはSea Power標準の言語辞書を利用します。DLLには翻訳キーと引数の整形処理を置き、文言は `package/language_en/mod_manager_enhancements.json` と `package/language_ja/mod_manager_enhancements.json` に分離しています。

第三者は、MMEのDLLを改変・再配布せず、別の翻訳MODから文言を追加・上書きできます。

## 翻訳MODの作成

1. 英語のJSONファイルを、通常のMODフォルダーの `language_<言語コード>/mod_manager_enhancements.json` にコピーします。ゲーム側の言語コードを使ってください。例は `fr`、`de`、`ru`、中国語は `cn` です。
2. 値を翻訳します。`ModManagerEnhancements` というオブジェクト名、各キー、差し込み引数は変更しません。
3. 翻訳MOD自身の `_info.ini` を用意し、MMEと翻訳MODを有効にします。同梱言語を上書きするときは、翻訳MODをMMEより上に並べます。
4. ファイルの追加・編集やMODの有効化後はゲームを再起動し、言語辞書を読み直します。

例：

```text
MyMMEFrenchTranslation/
  _info.ini
  language_fr/
    mod_manager_enhancements.json
```

一部のキーだけを翻訳することもできます。

```json
{
  "ModManagerEnhancements": {
    "SearchPlaceholder": "Rechercher des mods",
    "Sync": "Synchroniser",
    "CompatibilityConfirmation": "Version {1} : {0}"
  }
}
```

未翻訳のキーはゲーム標準の仕組みで英語にフォールバックします。英語ファイルの他のキーも翻訳すれば、翻訳範囲を広げられます。最後の値は引数の並べ替えの例です。完成した翻訳では、確認文の説明や注意事項も残してください。

UTF-8のJSONを使用し、改行は `\n`、引用符は `\"` と書きます。言語フォルダー全体を `ReplacesFolders` で置換する必要はありません。フォルダーを置換すると、他の翻訳も隠してしまいます。

対象はSea Powerが対応している言語です。MMEがゲームの言語選択肢自体を増やすものではありません。ゲーム標準部分の文言は、引き続きゲーム自身の翻訳キーを使用します。

## 翻訳キーと差し込み引数

英語ファイルがキーの一覧です。キーは翻訳MOD向けの公開仕様として維持します。翻訳のためにキー名を変更しないでください。文言の変更にDLLの再ビルドは必要ありません。

引数は.NETの複合書式を使用します。

| キー | 引数 |
| --- | --- |
| `FilteredSelection` | `{0}`：選択中のMOD名 |
| `CompatibilityConfirmation` | `{0}`：MOD名、`{1}`：現在のゲームバージョン |
| `CompatibilityRestoreConfirmation` | `{0}`：MOD名 |
| `CompatibilityFailure` | `{0}`：エラーの詳細 |
| `Summary` | `{0}`：重複ファイル数 |
| `Tooltip` | `{0}`：優先件数、`{1}`：上書きされる件数、`{2}`：翻訳済みのクリック案内 |

引数の順序は変更できます。引数付き文言で波括弧そのものを表示するときは `{{`、`}}` と書きます。MOD名に含まれる波括弧はそのまま挿入されるため、エスケープ不要です。ツールチップのクリック案内は `FilterOverridesAction` と `ClearFilterAction` で翻訳します。

不正なJSONや書式引数はエラーとして扱い、DLL内の固定文言に置き換えて隠しません。読み込まれない場合はゲームのログを確認してください。選択言語にも英語にも存在しないキーは、ゲーム標準の欠落時のエラー処理に従います。

## シーン再読み込み

文字列を取得するたびに現在の言語ハンドラーを参照します。旧シーンのハンドラーを保持したり、そのイベントを購読したりしません。言語のフォールバックとMODの優先順位はSea Power側の仕組みに任せます。
