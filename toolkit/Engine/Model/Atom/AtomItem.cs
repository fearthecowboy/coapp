//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

#region tmp

/*
    <entry>
        <id>$Model.ProductCode</id>
        <title type="text">$Model.CosmeticName</title>
        <summary>$Model.ShortDescription</summary>
        <published>$Model.PublishDate</published>
         
        <author>
          <name>$Model.Publisher.Name</name>
          <uri>$Model.Publisher.Location</uri>
          <email>$Model.Publisher.Email</email>
        </author>
         
        <contributor>  <!-- may have multiple ... collection of $Model.Contributors -->
            <name>$Model.Contributor[n].Name</name>
          <uri>$Model.Contributor[n].Location</uri>
          <email>$Model.Contributor[n].Email</email>
        </contributor>
     
        <rights>AUTHOR-SUPPLIED-COPYRIGHT-STATEMENT</rights> <!-- NOT LICENSE TEXT ... hmm? -->
         
        <link rel="enclosure" href="http://foo/bar.msi" /> link to original location of the package itself.
        <link rel="related" title="sourcepackage" href="http://foo/bar-src.msi"/>

        <category term="$Model.Tag[x]" /> <!-- may have multiple tags -->
         
        <content type="text/html">
             $Model.Description *     <!-- this should be limited in supported HTML tags -->
        </content>
         
        <package:Package xmlns:package="http://coapp.org/atom-package-feed"> // our custom package data, xmlserialized.
            <package:ProductCode>PKG-GUID</package:ProductCode>
            <package:Name>PROPER-NAME</package:Name> <!-- name-version-platform -->
            <package:Architecture>x86</package:Architecture>
            <package:Version>281474976710656</package:Version>
            <package:PublicKeyToken>1231231231231231</package:PublicKeyToken>
            <package:BindingPolicyMinVersion>0</package:BindingPolicyMinVersion>
            <package:BindingPolicyMaxVersion>0</package:BindingPolicyMaxVersion>
            <package:Dependencies>COMMA-SEPERATED-GUIDS</package:Dependencies>
            <package:Locations>
                <package:string>./packages/foo.msi</package:string>
                <package:string>http://some-other-place.com/foo.msi</package:string>
            </package:Locations>
            
            <package:Icon>BASE-64-ENCODED-IMAGE(PNG, 256x256)</package:Icon>
          
            <!-- soon: potential package metadata tags:
             *  license: license text (or license URL?)
             *  etc...
             * -->
        </package:Package>
  </entry> 
         */

#endregion

using CoApp.Toolkit.Win32;

namespace CoApp.Toolkit.Engine.Model.Atom {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel.Syndication;
    using System.Xml;
    using Extensions;
    using Tasks;

    public class AtomItem : SyndicationItem {
        public PackageModel Model { get; private set; }

        /// <summary>
        ///   This needs to be called after anything in the model is changed.
        /// </summary>
        public void SyncFromModel() {
            // this pulls down information from the Model element into the atom item.
            Id = Model.ProductCode.ToString();
            Title = new TextSyndicationContent(Model.CosmeticName);
            Summary = new TextSyndicationContent(Model.PackageDetails.SummaryDescription);
            PublishDate = Model.PackageDetails.PublishDate;
            Authors.Clear();
            Contributors.Clear();
            Categories.Clear();
            Links.Clear();

            if (Model.PackageDetails.Publisher != null) {
                Authors.Add(CreatePerson().With(a => {
                    a.Name = Model.PackageDetails.Publisher.Name;
                    a.Email = Model.PackageDetails.Publisher.Email;
                    a.Uri = Model.PackageDetails.Publisher.Location == null ? string.Empty : Model.PackageDetails.Publisher.Location.ToString();
                }));
            }
            if (!Model.PackageDetails.Contributors.IsNullOrEmpty()) {
                foreach (var contributor in Model.PackageDetails.Contributors) {
                    Contributors.Add(CreatePerson().With(a => {
                        a.Name = contributor.Name;
                        a.Email = contributor.Email;
                        a.Uri = contributor.Location == null ? string.Empty : contributor.Location.ToString();
                    }));
                }
            }


            if( !string.IsNullOrEmpty(Model.PackageDetails.CopyrightStatement)) {
                Copyright = new TextSyndicationContent(Model.PackageDetails.CopyrightStatement);
            }

            if (!Model.PackageDetails.Tags.IsNullOrEmpty()) {
                foreach (var tag in Model.PackageDetails.Tags) {
                    Categories.Add(new SyndicationCategory(tag, "/Tags", tag));
                }
            }

            if (!Model.PackageDetails.Categories.IsNullOrEmpty()) {
                foreach (var category in Model.PackageDetails.Categories) {
                    Categories.Add(new SyndicationCategory(category, "/Categories", category));
                }
            }

            if (Model.PackageDetails.Description != null) {
                Content = SyndicationContent.CreateHtmlContent(Model.PackageDetails.Description);
            }

            if (!Model.Locations.IsNullOrEmpty()) {
                foreach (var location in Model.Locations) {
                    Links.Add(CreateLink().With(link => {
                        link.RelationshipType = "enclosure";
                        link.MediaType = "application/package";
                        link.Uri = location;
                        link.Title = Model.Name;
                    }));

                    Links.Add(CreateLink().With(link => { link.Uri = location; }));
                }
            }
            // and serialize that out.
            ElementExtensions.Add(Model, Model.XmlSerializer);
        }

