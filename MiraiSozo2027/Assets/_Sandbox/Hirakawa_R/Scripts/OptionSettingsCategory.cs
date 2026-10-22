using System;

namespace MiraiSozo.Options
{
    [Flags]
    public enum OptionSettingsCategory
    {
        None = 0,
        System = 1,
        Camera = 2,
        Audio = 4,
        Controls = 8,
        All = System | Camera | Audio | Controls
    }
}
