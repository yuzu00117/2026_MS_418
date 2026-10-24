using System;

namespace SceneFlow
{
    /// <summary>
    /// ロード画面を伴うシーン読み込みの実装側が満たす窓口。
    /// SceneTransition は具体的なロード画面(方式A/B/C)を知らず、これだけに依存する。
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>読み込み(ロード画面の表示を含む)中か。</summary>
        bool IsLoading { get; }

        /// <summary>
        /// ロード画面を挟んでシーンを読み込む。
        /// onLoaded は新シーンの初期化が一巡し、ロード画面が消え始める直前に呼ぶ。
        /// onCompleted はロード画面が完全に消え、遷移が終わったときに呼ぶ。
        /// </summary>
        void Load(string scenePath, Action onLoaded, Action onCompleted);
    }
}