        /// <summary>
        ///   This is only ever required after pulling values out of the AtomFeed XML, and is done automatically.
        /// </summary>
        private void SyncToModel() {
            // Model.ProductCode = Id; 
            Model.PackageDetails.SummaryDescription = Summary.Text;
            Model.PackageDetails.PublishDate = PublishDate.DateTime;

            var pub = Authors.FirstOrDefault();
            if (pub != null) {
                Model.PackageDetails.Publisher = new Identity {
                    Name = pub.Name,
                    Location = pub.Uri != null ? pub.Uri.ToUri() : null,
                    Email = pub.Email
                };
            }

            Model.PackageDetails.Contributors = Contributors.Select(each => new Identity {
                Name = each.Name,
                Location = each.Uri.ToUri(),
                Email = each.Email,
            }).ToList();

            Model.PackageDetails.Tags = Categories.Where(each => each.Scheme == "/Tags").Select(each => each.Name).ToList();
            Model.PackageDetails.Categories = Categories.Where(each => each.Scheme == "/Categories").Select(each => each.Name).ToList();

            var content = (Content as TextSyndicationContent);
            Model.PackageDetails.Description = content == null ? string.Empty : content.Text;

            Model.PackageDetails.CopyrightStatement = Copyright == null ? string.Empty : Copyright.Text; 

            Model.Locations = Links.Select(each => each.Uri.AbsoluteUri.ToUri()).Distinct().ToList();
        }

        /// <summary>
        /// </summary>
        /// <param name = "reader"></param>
        /// <param name = "version"></param>
        /// <returns>When the reader parses the embedded package model we sync that back to the exposed model right away.</returns>
        protected override bool TryParseElement(XmlReader reader, string version) {
            var extension = Model.XmlSerializer.Deserialize(reader) as PackageModel;
            if (extension != null) {
                Model = extension;
                SyncToModel();
            }
            return Model != null;
        }

        public AtomItem() {
            Model = new PackageModel();
        }

        public AtomItem(PackageModel model) {
            Model = model;
        }

#if COAPP_ENGINE_CORE
        public AtomItem(Package package) {
            Model = new PackageModel();
            Model.Name = package.Name;
            // var arch = Architecture.Unknown;
            // Enum.TryParse(package.Architecture, true, out arch);
            Model.Architecture = package.Architecture;
            Model.DisplayName = package.DisplayName;
            Model.Vendor = package.Vendor;

            Model.Version = package.Version;
            Model.PublicKeyToken = package.PublicKeyToken;
            Model.BindingPolicyMinVersion = package.InternalPackageData.PolicyMinimumVersion;
            Model.BindingPolicyMaxVersion = package.InternalPackageData.PolicyMaximumVersion;

            Model.Roles = new List<Role>(package.InternalPackageData.Roles);
            Model.Dependencies = package.InternalPackageData.Dependencies.Where(each => each.ProductCode.HasValue).Select(each => each.ProductCode.Value).ToList();
            if (!package.InternalPackageData.Features.IsNullOrEmpty()) {
                Model.Features = new List<Feature>(package.InternalPackageData.Features);
            }
            if (!package.InternalPackageData.RequiredFeatures.IsNullOrEmpty()) {
                Model.RequiredFeatures = new List<Feature>(package.InternalPackageData.RequiredFeatures);
            }

            Model.Feeds = package.InternalPackageData.FeedLocations.Select(each => each.ToUri()).ToList();
            Model.Locations = package.InternalPackageData.RemoteLocations.Select(each => each.ToUri()).ToList();

            // heh-heh, this makes life ... EASY!
            // Note: Should this be a clone? 
            Model.PackageDetails = package.PackageDetails;

            // when complete, 
            SyncFromModel();
        }

        /// <summary>
        ///   returns the package object for this element
        /// </summary>
        public Package Package {
            get {
                var package = Package.GetPackage(Model.Name, Model.Version, Model.Architecture, Model.PublicKeyToken, Model.ProductCode);

                // lets copy what details we have into that package.
                package.DisplayName = Model.DisplayName;
                package.Vendor = Model.Vendor;

                package.InternalPackageData.PolicyMinimumVersion = Model.BindingPolicyMinVersion;
                package.InternalPackageData.PolicyMaximumVersion = Model.BindingPolicyMaxVersion;
                if (package.InternalPackageData.Roles.IsNullOrEmpty()) {
                    package.InternalPackageData.Roles.AddRange(Model.Roles);
                }
                if (package.InternalPackageData.Dependencies.IsNullOrEmpty()) {
                    package.InternalPackageData.Dependencies.AddRange(Model.Dependencies.Select(each => Package.GetPackageFromProductCode(each)));
                }
                if (package.InternalPackageData.Features.IsNullOrEmpty() && !Model.Features.IsNullOrEmpty()) {
                    package.InternalPackageData.Features.AddRange( Model.Features );
                }
                if (package.InternalPackageData.RequiredFeatures.IsNullOrEmpty() && !Model.RequiredFeatures.IsNullOrEmpty()) {
                    package.InternalPackageData.RequiredFeatures.AddRange(Model.RequiredFeatures);
                }
                if(!Model.Feeds.IsNullOrEmpty()) {
                    foreach( var feed in Model.Feeds ) {
                        package.InternalPackageData.FeedLocation = feed.AbsoluteUri;
                    }
                }
                if (!Model.Locations.IsNullOrEmpty()) {
                    foreach (var location in Model.Locations) {
                        package.InternalPackageData.RemoteLocation = location.AbsoluteUri;
                    }
                }

                // store the place to get the cosmetic package details later 
                Cache<PackageDetails>.Value.Insert(package.ProductCode.ToString(), (unusedCanonicalFileName) => GetPackageDetails(package, Model));

                return package;
            }
        }

        internal static PackageDetails GetPackageDetails(Package pkg, PackageModel model) {
            return model.PackageDetails;
        }

#endif

    }
}