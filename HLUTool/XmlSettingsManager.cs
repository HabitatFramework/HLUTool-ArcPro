using System;
using System.IO;
using System.Xml.Serialization;
using System.Reflection;
using System.Linq;
using System.Xml.Linq;
using ArcGIS.Desktop.Framework;

namespace HLU;

/// <summary>
/// Manages the settings for the add-in stored in an XML file.
/// </summary>
public class XmlSettingsManager
{
    private string _settingsFile;

    /// <summary>
    /// Constructor
    /// </summary>
    internal XmlSettingsManager()
    {
        // Get the full file path of the .esriAddInX file.
        string addinPath = GetEsriAddinXPath();

        // Get the directory where the add-in file is loaded from.
        string addInDirectory = Path.GetDirectoryName(addinPath);

        // Get the full path to the settings file.
        _settingsFile = Path.Combine(addInDirectory, "HLUTool.xml");
    }

    /// <summary>
    /// Get the full file path of the .esriAddInX file.
    /// </summary>
    /// <returns></returns>
    public static string GetEsriAddinXPath()
    {
        // Get the collection of all installed add-ins.
        var addIns = FrameworkApplication.GetAddInInfos();

        // Get the name of the currently executing add-in by checking the assembly name.
        var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
        string executingAssemblyName = executingAssembly.GetName().Name;

        // Find the add-in that matches the executing assembly name.
        var currentAddIn = addIns.FirstOrDefault(a => a.Name.ToString().Equals(executingAssemblyName, StringComparison.OrdinalIgnoreCase));

        // Return the original .esriAddinX full file path.
        return currentAddIn?.FullPath;
    }

    /// <summary>
    /// Load the settings from the settings file.
    /// </summary>
    /// <returns></returns>
    public AddInSettings LoadSettings()
    {
        // If the settings file exists, load the settings from the file.
        if (File.Exists(_settingsFile))
        {
            XmlSerializer serializer = new(typeof(AddInSettings));
            using StreamReader reader = new(_settingsFile);
            return (AddInSettings)serializer.Deserialize(reader);
        }

        // If the settings file does not exist, return the default settings.
        return new AddInSettings();
    }

    /// <summary>
    /// Save the settings to the settings file.
    /// </summary>
    /// <param name="settings"></param>
    public void SaveSettings(AddInSettings settings)
    {
        XmlSerializer serializer = new(typeof(AddInSettings));
        using StreamWriter writer = new(_settingsFile);
        serializer.Serialize(writer, settings);
    }

    /// <summary>
    /// Removes a specified node from the XML file.
    /// </summary>
    /// <param name="nodeName"></param>
    public void RemoveNode(string nodeName)
    {
        if (File.Exists(_settingsFile))
        {
            XDocument doc = XDocument.Load(_settingsFile);

            // Find and remove the first occurrence of the node
            XElement nodeToRemove = doc.Root.Element(nodeName);
            if (nodeToRemove != null)
            {
                nodeToRemove.Remove();
                doc.Save(_settingsFile);
            }
        }
    }
}

/// <summary>
/// The settings for the add-in in the XML file.
/// </summary>
public class AddInSettings
{
    // Help URL.
    public string HelpURL { get; set; } = "https://hlutool-userguide.readthedocs.io/en/latest/";

    public HelpPages HelpPages { get; set; } = new();

    // Application database options
    public int DbConnectionTimeout { get; set; } = 60;
    public int IncidTablePageSize { get; set; } = 100;

    // Application dates options
    public string[] SeasonNames { get; set; } = ["Spring", "Summery", "Autumn", "Winter"];
    public string VagueDateDelimiter { get; set; } = "-";

    // Application validation options
    public int HabitatSecondaryCodeValidation { get; set; } = 2;
    public int PrimarySecondaryCodeValidation { get; set; } = 1;
    public int QualityValidation { get; set; } = 1;
    public int PotentialPriorityDetermQtyValidation { get; set; } = 1;

    // Application updates options
    public int SubsetUpdateAction { get; set; } = 0;
    public string ClearIHSUpdateAction { get; set; } = "Do not clear";
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

    //    [XmlElement("BulkOSMMSourceId")]
    public int? BulkOSMMSourceId { get; set; } = null;
}

/// <summary>
/// The individual help pages settings.
/// </summary>
public class HelpPages
{
    // Individual help pages
    public string AppDatabase { get; set; } = "interface/interface.html#database-options";
    public string AppDates { get; set; } = "interface/interface.html#dates-options";
    public string AppBulkUpdate { get; set; } = "interface/interface.html#bulk-update-options";
    public string AppUpdates { get; set; } = "interface/interface.html#updates-options";
    public string AppValidation { get; set; } = "interface/interface.html#updates-options";
    public string UserGIS { get; set; } = "interface/interface.html#gis-export-options";
    public string UserInterface { get; set; } = "interface/interface.html#interface-options";
    public string UserUpdates { get; set; } = "interface/interface.html#updates-options";
    public string UserSQL { get; set; } = "interface/interface.html#filter-options";
    public string UserHistory { get; set; } = "interface/interface.html#history-options";
}
