//---------------------------------------------------------------------
// <copyright file="Entities.cs" company="Microsoft">
//    Copyright (c) Microsoft Corporation.  All rights reserved.
//
//    The use and distribution terms for this software are covered by the
//    Common Public License 1.0 (http://opensource.org/licenses/cpl1.0.php)
//    which can be found in the file CPL.TXT at the root of this distribution.
//    By using this software in any fashion, you are agreeing to be bound by
//    the terms of this license.
//
//    You must not remove this notice, or any other, from this software.
// </copyright>
// <summary>
// Part of the Deployment Tools Foundation project.
// </summary>
//---------------------------------------------------------------------

namespace CoApp.Packaging.Service.dtf.WindowsInstaller.Linq
{
    // Silence warnings about style and doc-comments
    #if !CODE_ANALYSIS
    #pragma warning disable 1591
    #region Generated code

    internal class Component_ : QRecord
    {
        internal string Component   { get { return this[0]; } set { this[0] = value; } }
        internal string ComponentId { get { return this[1]; } set { this[1] = value; } }
        internal string Directory_  { get { return this[2]; } set { this[2] = value; } }
        internal string Condition   { get { return this[4]; } set { this[4] = value; } }
        internal string KeyPath     { get { return this[5]; } set { this[5] = value; } }
        internal ComponentAttributes Attributes
        { get { return (ComponentAttributes) this.I(3); } set { this[3] = ((int) value).ToString(); } }
    }

    internal class CreateFolder_ : QRecord
    {
        internal string Directory_ { get { return this[0]; } set { this[0] = value; } }
        internal string Component_ { get { return this[1]; } set { this[1] = value; } }
    }

    internal class CustomAction_ : QRecord
    {
        internal string Action { get { return this[0]; } set { this[0] = value; } }
        internal string Source { get { return this[2]; } set { this[2] = value; } }
        internal string Target { get { return this[3]; } set { this[3] = value; } }
        internal CustomActionTypes Type
        { get { return (CustomActionTypes) this.I(1); } set { this[1] = ((int) value).ToString(); } }
    }

    internal class Directory_ : QRecord
    {
        internal string Directory        { get { return this[0]; } set { this[0] = value; } }
        internal string Directory_Parent { get { return this[1]; } set { this[1] = value; } }
        internal string DefaultDir       { get { return this[2]; } set { this[2] = value; } }
    }

    internal class DuplicateFile_ : QRecord
    {
        internal string FileKey    { get { return this[0]; } set { this[0] = value; } }
        internal string Component_ { get { return this[1]; } set { this[1] = value; } }
        internal string File_      { get { return this[2]; } set { this[2] = value; } }
        internal string DestName   { get { return this[4]; } set { this[4] = value; } }
        internal string DestFolder { get { return this[5]; } set { this[5] = value; } }
    }

    internal class Feature_ : QRecord
    {
        internal string Feature        { get { return this[0];    } set { this[0] = value; } }
        internal string Feature_Parent { get { return this[1];    } set { this[1] = value; } }
        internal string Title          { get { return this[2];    } set { this[2] = value; } }
        internal string Description    { get { return this[3];    } set { this[3] = value; } }
        internal int?   Display        { get { return this.NI(4); } set { this[4] = value.ToString(); } }
        internal int    Level          { get { return this.I(5);  } set { this[5] = value.ToString(); } }
        internal string Directory_     { get { return this[6];    } set { this[6] = value; } }
        internal FeatureAttributes Attributes
        { get { return (FeatureAttributes) this.I(7); } set { this[7] = ((int) value).ToString(); } }
    }

    [DatabaseTable("FeatureComponents")]
    internal class FeatureComponent_ : QRecord
    {
        internal string Feature_   { get { return this[0]; } set { this[0] = value; } }
        internal string Component_ { get { return this[1]; } set { this[1] = value; } }
    }

