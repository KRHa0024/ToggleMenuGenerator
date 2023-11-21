# ToggleAnimGenerator
VRCアバターにおけるトグルアニメーションクリップを生成します！
色々機能追加中

### 説明
ToggleAnimGeneratorはUnityエディター拡張機能で、指定されたGameObjectのアクティブ/非アクティブ状態を切り替えるアニメーションクリップを生成するツールです。このツールは、VRCアバターにおける衣装や小物のトグルスイッチに使用されがちなアニメーションクリップを生成します。
つまり、階層構造を持つGameObjectに対して、その最上位の親オブジェクトにアタッチされたAnimatorを使用して、子オブジェクトのアクティブ/非アクティブ化を制御するアニメーションクリップを生成します。

誰かわかりやすい説明ください

### ダウンロード
[ここ](https://github.com/KRHa0024/ToggleAnimGenerator/releases/tag/Latest)から。

### 使い方
1. Unityエディターのメニューバーから「くろ～は > ToggleAnimGenerator」を選択して、ToggleAnimGenerator ウィンドウを開きます。
2. 「GameObjectの数」フィールドに、アニメーションを生成したいGameObjectの数を入力します。
3. 各GameObjectフィールドに、アニメーションを生成したいGameObjectをドラッグ＆ドロップします。
4. 必要に応じて、「アニメーションをまとめる」チェックボックスを使用して、複数のGameObjectのアニメーションを結合します。
5. 「保存先を変更」フィールドで、生成されたアニメーションクリップの保存先を指定します。
6. 「アニメーションを生成」ボタンをクリックして、アニメーションクリップを生成します。

### 注意事項
* このツールはUnityエディター専用です。ビルドされたアプリケーションでは動作しません。
* アニメーションは Assets フォルダ以下にのみ保存できます。
* 生成されたアニメーションクリップは、手動で適切なAnimatorコントローラーに追加する必要があります。

### ライセンス
MITです。表示が必要な場合は以下を参考にどうぞ。
Copyright (c) 2023 KRHa
Released under the MIT license
https://opensource.org/licenses/mit-license.php

### サポート
製作者が飽きた時点でサポート終了です。
