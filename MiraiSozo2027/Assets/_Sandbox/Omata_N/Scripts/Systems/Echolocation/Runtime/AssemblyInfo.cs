// 表示状態の報告はURP描画側だけが行う。外部スクリプトからは読み取り専用にする。
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Echo.URP")]
