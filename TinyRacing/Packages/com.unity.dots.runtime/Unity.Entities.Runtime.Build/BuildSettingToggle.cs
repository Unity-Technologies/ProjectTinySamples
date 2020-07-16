namespace Unity.Entities.Runtime.Build
{
    /// <summary>
    /// Toggle for enabling/disabling settings for BuildConfiguration components with a default value of
    /// UseBuildConfiguration where the value is determined by some criteria specific to the overall
    /// BuildConfiguration. When using this enum, ensure you document (ideally with at least a [ToolTip("")]
    /// attribute on the field) what the expected behaviour is for when "UseBuildConfiguration" is selected.
    /// </summary>
    public enum BuildSettingToggle
    {
        UseBuildConfiguration,
        Enabled,
        Disabled,
    }
}
