using System;
using System.IO;
using System.Xml.Serialization;
using System.Reflection;

namespace HLU;

public class AddInSettings
{
    public string HelpURL { get; set; } = "https://hlutool-userguide.readthedocs.io/en/latest/";

    // Application database options
    public int DbConnectionTimeout { get; set; } = 60;
    public int IncidTablePageSize { get; set; } = 100;

    // Application dates options
    public string[] SeasonNames { get; set; } = ["Spring", "Summery", "Autumn", "Winter"];
    public string VagueDateDelimiter { get; set; } = "-";

    // Application validation options
    public int HabitatSecondaryCodeValidation { get; set; } = 2;
    public int PrimarySecondaryCodeValidation { get; set; } = 1;
    public int QualityValidation { get; set; } = 0;
    public int PotentialPriorityDeterminQtyValidation { get; set; } = 0;

    // Application updates options
    public int SubsetUpdateAction { get; set; } = 0;
    public int ClearIHSUpdateAction { get; set; } = 0;
    public string SecondaryCodeDelimiter { get; set; } = ".";
    public bool ResetOSMMUpdatesStatus { get; set; } = false;

    // Application bulk update options
    public bool BulkUpdateDeleteOrphanBapHabitats { get; set; } = false;
    public bool BulkUpdateDeletePotentialBapHabitats { get; set; } = false;
    public bool BulkUpdateDeleteIHSCodes { get; set; } = false;
    public bool BulkUpdateDeleteSecondaryCodes { get; set; } = false;
    public bool BulkUpdateCreateHistoryRecords { get; set; } = true;
    public string BulkUpdateDeterminationQuality { get; set; } = @"PI";
    public string BulkUpdateInterpretationQuality { get; set; } = @"M2";
    public int BulkOSMMSourceId { get; set; } = 0;
}

public class XmlSettingsManager
{
    private string _settingsFile;

    /// <summary>
    /// Constructor
    /// </summary>
    internal XmlSettingsManager()
    {
        // Get the full path of the executing assembly (DLL inside the .esriAddinX package)
        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        // Get the directory where the add-in is loaded from
        string addInDirectory = Path.GetDirectoryName(assemblyPath);

        // Get the full path to the settings file
        _settingsFile = Path.Combine(addInDirectory, "Settings.xml");
    }

    /// <summary>
    /// Check if the settings file exists
    /// </summary>
    /// <returns></returns>
    public bool SettingsFound()
    {
        // Return true if the settings file exists
        return File.Exists(_settingsFile);
    }


    /// <summary>
    /// Load the settings from the settings file
    /// </summary>
    /// <returns></returns>
    public AddInSettings LoadSettings()
    {
        if (File.Exists(_settingsFile))
        {
            XmlSerializer serializer = new(typeof(AddInSettings));
            using StreamReader reader = new(_settingsFile);
            return (AddInSettings)serializer.Deserialize(reader);
        }
        return new AddInSettings(); // Default settings
    }

    /// <summary>
    /// Save the settings to the settings file
    /// </summary>
    /// <param name="settings"></param>
    public void SaveSettings(AddInSettings settings)
    {
        XmlSerializer serializer = new(typeof(AddInSettings));
        using StreamWriter writer = new(_settingsFile);
        serializer.Serialize(writer, settings);
    }
}