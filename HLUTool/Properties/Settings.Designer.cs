﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace HLU.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.12.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DbConnectionPersistSecurityInfo {
            get {
                return ((bool)(this["DbConnectionPersistSecurityInfo"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string DbDefaultSchema {
            get {
                return ((string)(this["DbDefaultSchema"]));
            }
            set {
                this["DbDefaultSchema"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool DbPromptPwd {
            get {
                return ((bool)(this["DbPromptPwd"]));
            }
            set {
                this["DbPromptPwd"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string DbConnectionString {
            get {
                return ((string)(this["DbConnectionString"]));
            }
            set {
                this["DbConnectionString"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DbIsUnicode {
            get {
                return ((bool)(this["DbIsUnicode"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DbUseTimeZone {
            get {
                return ((bool)(this["DbUseTimeZone"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("50")]
        public uint DbTextLength {
            get {
                return ((uint)(this["DbTextLength"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("50")]
        public uint DbBinaryLength {
            get {
                return ((uint)(this["DbBinaryLength"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public uint DbTimePrecision {
            get {
                return ((uint)(this["DbTimePrecision"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public uint DbNumericPrecision {
            get {
                return ((uint)(this["DbNumericPrecision"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public uint DbNumericScale {
            get {
                return ((uint)(this["DbNumericScale"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public int DbConnectionType {
            get {
                return ((int)(this["DbConnectionType"]));
            }
            set {
                this["DbConnectionType"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UseAutomaticCommandBuilders {
            get {
                return ((bool)(this["UseAutomaticCommandBuilders"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("^")]
        public string ConverterParameterSeparator {
            get {
                return ((string)(this["ConverterParameterSeparator"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("geom1")]
        public string HistoryGeometry1ColumnName {
            get {
                return ((string)(this["HistoryGeometry1ColumnName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("geom2")]
        public string HistoryGeometry2ColumnName {
            get {
                return ((string)(this["HistoryGeometry2ColumnName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UseObjectFactory {
            get {
                return ((bool)(this["UseObjectFactory"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("description")]
        public string LutDescriptionFieldName {
            get {
                return ((string)(this["LutDescriptionFieldName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2")]
        public int LutDescriptionFieldOrdinal {
            get {
                return ((int)(this["LutDescriptionFieldOrdinal"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public int HistoryDisplayLastN {
            get {
                return ((int)(this["HistoryDisplayLastN"]));
            }
            set {
                this["HistoryDisplayLastN"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<clear>")]
        public string CodeDeleteRow {
            get {
                return ((string)(this["CodeDeleteRow"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool BulkUpdateUsesAdo {
            get {
                return ((bool)(this["BulkUpdateUsesAdo"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public int WarnBeforeGISSelect {
            get {
                return ((int)(this["WarnBeforeGISSelect"]));
            }
            set {
                this["WarnBeforeGISSelect"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("~~~")]
        public string HistoryAdditionalFieldsDelimiter {
            get {
                return ((string)(this["HistoryAdditionalFieldsDelimiter"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("NP")]
        public string BAPDeterminationQualityUserAdded {
            get {
                return ((string)(this["BAPDeterminationQualityUserAdded"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool CallUpgrade {
            get {
                return ((bool)(this["CallUpgrade"]));
            }
            set {
                this["CallUpgrade"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("N")]
        public string SourceImportanceSkip {
            get {
                return ((string)(this["SourceImportanceSkip"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("P")]
        public string SourceImportanceApply1 {
            get {
                return ((string)(this["SourceImportanceApply1"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("S")]
        public string SourceImportanceApply2 {
            get {
                return ((string)(this["SourceImportanceApply2"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("C")]
        public string SourceImportanceApply3 {
            get {
                return ((string)(this["SourceImportanceApply3"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("P")]
        public string MinimumDbVersion {
            get {
                return ((string)(this["MinimumDbVersion"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("ICAT")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string PreferredHabitatClass {
            get {
                return ((string)(this["PreferredHabitatClass"]));
            }
            set {
                this["PreferredHabitatClass"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool NotifyOnSplitMerge {
            get {
                return ((bool)(this["NotifyOnSplitMerge"]));
            }
            set {
                this["NotifyOnSplitMerge"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string SqlPath {
            get {
                return ((string)(this["SqlPath"]));
            }
            set {
                this["SqlPath"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10000")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public int GetValueRows {
            get {
                return ((int)(this["GetValueRows"]));
            }
            set {
                this["GetValueRows"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("100000")]
        public int MaxGetValueRows {
            get {
                return ((int)(this["MaxGetValueRows"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("30000")]
        public int MaxSqlLengthArcGIS {
            get {
                return ((int)(this["MaxSqlLengthArcGIS"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("source_name")]
        public string LutSourceFieldName {
            get {
                return ((string)(this["LutSourceFieldName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2")]
        public int LutSourceFieldOrdinal {
            get {
                return ((int)(this["LutSourceFieldOrdinal"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("user_name")]
        public string LutUserFieldName {
            get {
                return ((string)(this["LutUserFieldName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2")]
        public int LutUserFieldOrdinal {
            get {
                return ((int)(this["LutUserFieldOrdinal"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool ShowNVCCodes {
            get {
                return ((bool)(this["ShowNVCCodes"]));
            }
            set {
                this["ShowNVCCodes"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public int AutoZoomSelection {
            get {
                return ((int)(this["AutoZoomSelection"]));
            }
            set {
                this["AutoZoomSelection"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool AutoSelectOnGis {
            get {
                return ((bool)(this["AutoSelectOnGis"]));
            }
            set {
                this["AutoSelectOnGis"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1000")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public int MinAutoZoom {
            get {
                return ((int)(this["MinAutoZoom"]));
            }
            set {
                this["MinAutoZoom"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("m")]
        public string MapDistanceUnits {
            get {
                return ((string)(this["MapDistanceUnits"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1000000")]
        public int MaxAutoZoom {
            get {
                return ((int)(this["MaxAutoZoom"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("When Outstanding")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string ShowOSMMUpdatesOption {
            get {
                return ((string)(this["ShowOSMMUpdatesOption"]));
            }
            set {
                this["ShowOSMMUpdatesOption"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool ShowGroupHeaders {
            get {
                return ((bool)(this["ShowGroupHeaders"]));
            }
            set {
                this["ShowGroupHeaders"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<all>")]
        public string CodeAnyRow {
            get {
                return ((string)(this["CodeAnyRow"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("PP")]
        public string BAPDeterminationQualityPrevious {
            get {
                return ((string)(this["BAPDeterminationQualityPrevious"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Not present but close to definition")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string BAPDeterminationQualityUserAddedDesc {
            get {
                return ((string)(this["BAPDeterminationQualityUserAddedDesc"]));
            }
            set {
                this["BAPDeterminationQualityUserAddedDesc"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Previously present, but may no longer exist")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string BAPDeterminationQualityPreviousDesc {
            get {
                return ((string)(this["BAPDeterminationQualityPreviousDesc"]));
            }
            set {
                this["BAPDeterminationQualityPreviousDesc"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool ShowIHSTab {
            get {
                return ((bool)(this["ShowIHSTab"]));
            }
            set {
                this["ShowIHSTab"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<All>")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string PreferredSecondaryGroup {
            get {
                return ((string)(this["PreferredSecondaryGroup"]));
            }
            set {
                this["PreferredSecondaryGroup"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public string SecondaryCodeOrder {
            get {
                return ((string)(this["SecondaryCodeOrder"]));
            }
            set {
                this["SecondaryCodeOrder"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("sort_order")]
        public string LutSortOrderFieldName {
            get {
                return ((string)(this["LutSortOrderFieldName"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool ShowHabitatSecondariesSuggested {
            get {
                return ((bool)(this["ShowHabitatSecondariesSuggested"]));
            }
            set {
                this["ShowHabitatSecondariesSuggested"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool ShowHabitatSummary {
            get {
                return ((bool)(this["ShowHabitatSummary"]));
            }
            set {
                this["ShowHabitatSummary"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool ShowSourceHabitatGroup {
            get {
                return ((bool)(this["ShowSourceHabitatGroup"]));
            }
            set {
                this["ShowSourceHabitatGroup"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.SpecialSettingAttribute(global::System.Configuration.SpecialSetting.ConnectionString)]
        [global::System.Configuration.DefaultSettingValueAttribute("Data Source=ZIPY\\SQLEXPRESS2016;Initial Catalog=HLUData;Integrated Security=True")]
        public string HluConnectionString {
            get {
                return ((string)(this["HluConnectionString"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.SpecialSettingAttribute(global::System.Configuration.SpecialSetting.ConnectionString)]
        [global::System.Configuration.DefaultSettingValueAttribute("Data Source=zipy\\sqldeveloper2016;Initial Catalog=HLUData;Integrated Security=Tru" +
            "e")]
        public string HLUDataConnectionString {
            get {
                return ((string)(this["HLUDataConnectionString"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.SpecialSettingAttribute(global::System.Configuration.SpecialSetting.ConnectionString)]
        [global::System.Configuration.DefaultSettingValueAttribute("Data Source=ZIPY\\SQLDEVELOPER2016;Initial Catalog=HLUData_V401;Integrated Securit" +
            "y=True")]
        public string HLUData_v400ConnectionString {
            get {
                return ((string)(this["HLUData_v400ConnectionString"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.SpecialSettingAttribute(global::System.Configuration.SpecialSetting.ConnectionString)]
        [global::System.Configuration.DefaultSettingValueAttribute("Data Source=ZIPY\\SQLDEVELOPER2017;Initial Catalog=HLUData_v401;Integrated Securit" +
            "y=True")]
        public string HLUData_v401ConnectionString {
            get {
                return ((string)(this["HLUData_v401ConnectionString"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.SpecialSettingAttribute(global::System.Configuration.SpecialSetting.ConnectionString)]
        [global::System.Configuration.DefaultSettingValueAttribute("Data Source=ZIPY\\SQLEXPRESS2017;Initial Catalog=HLUData_v401;Integrated Security=" +
            "True")]
        public string HLUData_v401ConnectionString_2017 {
            get {
                return ((string)(this["HLUData_v401ConnectionString_2017"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<ArrayOfString xmlns:xsd=\"http://www.w3." +
            "org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n  <s" +
            "tring>0</string>\r\n  <string>1</string>\r\n  <string>2</string>\r\n</ArrayOfString>")]
        public global::System.Collections.Specialized.StringCollection GisIDColumnOrdinals {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["GisIDColumnOrdinals"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(@"<?xml version=""1.0"" encoding=""utf-16""?>
<ArrayOfString xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <string>Never</string>
  <string>When Outstanding</string>
  <string>Always</string>
</ArrayOfString>")]
        public global::System.Collections.Specialized.StringCollection ShowOSMMUpdatesOptions {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["ShowOSMMUpdatesOptions"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(@"<?xml version=""1.0"" encoding=""utf-16""?>
<ArrayOfString xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <string>Rejected</string>
  <string>Ignored</string>
  <string>Proposed</string>
</ArrayOfString>")]
        public global::System.Collections.Specialized.StringCollection OSMMUpdatesStatuses {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["OSMMUpdatesStatuses"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(@"<?xml version=""1.0"" encoding=""utf-16""?>
<ArrayOfString xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <string>As entered</string>
  <string>By group then code</string>
  <string>By code</string>
</ArrayOfString>")]
        public global::System.Collections.Specialized.StringCollection SecondaryCodeOrderOptions {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["SecondaryCodeOrderOptions"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(@"<?xml version=""1.0"" encoding=""utf-16""?>
<ArrayOfString xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <string>Do not clear</string>
  <string>Clear on change in primary code only</string>
  <string>Clear on change in primary or secondary codes only</string>
  <string>Clear on any change</string>
</ArrayOfString>")]
        public global::System.Collections.Specialized.StringCollection ClearIHSUpdateActions {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["ClearIHSUpdateActions"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<ArrayOfString xmlns:xsd=\"http://www.w3." +
            "org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\r\n  <s" +
            "tring>4</string>\r\n</ArrayOfString>")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public global::System.Collections.Specialized.StringCollection HistoryColumnOrdinals {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["HistoryColumnOrdinals"]));
            }
            set {
                this["HistoryColumnOrdinals"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("_")]
        public string ColumnTableNameSeparator {
            get {
                return ((string)(this["ColumnTableNameSeparator"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("SQLServer")]
        public string DefaultConnectionType {
            get {
                return ((string)(this["DefaultConnectionType"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("***")]
        public string PasswordMaskString {
            get {
                return ((string)(this["PasswordMaskString"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        [global::System.Configuration.SettingsManageabilityAttribute(global::System.Configuration.SettingsManageability.Roaming)]
        public bool CallXMLUpgrade {
            get {
                return ((bool)(this["CallXMLUpgrade"]));
            }
            set {
                this["CallXMLUpgrade"] = value;
            }
        }
    }
}
