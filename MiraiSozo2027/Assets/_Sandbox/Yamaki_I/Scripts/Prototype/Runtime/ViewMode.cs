using UnityEngine;

namespace Aura.Prototype
{
    /// @brief カメラの視点
    public enum ViewMode
    {
        [InspectorName("一人称")]
        FirstPerson,  ///< 一人称
        [InspectorName("三人称")]
        ThirdPerson,  ///< 三人称
    }
}
