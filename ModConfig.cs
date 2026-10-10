namespace TouchButtons
{
    internal class ModConfig
    {
        public bool Enabled { get; set; } = true;

        public bool ShowToolbar { get; set; } = true;

        public bool ShowLeftRight { get; set; } = true;

        public bool ShowUtility { get; set; } = true;

        public bool ShowModeToggle { get; set; } = true;

        // false = direct touch, true = slide (touchpad cursor)
        public bool SlideMode { get; set; } = false;

        // idle opacity, 0.1 = ghost, 1.0 = solid
        public float ButtonAlpha { get; set; } = 0.35f;
    }
}