    internal class File_ : QRecord
    {
        internal string File       { get { return this[0];   } set { this[0] = value; } }
        internal string Component_ { get { return this[1];   } set { this[1] = value; } }
        internal string FileName   { get { return this[2];   } set { this[2] = value; } }
        internal int    FileSize   { get { return this.I(3); } set { this[3] = value.ToString(); } }
        internal string Version    { get { return this[4];   } set { this[4] = value; } }
        internal string Language   { get { return this[5];   } set { this[5] = value; } }
        internal int    Sequence   { get { return this.I(7); } set { this[7] = value.ToString(); } }
        internal FileAttributes Attributes
        { get { return (FileAttributes) this.I(6); } set { this[6] = ((int) value).ToString(); } }
    }

    [DatabaseTable("MsiFileHash")]
    internal class FileHash_ : QRecord
    {
        internal string File_     { get { return this[0];   } set { this[0] = value; } }
        internal int    Options   { get { return this.I(1); } set { this[1] = value.ToString(); } }
        internal int    HashPart1 { get { return this.I(2); } set { this[2] = value.ToString(); } }
        internal int    HashPart2 { get { return this.I(3); } set { this[3] = value.ToString(); } }
        internal int    HashPart3 { get { return this.I(4); } set { this[4] = value.ToString(); } }
        internal int    HashPart4 { get { return this.I(5); } set { this[5] = value.ToString(); } }
    }

    [DatabaseTable("InstallExecuteSequence")]
    internal class InstallSequence_ : QRecord
    {
        internal string Action    { get { return this[0];   } set { this[0] = value; } }
        internal string Condition { get { return this[1];   } set { this[1] = value; } }
        internal int    Sequence  { get { return this.I(2); } set { this[2] = value.ToString(); } }
    }

    internal class LaunchCondition_ : QRecord
    {
        internal string Condition   { get { return this[0]; } set { this[0] = value; } }
        internal string Description { get { return this[1]; } set { this[1] = value; } }
    }

    internal class Media_ : QRecord
    {
        internal int    DiskId       { get { return this.I(0); } set { this[0] = value.ToString(); } }
        internal int    LastSequence { get { return this.I(1); } set { this[1] = value.ToString(); } }
        internal string DiskPrompt   { get { return this[2];   } set { this[2] = value; } }
        internal string Cabinet      { get { return this[3];   } set { this[3] = value; } }
        internal string VolumeLabel  { get { return this[4];   } set { this[4] = value; } }
        internal string Source       { get { return this[5];   } set { this[5] = value; } }
    }

    internal class Property_ : QRecord
    {
        internal string Property { get { return this[0]; } set { this[0] = value; } }
        internal string Value    { get { return this[1]; } set { this[1] = value; } }
    }

    internal class Registry_ : QRecord
    {
        internal string Registry   { get { return this[0]; } set { this[0] = value; } }
        internal string Key        { get { return this[2]; } set { this[2] = value; } }
        internal string Name       { get { return this[3]; } set { this[3] = value; } }
        internal string Value      { get { return this[4]; } set { this[4] = value; } }
        internal string Component_ { get { return this[5]; } set { this[5] = value; } }
        internal RegistryRoot Root
        { get { return (RegistryRoot) this.I(1); } set { this[0] = ((int) value).ToString(); } }
    }

    internal class RemoveFile_ : QRecord
    {
        internal string FileKey     { get { return this[0]; } set { this[0] = value; } }
        internal string Component_  { get { return this[2]; } set { this[2] = value; } }
        internal string FileName    { get { return this[3]; } set { this[3] = value; } }
        internal string DirProperty { get { return this[4]; } set { this[4] = value; } }
        internal RemoveFileModes InstallMode
        { get { return (RemoveFileModes) this.I(5); } set { this[5] = ((int) value).ToString(); } }
    }

    #endregion // Generated code
    #pragma warning restore 1591
    #endif // !CODE_ANALYSIS
}
