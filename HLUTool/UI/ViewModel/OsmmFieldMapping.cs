// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2025-2026 Andy Foy Consulting
//
// This file is part of HLUTool.
//
// HLUTool is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// HLUTool is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with HLUTool.  If not, see <http://www.gnu.org/licenses/>.

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Carries the user's selections from the OSMM Load setup dialog:
    /// the name of the input layer and the five field-name mappings that
    /// correspond to the <c>lut_osmm_habitat_xref</c> lookup columns.
    /// </summary>
    internal sealed record OsmmFieldMapping(
        string LayerName,
        string ToidField,
        string MakeField,
        string DescGroupField,
        string DescTermField,
        string ThemeField,
        string FeatCodeField,
        string OutputWorkspace,
        string OutputFeatureClassName);
}
