//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Common.Model {
    using System.ComponentModel;

    public enum LicenseId {
        [Description("Other/unknown license"), Location("")]
        Other,

        [Description("Apache License, 2.0 "), Location("http://opensource.org/licenses/Apache-2.0")]
        Apache20,

        [Description("Apache Software License 1.1 "), Location("http://opensource.org/licenses/apachepl-1.1")]
        Apache11,

        [Description("BSD 3-Clause 'New' or 'Revised' license "), Location("http://opensource.org/licenses/BSD-3-Clause")]
        BSD3Clause,

        [Description("BSD 3-Clause 'Simplified' or 'FreeBSD' license "), Location("http://opensource.org/licenses/BSD-2-Clause")]
        BSD2Clause,

        [Description("GNU General Public License 2.0"), Location("http://opensource.org/licenses/gpl-2.0.php")]
        GPL20,

        [Description("GNU General Public License 3.0"), Location("http://opensource.org/licenses/gpl-3.0.html")]
        GPL30,

        [Description("GNU Library or 'Lesser' General Public License 2.1"), Location("http://opensource.org/licenses/lgpl-2.1.php")]
        LGPL21,

        [Description("GNU Library or 'Lesser' General Public License 3.0"), Location("http://opensource.org/licenses/lgpl-3.0.html")]
        LGPL30,

        [Description("MIT license "), Location("http://opensource.org/licenses/MIT")]
        MIT,

        [Description("Mozilla Public License 1.1 "), Location("http://opensource.org/licenses/MPL-1.1")]
        MPL11,

        [Description("Common Development and Distribution License "), Location("http://opensource.org/licenses/CDDL-1.0")]
        CDDL10,

        [Description("Eclipse Public License "), Location("http://opensource.org/licenses/EPL-1.0")]
        EPL10,

        [Description("IPA Font License "), Location("http://opensource.org/licenses/IPA")]
        IPA,

        [Description("NASA Open Source Agreement 1.3 "), Location("http://opensource.org/licenses/NASA-1.3")]
        NASA13,

        [Description("Open Font License 1.1 "), Location("http://opensource.org/licenses/OFL-1.1")]
        OFL11,

        [Description("Adaptive Public License "), Location("http://opensource.org/licenses/APL-1.0")]
        APL10,

        [Description("Artistic license 2.0 "), Location("http://opensource.org/licenses/Artistic-2.0")]
        Artistic20,

        [Description("Open Software License "), Location("http://opensource.org/licenses/OSL-3.0")]
        OSL30,

        [Description("Q Public License "), Location("http://opensource.org/licenses/QPL-1.0")]
        QPL10,

        [Description("zlib/libpng license "), Location("http://opensource.org/licenses/Zlib")]
        Zlib,

        [Description("Academic Free License "), Location("http://opensource.org/licenses/AFL-3.0")]
        AFL30,

        [Description("Attribution Assurance Licenses "), Location("http://opensource.org/licenses/AAL")]
        AAL,

        [Description("Eiffel Forum License V2.0 "), Location("http://opensource.org/licenses/EFL-2.0")]
        EFL20,

        [Description("Fair License "), Location("http://opensource.org/licenses/Fair")]
        Fair,

        [Description("Historical Permission Notice and Disclaimer "), Location("http://opensource.org/licenses/HPND")]
        HPND,

        [Description("Lucent Public License Version 1.02 "), Location("http://opensource.org/licenses/LPL-1.02")]
        LPL102,

        [Description("The PostgreSQL License "), Location("http://opensource.org/licenses/PostgreSQL")]
        PostgreSQL,

        [Description("University of Illinois/NCSA Open Source License "), Location("http://opensource.org/licenses/NCSA")]
        NCSA,

        [Description("X.Net License "), Location("http://opensource.org/licenses/Xnet")]
        Xnet,

        [Description("Apple Public Source License "), Location("http://opensource.org/licenses/APSL-2.0")]
        APSL20,

        [Description("Computer Associates Trusted Open Source License 1.1 "), Location("http://opensource.org/licenses/CATOSL-1.1")]
        CATOSL11,

        [Description("CUA Office Public License Version 1.0 "), Location("http://opensource.org/licenses/CUA-OPL-1.0")]
        CUAOPL10,

        [Description("EU DataGrid Software License "), Location("http://opensource.org/licenses/EUDatagrid")]
        EUDatagrid,

        [Description("Entessa Public License "), Location("http://opensource.org/licenses/Entessa")]
        Entessa,

        [Description("Frameworx License "), Location("http://opensource.org/licenses/Frameworx-1.0")]
        Frameworx10,

        [Description("IBM Public License "), Location("http://opensource.org/licenses/IPL-1.0")]
        IPL10,

        [Description("LaTeX Project Public License "), Location("http://opensource.org/licenses/LPPL-1.3c")]
        LPPL13c,

        [Description("Motosoto License "), Location("http://opensource.org/licenses/Motosoto")]
        Motosoto,

        [Description("Multics License "), Location("http://opensource.org/licenses/Multics")]
        Multics,

        [Description("Nethack General Public License "), Location("http://opensource.org/licenses/NGPL")]
        NGPL,

        [Description("Nokia Open Source License "), Location("http://opensource.org/licenses/Nokia")]
        Nokia,

        [Description("OCLC Research Public License 2.0 "), Location("http://opensource.org/licenses/OCLC-2.0")]
        OCLC20,

        [Description("PHP License "), Location("http://opensource.org/licenses/PHP-3.0")]
        PHP30,

        [Description("Python License "), Location("http://opensource.org/licenses/Python-2.0")]
        Python20,

        [Description("RealNetworks Public Source License V1.0 "), Location("http://opensource.org/licenses/RPSL-1.0")]
        RPSL10,

        [Description("Ricoh Source Code Public License "), Location("http://opensource.org/licenses/RSCPL")]
        RSCPL,

        [Description("Sleepycat License "), Location("http://opensource.org/licenses/Sleepycat")]
        Sleepycat,

        [Description("Sun Public License "), Location("http://opensource.org/licenses/SPL")]
        SPL,

        [Description("Sybase Open Watcom Public License 1.0 "), Location("http://opensource.org/licenses/Watcom-1.0")]
        Watcom10,

        [Description("Vovida Software License v. 1.0 "), Location("http://opensource.org/licenses/VSL-1.0")]
        VSL10,

        [Description("W3C License "), Location("http://opensource.org/licenses/W3C")]
        W3C,

        [Description("wxWindows Library License "), Location("http://opensource.org/licenses/WXwindows")]
        WXwindows,

        [Description("Zope Public License "), Location("http://opensource.org/licenses/ZPL-2.0")]
        ZPL20,

        [Description("Lucent Public License "), Location("http://opensource.org/licenses/plan9")]
        Plan9,

        [Description("Mozilla Public License 1.0 "), Location("http://opensource.org/licenses/mozilla1.0")]
        MPL,

        [Description("Open Software License 1.0 "), Location("http://opensource.org/licenses/osl-1.0")]
        OSL10,

        [Description("Reciprocal Public License "), Location("http://opensource.org/licenses/rpl")]
        RPL,

        [Description("Intel Open Source License "), Location("http://opensource.org/licenses/intel-open-source-license")]
        IntelOSL,

        [Description("Jabber Open Source License "), Location("http://opensource.org/licenses/jabberpl")]
        Jabberpl,

        [Description("Common Public License 1.0"), Location("http://opensource.org/licenses/cpl1.0")]
        Cpl1,

        [Description("Artistic license 1.0"), Location("http://opensource.org/licenses/artistic-license-1.0")]
        Artistic1,

        [Description("Eiffel Forum License V1.0 "), Location("http://opensource.org/licenses/ver1_eiffel")]
        Eiffel1,

        [Description("Naumen Public License "), Location("http://opensource.org/licenses/Naumen")]
        Naumen,

        [Description("CNRI Python license "), Location("http://opensource.org/licenses/pythonpl")]
        CNRI,

        [Description("Educational Community License "), Location("http://opensource.org/licenses/ecl1")]
        ECL1,

        [Description("MITRE Collaborative Virtual Workspace License "), Location("http://opensource.org/licenses/mitrepl")]
        CVW,

        [Description("Sun Industry Standards Source License "), Location("http://opensource.org/licenses/sisslpl")]
        SISSL,

        [Description("Boost Software License "), Location("http://opensource.org/licenses/bsl1.0")]
        BSL10,

        [Description("Common Public Attribution License 1.0 "), Location("http://opensource.org/licenses/cpal_1.0")]
        CPAL,

        [Description("GNU Affero General Public License v3 "), Location("http://opensource.org/licenses/AGPL-3.0")]
        AGPL30,

        [Description("ISC License "), Location("http://opensource.org/licenses/ISC")]
        ISC,

        [Description("Microsoft Public License "), Location("http://opensource.org/licenses/MS-PL")]
        MSPL,

        [Description("Microsoft Reciprocal License "), Location("http://opensource.org/licenses/MS-RL")]
        MSRL,

        [Description("MirOS Licence "), Location("http://opensource.org/licenses/MirOs")]
        MirOs,

        [Description("Non-Profit Open Software License 3.0 "), Location("http://opensource.org/licenses/NPOSL-3.0")]
        NPOSL30,

        [Description("NTP License "), Location("http://opensource.org/licenses/NTP")]
        NTP,

        [Description("Reciprocal Public License 1.5 "), Location("http://opensource.org/licenses/RPL-1.5")]
        RPL15,

        [Description("Simple Public License 2.0 "), Location("http://opensource.org/licenses/Simple-2.0")]
        Simple20,

        [Description("Open Group Test Suite License "), Location("http://opensource.org/licenses/OGTSL")]
        OGTSL,

        [Description("European Union Public License "), Location("http://www.osor.eu/eupl/european-union-public-licence-eupl-v.1.1")]
        EUPL11,
    }
}