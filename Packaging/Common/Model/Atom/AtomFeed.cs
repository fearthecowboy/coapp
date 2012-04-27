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

namespace CoApp.Packaging.Common.Model.Atom {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.ServiceModel.Syndication;
    using System.Xml;
    using Toolkit.Extensions;

#if COAPP_ENGINE_CORE
    using Packaging.Service;
#endif

    public class AtomFeed : SyndicationFeed {
        // private readonly List<AtomItem> _items = new List<AtomItem>();
        private readonly string _outputFilename;
        private readonly string _packageUrlPrefix;
        private readonly string _actualUrl;
        private readonly string _rootUrl;

        public AtomFeed() {
            LastUpdatedTime = DateTime.Now;
        }

        /* 
         output-file=<f>        
         root-url=<url>         
         package-source=<dir>   
         package-url=<url>     
         
         actual-url=<url>       
         recursive				
         title=<title>
         */

        public AtomFeed(string outputFilename, string rootUrl, string packageUrl, string actualUrl = null, string title = null) : this() {
            Title = new TextSyndicationContent(title ?? "CoApp Package Feed");

            _rootUrl = rootUrl.EndsWith("/") ? rootUrl : rootUrl + "/";
            _actualUrl = actualUrl ?? _rootUrl + Path.GetFileName(outputFilename);

            if (!packageUrl.Contains("://")) {
                packageUrl = _rootUrl + (packageUrl.StartsWith("/") ? packageUrl.Substring(1) : packageUrl);
            }
            _packageUrlPrefix = (packageUrl.EndsWith("/") ? packageUrl : packageUrl + "/");

            var selfLink = CreateLink();
            selfLink.RelationshipType = "self";
            selfLink.MediaType = "application/atom+xml";
            selfLink.Title = "Feed Location";
            selfLink.Uri = new Uri(_actualUrl);
            Links.Add(selfLink);

            Generator = "CoAppEngine";
            _outputFilename = outputFilename;
        }

        public static AtomFeed LoadFile(string localPath) {
            return File.Exists(localPath) ? Load(File.ReadAllText(localPath)) : null;
        }

        public static AtomFeed Load(string xmlDocument) {
            if (xmlDocument.Length < 512 && xmlDocument.FileIsLocalAndExists()) {
                // if I sent a filename here instead, this will let it still work.
                xmlDocument = File.ReadAllText(xmlDocument);
            }

            using (var sr = new StringReader(xmlDocument)) {
                return Load<AtomFeed>(XmlReader.Create(sr));
            }
        }

        public void Save() {
            Save(_outputFilename);
        }

        public void Save(string localPath) {
            File.WriteAllText(localPath, ToString());
        }

        public override string ToString() {
            foreach (var item in from each in Items where (each as AtomItem) != null select each as AtomItem) {
                item.SyncFromModel();
            }

            using (var ms = new MemoryStream()) {
                var writer = XmlWriter.Create(ms);
                var formatter = GetAtom10Formatter();
                formatter.PreserveAttributeExtensions = true;
                formatter.WriteTo(writer);
                writer.WriteEndDocument();
                writer.Close();
                return ms.PrettyXml();
            }
        }

        protected override void WriteAttributeExtensions(XmlWriter writer, string version) {
            writer.WriteAttributeString("xmlns", "coapp", null, "http://coapp.org/atom-package-feed-1.0");
            writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instanceb");
            writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
        }

        /// <summary>
        ///   This adds a new package model to the feed. The package model doesn't have to be completed when added, but the caller must fill in the values before the feed is generated, or it's kinda pointless. :) Use this when trying to create feeds.
        /// </summary>
        /// <param name="model"> </param>
        /// <returns> </returns>
        public AtomItem Add(PackageModel model) {
            lock (this) {
                var item = new AtomItem(model);
                Items = Items.Where(each => each.Id != item.Id).Union(item.SingleItemAsEnumerable()).ToArray();
                return item;
            }
        }

        public AtomItem Add(AtomItem item) {
            lock (this) {
                Items = Items.Where(each => each.Id != item.Id).Union(item.SingleItemAsEnumerable()).ToArray();
                return item;
            }
        }

        public void Add(IEnumerable<AtomItem> items) {
            foreach (var item in items) {
                Add(item);
            }
        }

#if COAPP_ENGINE_CORE 
    /// <summary>
    /// This takes an existing package object and creates a package model from it and inserts it into the feed.
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
        public AtomItem Add(Package package) {
            var item = new AtomItem(package); 
            Items = Items.Union(item.SingleItemAsEnumerable()).ToArray();
            return item;
        }

        public void Add(IEnumerable<Package> packages) {
            Items = Items.Union(packages.Select(each => new AtomItem(each))).ToArray();
        }
        
        public IEnumerable<Package> Packages {
            get {
                return Items.Select(each => {
                    var atomItem = each as AtomItem;
                    return atomItem != null ? atomItem.Package : null;
                });
            }
        } 

#endif

        protected override SyndicationItem CreateItem() {
            return new AtomItem();
        }
    }
}